using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FFMpegCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Models;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.ExecutionProvider.DirectML;

namespace PickleIQ.Infrastructure.Services;

public class RallyDetectionService(
    IConfiguration configuration,
    ILogger<RallyDetectionService> logger) : IRallyDetectionService
{
    private const double FrameRateFps = 2.0;
    private const double MinRallySeconds = 3.0;
    private const double GapToleranceSeconds = 1.0;
    private const float PersonConfidenceThreshold = 0.4f;

    public async Task<IList<(double StartSeconds, double EndSeconds)>> DetectRalliesAsync(
        string videoPath, VideoMode mode = VideoMode.Match, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting rally detection for {VideoPath}", videoPath);

        var activeTimestamps = await RunDetectionPipelineAsync(videoPath, mode, cancellationToken);

        logger.LogInformation("Active in {Count} frames", activeTimestamps.Count);

        var segments = GroupIntoSegments(activeTimestamps);

        logger.LogInformation("Detected {Count} rally segments", segments.Count);
        return segments;
    }

    internal static int ComputeScaledHeight(int nativeW, int nativeH, int targetW)
    {
        var h = (int)Math.Round((double)targetW * nativeH / nativeW);
        return h % 2 == 0 ? h : h + 1;
    }

    private async Task<List<double>> RunDetectionPipelineAsync(
        string videoPath, VideoMode mode, CancellationToken cancellationToken)
    {
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        var workerCount = int.TryParse(
            configuration["Processing:PipelineWorkers"], out var w) ? w : 2;
        var minPlayers = mode is VideoMode.Training or VideoMode.FollowCam ? 1 : 2;

        var probe = await FFProbe.AnalyseAsync(videoPath, cancellationToken: cancellationToken);
        var videoStream = probe.VideoStreams.FirstOrDefault()
            ?? throw new InvalidOperationException($"No video stream found in {videoPath}");

        var scaledH = ComputeScaledHeight(videoStream.Width, videoStream.Height, 640);
        var frameSize = 640 * scaledH * 4; // bgra = 4 bytes per pixel

        logger.LogInformation(
            "Pipeline: {W}x{H} native → 640x{ScaledH}, workers={Workers}",
            videoStream.Width, videoStream.Height, scaledH, workerCount);

        var channel = Channel.CreateBounded<(int Index, SKBitmap Frame)>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait });

        var activeTimestamps = new ConcurrentBag<double>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var producerTask = RunProducerAsync(
            videoPath, scaledH, frameSize, channel.Writer, ffOptions, cts);

        var consumerTasks = Enumerable.Range(0, workerCount)
            .Select(id => RunConsumerAsync(id, channel.Reader, minPlayers, activeTimestamps, cts))
            .ToArray();

        await Task.WhenAll(new[] { producerTask }.Concat(consumerTasks));

        return activeTimestamps.OrderBy(t => t).ToList();
    }

    internal static bool IsFrameActive(
        int personCount,
        int minPlayers,
        bool ballDetected)
        => personCount >= minPlayers && ballDetected;

    internal static bool IsFrameActive(
        int personCount,
        int minPlayers,
        bool ballDetected,
        bool anyPlayerSwinging)
        => personCount >= minPlayers && ballDetected && anyPlayerSwinging;

    private const float MinKeypointConfidence = 0.5f;

    internal static bool AnyPlayerSwinging(
        IEnumerable<(float X, float Y, float Confidence)[]> personsKeypoints)
    {
        foreach (var kps in personsKeypoints)
        {
            if (kps.Length < 11) continue;

            var leftShoulder  = kps[5];
            var rightShoulder = kps[6];
            var leftWrist     = kps[9];
            var rightWrist    = kps[10];

            // Left arm: wrist above shoulder (smaller Y) with sufficient confidence
            if (leftWrist.Confidence  >= MinKeypointConfidence &&
                leftShoulder.Confidence >= MinKeypointConfidence &&
                leftWrist.Y < leftShoulder.Y)
                return true;

            // Right arm
            if (rightWrist.Confidence  >= MinKeypointConfidence &&
                rightShoulder.Confidence >= MinKeypointConfidence &&
                rightWrist.Y < rightShoulder.Y)
                return true;
        }
        return false;
    }

    private static List<(double StartSeconds, double EndSeconds)> GroupIntoSegments(List<double> activeTimestamps)
    {
        if (activeTimestamps.Count == 0) return [];

        var segments = new List<(double Start, double End)>();
        var segStart = activeTimestamps[0];
        var segEnd = activeTimestamps[0];

        for (int i = 1; i < activeTimestamps.Count; i++)
        {
            var gap = activeTimestamps[i] - segEnd;
            if (gap <= GapToleranceSeconds)
            {
                segEnd = activeTimestamps[i];
            }
            else
            {
                if (segEnd - segStart >= MinRallySeconds)
                    segments.Add((segStart, segEnd));
                segStart = activeTimestamps[i];
                segEnd = activeTimestamps[i];
            }
        }

        if (segEnd - segStart >= MinRallySeconds)
            segments.Add((segStart, segEnd));

        return segments;
    }

    private static async ValueTask<int> ReadExactAsync(
        Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    private async Task RunProducerAsync(
        string videoPath,
        int scaledH,
        int frameSize,
        ChannelWriter<(int Index, SKBitmap Frame)> writer,
        FFOptions ffOptions,
        CancellationTokenSource cts)
    {
        var ffmpegExe = Path.Combine(ffOptions.BinaryFolder ?? "", "ffmpeg.exe");
        if (!File.Exists(ffmpegExe)) ffmpegExe = "ffmpeg";

        var args = $"-y -i \"{videoPath.Replace("\\", "/")}\" " +
                   $"-vf scale=640:{scaledH},fps={FrameRateFps} " +
                   $"-f rawvideo -pix_fmt bgra pipe:1";

        var psi = new ProcessStartInfo(ffmpegExe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi)!;
            // Drain stderr concurrently — prevents pipe buffer deadlock on long videos
            var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);

            var stdout = proc.StandardOutput.BaseStream;
            var buffer = new byte[frameSize];
            var frameIndex = 0;

            while (true)
            {
                var bytesRead = await ReadExactAsync(stdout, buffer, frameSize, cts.Token);
                if (bytesRead < frameSize) break;

                var bmp = new SKBitmap(new SKImageInfo(640, scaledH, SKColorType.Bgra8888, SKAlphaType.Opaque));
                Marshal.Copy(buffer, 0, bmp.GetPixels(), frameSize);

                try
                {
                    await writer.WriteAsync((frameIndex++, bmp), cts.Token);
                }
                catch
                {
                    bmp.Dispose();
                    throw;
                }
            }

            await proc.WaitForExitAsync(cts.Token);

            if (proc.ExitCode != 0)
            {
                var stderr = await stderrTask;
                throw new InvalidOperationException(
                    $"FFmpeg exited {proc.ExitCode}: {stderr[..Math.Min(500, stderr.Length)]}");
            }

            logger.LogInformation("Producer: streamed {Count} frames", frameIndex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cts.Cancel();
            throw;
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task RunConsumerAsync(
        int workerId,
        ChannelReader<(int Index, SKBitmap Frame)> reader,
        int minPlayers,
        ConcurrentBag<double> activeTimestamps,
        CancellationTokenSource cts)
    {
        var modelPath = configuration["YoloModel:Path"]
            ?? Path.Combine(AppContext.BaseDirectory, "Models", "yolo26n.onnx");
        var ballConfidenceThreshold = float.TryParse(
            configuration["YoloModel:BallConfidenceThreshold"], out var bt) ? bt : 0.25f;
        // Note: RunObjectDetection pre-filters at PersonConfidenceThreshold (0.4f),
        // so BallConfidenceThreshold values below 0.4 have no additional effect.

        var useGpu = workerId == 0
            && bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;

        Yolo? yolo = null;

        if (useGpu)
        {
            try
            {
                yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new DirectMLExecutionProvider(modelPath)
                });
                logger.LogInformation("Consumer {WorkerId}: YOLO on GPU (DirectML)", workerId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consumer {WorkerId}: DirectML failed, falling back to CPU", workerId);
            }
        }

        if (yolo is null)
        {
            yolo = new Yolo(new YoloOptions
            {
                ExecutionProvider = new CpuExecutionProvider(modelPath)
            });
            logger.LogInformation("Consumer {WorkerId}: YOLO on CPU", workerId);
        }

        using (yolo)
        {
            try
            {
                await foreach (var (index, frame) in reader.ReadAllAsync(cts.Token))
                {
                    using (frame)
                    {
                        try
                        {
                            var detections = yolo.RunObjectDetection(
                                frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
                            var personCount = detections.Count(d => d.Label.Name == "person");
                            var ballDetected = detections.Any(d =>
                                d.Label.Name == "sports ball" && d.Confidence >= ballConfidenceThreshold);
                            var isActive = IsFrameActive(personCount, minPlayers, ballDetected);
                            if (isActive)
                                activeTimestamps.Add(index * (1.0 / FrameRateFps));

                            if (logger.IsEnabled(LogLevel.Debug))
                            {
                                var timestamp = index * (1.0 / FrameRateFps);
                                var labelSummary = detections.Count > 0
                                    ? string.Join(", ", detections
                                        .GroupBy(d => d.Label.Name)
                                        .OrderByDescending(g => g.Count())
                                        .Select(g => $"{g.Key}×{g.Count()}"))
                                    : "(none)";
                                var ballStatus = ballDetected ? "ball✓" : "no ball";
                                var status = isActive
                                    ? "ACTIVE"
                                    : personCount < minPlayers
                                        ? $"inactive (persons={personCount} min={minPlayers})"
                                        : "inactive (no ball)";
                                logger.LogDebug(
                                    "Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {Labels} | {BallStatus} → {Status}",
                                    workerId, index, timestamp, labelSummary, ballStatus, status);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex,
                                "Consumer {WorkerId}: frame {Index} detection failed, skipping",
                                workerId, index);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                cts.Cancel();
                throw;
            }
        }
    }
}
