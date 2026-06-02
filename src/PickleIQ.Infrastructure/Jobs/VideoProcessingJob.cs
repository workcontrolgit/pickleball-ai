using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;

namespace PickleIQ.Infrastructure.Jobs;

public class VideoProcessingJob(
    AppDbContext db,
    IRallyDetectionService rallyDetectionService,
    ICoachingFrameSampler frameSampler,
    IHighlightGenerationService highlightGenerationService,
    ICoachingEngine coachingEngine,
    ILogger<VideoProcessingJob> logger) : IVideoProcessingJob
{
    public async Task ProcessAsync(Guid jobId)
    {
        logger.LogInformation("Starting video processing for job {JobId}", jobId);

        var job = await db.VideoJobs.FindAsync(jobId);
        if (job is null)
        {
            logger.LogWarning("VideoJob {JobId} not found", jobId);
            return;
        }

        try
        {
            // Step 1: Rally Detection
            job.Status = VideoJobStatus.RallyDetectionInProgress;
            await db.SaveChangesAsync();

            var segments = await rallyDetectionService.DetectRalliesAsync(job.FilePath);

            foreach (var (start, end) in segments)
            {
                db.RallySegments.Add(new RallySegment
                {
                    Id = Guid.NewGuid(),
                    VideoJobId = jobId,
                    StartSeconds = start,
                    EndSeconds = end
                });
            }

            job.Status = VideoJobStatus.RallyDetectionComplete;
            await db.SaveChangesAsync();

            logger.LogInformation("Job {JobId}: {Count} rally segments detected", jobId, segments.Count);

            // Steps 2a/2b/2c — run in parallel
            job.Status = VideoJobStatus.HighlightInProgress;
            await db.SaveChangesAsync();

            var (highlightPath, coachingFrames, totalMatchSeconds) = await RunParallelStepsAsync(job, segments);

            if (!string.IsNullOrEmpty(highlightPath))
                job.HighlightFilePath = highlightPath;
            job.Status = VideoJobStatus.HighlightComplete;
            await db.SaveChangesAsync();

            logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);

            // Step 4: Coaching Report
            job.Status = VideoJobStatus.ReportInProgress;
            await db.SaveChangesAsync();

            var savedSegments = await db.RallySegments.Where(r => r.VideoJobId == jobId).ToListAsync();
            var durations = savedSegments.Select(s => s.EndSeconds - s.StartSeconds).ToList();

            var summary = new MatchSummary(
                RallyCount: savedSegments.Count,
                AverageRallySeconds: durations.Count > 0 ? durations.Average() : 0,
                LongestRallySeconds: durations.Count > 0 ? durations.Max() : 0,
                TotalMatchSeconds: totalMatchSeconds);

            var report = await coachingEngine.GenerateReportHtmlAsync(summary, coachingFrames);

            db.CoachingReports.Add(new CoachingReport
            {
                Id = Guid.NewGuid(),
                VideoJobId = jobId,
                HtmlContent = report
            });

            job.Status = VideoJobStatus.ReportComplete;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("Job {JobId} completed successfully", jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Status = VideoJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            await db.SaveChangesAsync();
        }
    }

    private async Task<(string? HighlightPath, IReadOnlyList<byte[]> CoachingFrames, double TotalMatchSeconds)>
        RunParallelStepsAsync(VideoJob job, IList<(double StartSeconds, double EndSeconds)> segments)
    {
        var highlightTask = highlightGenerationService.GenerateAsync(job.Id, job.FilePath);
        var framesTask = frameSampler.SampleAsync(
            job.FilePath,
            (IReadOnlyList<(double StartSeconds, double EndSeconds)>)segments);
        var probeTask = FFProbe.AnalyseAsync(job.FilePath);

        await Task.WhenAll(highlightTask, framesTask, probeTask);

        return (await highlightTask, await framesTask, (await probeTask).Duration.TotalSeconds);
    }
}
