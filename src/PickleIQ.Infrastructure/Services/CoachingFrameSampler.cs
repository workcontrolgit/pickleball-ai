using FFMpegCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.Services;

public class CoachingFrameSampler(
    IConfiguration configuration,
    ILogger<CoachingFrameSampler> logger) : ICoachingFrameSampler
{
    public async Task<IReadOnlyList<byte[]>> SampleAsync(
        string videoPath,
        IReadOnlyList<(double StartSeconds, double EndSeconds)> rallies,
        CancellationToken cancellationToken = default)
    {
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        var tempDir = Path.Combine(Path.GetTempPath(), $"pickleiq-coaching-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var maxRallies = int.TryParse(configuration["Coaching:MaxRallies"], out var mr) ? mr : 10;
            var topRallies = rallies
                .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
                .Take(maxRallies)
                .ToList();

            var frames = new List<byte[]>();

            foreach (var rally in topRallies)
            {
                var duration = rally.EndSeconds - rally.StartSeconds;
                var timestamps = new[] { 0.25, 0.5, 0.75 }
                    .Select(pct => rally.StartSeconds + duration * pct);

                foreach (var ts in timestamps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var framePath = Path.Combine(tempDir, $"frame-{Guid.NewGuid()}.jpg");

                    try
                    {
                        await FFMpegArguments
                            .FromFileInput(videoPath, verifyExists: false,
                                opts => opts.Seek(TimeSpan.FromSeconds(ts)))
                            .OutputToFile(framePath, true, opts => opts
                                .WithFrameOutputCount(1)
                                .WithVideoFilters(f => f.Scale(640, -2))
                                .ForceFormat("image2"))
                            .ProcessAsynchronously(true, ffOptions);

                        if (File.Exists(framePath))
                            frames.Add(await File.ReadAllBytesAsync(framePath, cancellationToken));
                        else
                            logger.LogWarning("FFmpeg ran successfully but frame file not found at {FramePath}", framePath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to extract coaching frame at {Timestamp:F1}s — skipping", ts);
                    }
                }
            }

            logger.LogInformation("Sampled {Count} coaching frames from {RallyCount} rallies",
                frames.Count, topRallies.Count);
            return frames;
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
