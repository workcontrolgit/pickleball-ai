using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;
using PickleIQ.Infrastructure.Jobs;

namespace PickleIQ.Infrastructure.Services;

public class VideoStorageService(
    AppDbContext db,
    IConfiguration configuration,
    IBackgroundJobClient backgroundJobClient,
    ILogger<VideoStorageService> logger) : IVideoStorageService
{
    public async Task<Guid> SaveAsync(Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default)
    {
        var basePath = configuration["VideoStorage:BasePath"] ?? "C:/temp/pickleiq/videos";
        Directory.CreateDirectory(basePath);

        var jobId = Guid.NewGuid();
        var safeFileName = $"{jobId}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(basePath, safeFileName);

        await using var dest = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(dest, cancellationToken);

        var job = new VideoJob
        {
            Id = jobId,
            FileName = fileName,
            FilePath = filePath,
            Status = VideoJobStatus.Queued
        };

        db.VideoJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        backgroundJobClient.Enqueue<VideoProcessingJob>(j => j.ProcessAsync(jobId));

        logger.LogInformation("Video job {JobId} created for file {FileName}", jobId, fileName);
        return jobId;
    }
}
