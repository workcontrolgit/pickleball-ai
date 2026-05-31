using System.Diagnostics;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;

namespace PickleIQ.Infrastructure.Services;

public class HighlightGenerationService(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<HighlightGenerationService> logger) : IHighlightGenerationService
{
    private const double PaddingSeconds = 2.0;

    public async Task<string> GenerateAsync(Guid jobId, string videoPath, CancellationToken cancellationToken = default)
    {
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        var targetDuration = double.TryParse(configuration["Processing:TargetHighlightDurationSeconds"], out var td) ? td : 60.0;
        var useGpu = bool.TryParse(configuration["Processing:UseGpuEncoding"], out var gpu) && gpu;
        var crf = int.TryParse(configuration["Processing:Crf"], out var q) ? q : 23;

        logger.LogInformation("Generating highlights for job {JobId} (GPU encoding: {UseGpu})", jobId, useGpu);

        var segments = await db.RallySegments
            .Where(r => r.VideoJobId == jobId)
            .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
            .ToListAsync(cancellationToken);

        if (segments.Count == 0)
        {
            logger.LogWarning("No rally segments found for job {JobId}; skipping highlight generation", jobId);
            return string.Empty;
        }

        // Select segments until we hit target total
        var selected = new List<(double Start, double End)>();
        double accumulated = 0;
        foreach (var seg in segments)
        {
            if (accumulated >= targetDuration) break;
            selected.Add((seg.StartSeconds, seg.EndSeconds));
            accumulated += seg.EndSeconds - seg.StartSeconds;
        }

        var highlightsPath = configuration["VideoStorage:HighlightsPath"] ?? "C:/temp/pickleiq/highlights";
        Directory.CreateDirectory(highlightsPath);

        var tempDir = Path.Combine(Path.GetTempPath(), $"pickleiq-clips-{jobId}");
        Directory.CreateDirectory(tempDir);

        var concatListPath = Path.Combine(tempDir, "concat.txt");
        var outputPath = Path.Combine(highlightsPath, $"{jobId}-highlights.mp4");

        var ffmpegExe = Path.Combine(ffOptions.BinaryFolder ?? "", "ffmpeg.exe");
        if (!File.Exists(ffmpegExe)) ffmpegExe = "ffmpeg";

        try
        {
            // Extract clips — try GPU first, fall back to CPU
            var clipPaths = new List<string>();
            for (int i = 0; i < selected.Count; i++)
            {
                var (start, end) = selected[i];
                var paddedStart = Math.Max(0, start - PaddingSeconds);
                var paddedEnd = end + PaddingSeconds;
                var clipPath = Path.Combine(tempDir, $"clip-{i:D3}.mp4");

                var clipEncoded = false;
                if (useGpu)
                {
                    clipEncoded = await TryExtractClipAsync(
                        ffmpegExe, videoPath, paddedStart, paddedEnd - paddedStart, clipPath,
                        codec: "h264_nvenc", preset: configuration["Processing:Preset"] ?? "p4",
                        crf: crf, useNvencQuality: true, cancellationToken);

                    if (!clipEncoded)
                        logger.LogWarning("GPU clip extraction failed for clip {I}, falling back to CPU", i);
                }

                if (!clipEncoded)
                {
                    await TryExtractClipAsync(
                        ffmpegExe, videoPath, paddedStart, paddedEnd - paddedStart, clipPath,
                        codec: "libx264", preset: configuration["Processing:FallbackPreset"] ?? "fast",
                        crf: crf, useNvencQuality: false, cancellationToken);
                }

                clipPaths.Add(clipPath);
            }

            // Write FFmpeg concat list
            var lines = clipPaths.Select(p => $"file '{p.Replace("\\", "/")}'");
            await File.WriteAllLinesAsync(concatListPath, lines, cancellationToken);

            // Concatenate — try GPU, fall back to CPU
            var concatDone = false;
            if (useGpu)
            {
                concatDone = await TryConcatAsync(
                    ffmpegExe, concatListPath, outputPath,
                    codec: "h264_nvenc", preset: configuration["Processing:Preset"] ?? "p4",
                    crf: crf, useNvencQuality: true, cancellationToken);

                if (!concatDone)
                    logger.LogWarning("GPU concat failed, falling back to CPU");
            }

            if (!concatDone)
            {
                await TryConcatAsync(
                    ffmpegExe, concatListPath, outputPath,
                    codec: "libx264", preset: configuration["Processing:FallbackPreset"] ?? "fast",
                    crf: crf, useNvencQuality: false, cancellationToken);
            }

            logger.LogInformation("Highlight reel created at {OutputPath}", outputPath);
            return outputPath;
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task<bool> TryExtractClipAsync(
        string ffmpegExe, string inputPath, double startSeconds, double durationSeconds,
        string outputPath, string codec, string preset, int crf, bool useNvencQuality,
        CancellationToken cancellationToken)
    {
        var qualityArg = useNvencQuality ? $"-cq {crf} -b:v 0" : $"-crf {crf}";
        var args = $"-y -ss {startSeconds:F3} -i \"{inputPath.Replace("\\", "/")}\" " +
                   $"-t {durationSeconds:F3} " +
                   $"-c:v {codec} {qualityArg} -preset {preset} -pix_fmt yuv420p " +
                   $"-c:a aac \"{outputPath.Replace("\\", "/")}\"";

        return await RunFfmpegAsync(ffmpegExe, args, cancellationToken);
    }

    private async Task<bool> TryConcatAsync(
        string ffmpegExe, string concatListPath, string outputPath,
        string codec, string preset, int crf, bool useNvencQuality,
        CancellationToken cancellationToken)
    {
        var qualityArg = useNvencQuality ? $"-cq {crf} -b:v 0" : $"-crf {crf}";
        var args = $"-y -f concat -safe 0 -i \"{concatListPath.Replace("\\", "/")}\" " +
                   $"-c:v {codec} {qualityArg} -preset {preset} -pix_fmt yuv420p " +
                   $"-c:a aac \"{outputPath.Replace("\\", "/")}\"";

        return await RunFfmpegAsync(ffmpegExe, args, cancellationToken);
    }

    private async Task<bool> RunFfmpegAsync(string ffmpegExe, string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(ffmpegExe, args)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);

        if (proc.ExitCode != 0)
        {
            logger.LogWarning("FFmpeg failed (exit {Code}): {Stderr}", proc.ExitCode, stderr[..Math.Min(500, stderr.Length)]);
            return false;
        }

        return true;
    }
}
