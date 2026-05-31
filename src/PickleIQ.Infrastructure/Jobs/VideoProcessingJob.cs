using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;

namespace PickleIQ.Infrastructure.Jobs;

public class VideoProcessingJob(AppDbContext db, ILogger<VideoProcessingJob> logger) : IVideoProcessingJob
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
            // Step 1: Rally Detection (stub — to be implemented in Task 11)
            job.Status = VideoJobStatus.RallyDetectionInProgress;
            await db.SaveChangesAsync();
            logger.LogInformation("Job {JobId}: rally detection placeholder", jobId);
            job.Status = VideoJobStatus.RallyDetectionComplete;
            await db.SaveChangesAsync();

            // Step 2: Highlight Generation (stub — to be implemented in Task 12)
            job.Status = VideoJobStatus.HighlightInProgress;
            await db.SaveChangesAsync();
            logger.LogInformation("Job {JobId}: highlight generation placeholder", jobId);
            job.Status = VideoJobStatus.HighlightComplete;
            await db.SaveChangesAsync();

            // Step 3: Coaching Report (stub — to be implemented in Task 13)
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
