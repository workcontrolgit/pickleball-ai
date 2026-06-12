using FFMpegCore;
using FFMpegCore.Enums;
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
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        logger.LogInformation("Starting rally detection for {VideoPath}", videoPath);

        var framesDir = Path.Combine(Path.GetTempPath(), $"pickleiq-frames-{Guid.NewGuid()}");
        Directory.CreateDirectory(framesDir);

        try
        {
            // Extract frames at 2fps
            await ExtractFramesAsync(videoPath, framesDir, ffOptions, cancellationToken);

            var frameFiles = Directory.GetFiles(framesDir, "*.jpg")
                .OrderBy(f => f)
                .ToList();

            logger.LogInformation("Extracted {Count} frames", frameFiles.Count);

            // Detect players in each frame
            var modelPath = configuration["YoloModel:Path"]
                ?? Path.Combine(AppContext.BaseDirectory, "Models", "yolo11n.onnx");

            var activeFrames = DetectActiveFrames(frameFiles, modelPath, mode);

            // Group active frames into rally segments
            var segments = GroupIntoSegments(activeFrames);

            logger.LogInformation("Detected {Count} rally segments", segments.Count);
            return segments;
        }
        finally
        {
            Directory.Delete(framesDir, recursive: true);
        }
    }

    internal static int ComputeScaledHeight(int nativeW, int nativeH, int targetW)
    {
        var h = (int)Math.Round((double)targetW * nativeH / nativeW);
        return h % 2 == 0 ? h : h + 1;
    }

    private static async Task ExtractFramesAsync(string videoPath, string outputDir, FFOptions ffOptions, CancellationToken cancellationToken)
    {
        await FFMpegArguments
            .FromFileInput(videoPath)
            .OutputToFile(
                Path.Combine(outputDir, "frame-%05d.jpg"),
                overwrite: true,
                options => options
                    .WithVideoFilters(f => f.Scale(640, -2))   // -2 = nearest even number (YOLO requires even dims)
                    .WithFramerate(FrameRateFps)
                    .ForceFormat("image2"))
            .ProcessAsynchronously(true, ffOptions);
    }

    private List<double> DetectActiveFrames(List<string> frameFiles, string modelPath, VideoMode mode)
    {
        if (!File.Exists(modelPath))
        {
            logger.LogWarning("YOLO model not found at {ModelPath}. Rally detection will return empty results.", modelPath);
            return [];
        }

        var activeTimestamps = new List<double>();
        var secondsPerFrame = 1.0 / FrameRateFps;
        var minPlayers = mode is VideoMode.Training or VideoMode.FollowCam ? 1 : 2;

        var useGpuYolo = bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;
        Yolo? yolo = null;

        if (useGpuYolo)
        {
            try
            {
                yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new DirectMLExecutionProvider(modelPath)
                });
                logger.LogInformation("YOLO running on GPU (DirectML)");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DirectML execution provider failed — falling back to CPU");
            }
        }

        if (yolo is null)
        {
            try
            {
                yolo = new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(modelPath)
                });
                logger.LogInformation("YOLO running on CPU");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize YOLO model from {ModelPath}", modelPath);
                return [];
            }
        }

        using (yolo)
        {
            for (int i = 0; i < frameFiles.Count; i++)
            {
                var frameTimestamp = i * secondsPerFrame;
                try
                {
                    using var bitmap = SKBitmap.Decode(frameFiles[i]);
                    if (bitmap is null) continue;

                    var detections = yolo.RunObjectDetection(bitmap, confidence: PersonConfidenceThreshold, iou: 0.5f);
                    var personCount = detections.Count(d => d.Label.Name == "person");

                    if (personCount >= minPlayers)
                        activeTimestamps.Add(frameTimestamp);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Frame {FrameIndex} detection failed, skipping", i);
                }
            }
        }

        return activeTimestamps;
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
}
