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
    private const double TargetDurationSeconds = 60.0;
    private const double PaddingSeconds = 2.0;

    public async Task<string> GenerateAsync(Guid jobId, string videoPath, CancellationToken cancellationToken = default)
    {
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        logger.LogInformation("Generating highlights for job {JobId}", jobId);

        var segments = await db.RallySegments
            .Where(r => r.VideoJobId == jobId)
            .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
            .ToListAsync(cancellationToken);

        if (segments.Count == 0)
        {
            logger.LogWarning("No rally segments found for job {JobId}; skipping highlight generation", jobId);
            return string.Empty;
        }

        // Select segments until we hit ~60s total
        var selected = new List<(double Start, double End)>();
        double accumulated = 0;
        foreach (var seg in segments)
        {
            if (accumulated >= TargetDurationSeconds) break;
            selected.Add((seg.StartSeconds, seg.EndSeconds));
            accumulated += seg.EndSeconds - seg.StartSeconds;
        }

        var highlightsPath = configuration["VideoStorage:HighlightsPath"] ?? "C:/temp/pickleiq/highlights";
        Directory.CreateDirectory(highlightsPath);

        var tempDir = Path.Combine(Path.GetTempPath(), $"pickleiq-clips-{jobId}");
        Directory.CreateDirectory(tempDir);

        var concatListPath = Path.Combine(tempDir, "concat.txt");
        var outputPath = Path.Combine(highlightsPath, $"{jobId}-highlights.mp4");

        try
        {
            var clipPaths = new List<string>();
            for (int i = 0; i < selected.Count; i++)
            {
                var (start, end) = selected[i];
                var paddedStart = Math.Max(0, start - PaddingSeconds);
                var paddedEnd = end + PaddingSeconds;
                var clipPath = Path.Combine(tempDir, $"clip-{i:D3}.mp4");

                await FFMpegArguments
                    .FromFileInput(videoPath, verifyExists: true, options => options
                        .Seek(TimeSpan.FromSeconds(paddedStart)))
                    .OutputToFile(clipPath, overwrite: true, options => options
                        .WithDuration(TimeSpan.FromSeconds(paddedEnd - paddedStart))
                        .WithVideoCodec("libx264")
                        .WithConstantRateFactor(23)
                        .WithAudioCodec("aac")
                        .ForceFormat("mp4"))
                    .ProcessAsynchronously(true, ffOptions);

                clipPaths.Add(clipPath);
            }

            // Write FFmpeg concat list
            var lines = clipPaths.Select(p => $"file '{p.Replace("\\", "/")}'");
            await File.WriteAllLinesAsync(concatListPath, lines, cancellationToken);

            // Concatenate clips — -f concat -safe 0 must appear before -i
            // FFMpegCore places input options after -i, so we invoke ffmpeg directly here
            var ffmpegExe = Path.Combine(ffOptions.BinaryFolder ?? "", "ffmpeg.exe");
            if (!File.Exists(ffmpegExe)) ffmpegExe = "ffmpeg"; // fall back to PATH

            var concatArgs = $"-y -f concat -safe 0 -i \"{concatListPath.Replace("\\", "/")}\" -c:v libx264 -crf 23 -preset fast -c:a aac \"{outputPath.Replace("\\", "/")}\"";
            var psi = new ProcessStartInfo(ffmpegExe, concatArgs)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg concat failed: {stderr}");

            logger.LogInformation("Highlight reel created at {OutputPath}", outputPath);
            return outputPath;
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
