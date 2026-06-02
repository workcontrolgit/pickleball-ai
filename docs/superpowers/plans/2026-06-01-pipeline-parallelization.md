# Pipeline Parallelization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run highlight generation, frame sampling, and FFProbe concurrently inside `VideoProcessingJob.ProcessAsync` using `Task.WhenAll`, reducing per-video processing time by 20–40 seconds.

**Architecture:** Only `VideoProcessingJob.cs` changes. After rally detection completes, a new private helper `RunParallelStepsAsync` starts all three tasks simultaneously and awaits them together. The coaching report step then uses all three results as before. No service interfaces, no status enum values, no other files change.

**Tech Stack:** .NET 10, `System.Threading.Tasks.Task.WhenAll`, FFMpegCore (`FFProbe.AnalyseAsync`), existing services unchanged

---

## Files Changed

| Action | File |
|--------|------|
| Modify | `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` |

---

## Task 1: Parallelize highlight generation, frame sampling, and FFProbe

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

The current sequential flow (lines 53–68) runs highlight generation, then frame sampling, then FFProbe one after another. Replace all three with a single `Task.WhenAll` call, and add a private helper that owns the parallel execution.

- [ ] **Step 1: Replace the sequential steps 2 and 3 in `ProcessAsync`**

  Open `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`.

  Find this block (currently lines 53–68):

  ```csharp
  // Step 2: Highlight Generation
  job.Status = VideoJobStatus.HighlightInProgress;
  await db.SaveChangesAsync();

  var highlightPath = await highlightGenerationService.GenerateAsync(jobId, job.FilePath);
  if (!string.IsNullOrEmpty(highlightPath))
      job.HighlightFilePath = highlightPath;
  job.Status = VideoJobStatus.HighlightComplete;
  await db.SaveChangesAsync();

  logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);

  // Step 3: Sample coaching frames + get video duration
  var coachingFrames = await frameSampler.SampleAsync(job.FilePath, (IReadOnlyList<(double StartSeconds, double EndSeconds)>)segments);
  var mediaInfo = await FFProbe.AnalyseAsync(job.FilePath);
  var totalMatchSeconds = mediaInfo.Duration.TotalSeconds;
  ```

  Replace it with:

  ```csharp
  // Steps 2a/2b/2c — run in parallel
  job.Status = VideoJobStatus.HighlightInProgress;
  await db.SaveChangesAsync();

  var (highlightPath, coachingFrames, totalMatchSeconds) = await RunParallelStepsAsync(job, segments);

  if (!string.IsNullOrEmpty(highlightPath))
      job.HighlightFilePath = highlightPath;
  job.Status = VideoJobStatus.HighlightComplete;
  await db.SaveChangesAsync();

  logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);
  ```

- [ ] **Step 2: Add the `RunParallelStepsAsync` private helper**

  After the closing brace of `ProcessAsync` (before the class's closing brace), add:

  ```csharp
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
  ```

  The complete file should now look like:

  ```csharp
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
  ```

- [ ] **Step 3: Build to verify compilation**

  ```bash
  cd c:/apps/pickleball/PickleIQ
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
  ```

  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs
  git commit -m "perf: parallelize highlight gen, frame sampling, FFProbe with Task.WhenAll"
  ```
