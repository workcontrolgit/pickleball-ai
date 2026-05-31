using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;

namespace PickleIQ.Infrastructure.Jobs;

public class VideoProcessingJob(
    AppDbContext db,
    IRallyDetectionService rallyDetectionService,
    IHighlightGenerationService highlightGenerationService,
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

            // Step 2: Highlight Generation
            job.Status = VideoJobStatus.HighlightInProgress;
            await db.SaveChangesAsync();

            var highlightPath = await highlightGenerationService.GenerateAsync(jobId, job.FilePath);
            job.HighlightFilePath = highlightPath;
            job.Status = VideoJobStatus.HighlightComplete;
            await db.SaveChangesAsync();

            logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);

            // Step 3: Coaching Report (stub — implemented in Task 13)
            job.Status = VideoJobStatus.ReportInProgress;
            await db.SaveChangesAsync();
            logger.LogInformation("Job {JobId}: coaching report placeholder", jobId);
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
}
