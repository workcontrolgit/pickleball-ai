# Job Status Push Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 5-second DB polling timer in `Results.razor` with instant in-process status push, so status transitions appear immediately and SQL polling stops.

**Architecture:** A singleton `JobStatusService` (backed by `System.Threading.Channels`) mirrors the `CoachingStreamService` pattern. `VideoProcessingJob` calls `PushStatus(jobId, status)` after each `db.SaveChangesAsync()`. `Results.razor` reads from the channel and calls `RefreshAsync()` + `StateHasChanged()` on each update. A 60-second fallback timer remains as a safety net for race conditions (page loaded after job already finished).

**Tech Stack:** .NET 10, `System.Threading.Channels`, Blazor Server `InvokeAsync`/`StateHasChanged`, no new NuGet packages

---

## Files Changed

| Action | File |
|--------|------|
| Create | `src/PickleIQ.Core/Interfaces/IJobStatusService.cs` |
| Create | `src/PickleIQ.Infrastructure/Services/JobStatusService.cs` |
| Modify | `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` |
| Modify | `src/PickleIQ.Web/Program.cs` |
| Modify | `src/PickleIQ.Web/Components/Pages/Results.razor` |

---

## Task 1: IJobStatusService + JobStatusService

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/IJobStatusService.cs`
- Create: `src/PickleIQ.Infrastructure/Services/JobStatusService.cs`

- [ ] **Step 1: Create the interface**

  Create `src/PickleIQ.Core/Interfaces/IJobStatusService.cs`:

  ```csharp
  using System.Threading.Channels;
  using PickleIQ.Core.Entities;

  namespace PickleIQ.Core.Interfaces;

  public interface IJobStatusService
  {
      void Subscribe(Guid jobId);
      void PushStatus(Guid jobId, VideoJobStatus status);
      void Unsubscribe(Guid jobId);
      ChannelReader<VideoJobStatus>? TryGetReader(Guid jobId);
  }
  ```

- [ ] **Step 2: Create the implementation**

  Create `src/PickleIQ.Infrastructure/Services/JobStatusService.cs`:

  ```csharp
  using System.Collections.Concurrent;
  using System.Threading.Channels;
  using PickleIQ.Core.Entities;
  using PickleIQ.Core.Interfaces;

  namespace PickleIQ.Infrastructure.Services;

  public class JobStatusService : IJobStatusService
  {
      private readonly ConcurrentDictionary<Guid, Channel<VideoJobStatus>> _channels = new();

      public void Subscribe(Guid jobId)
      {
          _channels.TryAdd(jobId, Channel.CreateUnbounded<VideoJobStatus>(new UnboundedChannelOptions
          {
              SingleWriter = true,
              SingleReader = true,
              AllowSynchronousContinuations = false
          }));
      }

      public void PushStatus(Guid jobId, VideoJobStatus status)
      {
          if (_channels.TryGetValue(jobId, out var channel))
              _ = channel.Writer.TryWrite(status);
      }

      public void Unsubscribe(Guid jobId)
      {
          if (_channels.TryRemove(jobId, out var channel))
              channel.Writer.TryComplete();
      }

      public ChannelReader<VideoJobStatus>? TryGetReader(Guid jobId) =>
          _channels.TryGetValue(jobId, out var channel) ? channel.Reader : null;
  }
  ```

- [ ] **Step 3: Build to verify**

  ```bash
  cd c:/apps/pickleball/PickleIQ
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj -q
  ```

  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/PickleIQ.Core/Interfaces/IJobStatusService.cs src/PickleIQ.Infrastructure/Services/JobStatusService.cs
  git commit -m "feat: add IJobStatusService and JobStatusService for instant status push"
  ```

---

## Task 2: Push status from VideoProcessingJob

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

Add `IJobStatusService jobStatusService` as a constructor parameter (after `coachingStreamService`). Call `jobStatusService.PushStatus(jobId, status)` immediately after each `job.Status = ...` + `db.SaveChangesAsync()` pair. Also push on failure. The full updated file:

- [ ] **Step 1: Update VideoProcessingJob.cs**

  Replace contents with:

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
      ICoachingStreamService coachingStreamService,
      IJobStatusService jobStatusService,
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
              jobStatusService.PushStatus(jobId, job.Status);

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
              jobStatusService.PushStatus(jobId, job.Status);

              logger.LogInformation("Job {JobId}: {Count} rally segments detected", jobId, segments.Count);

              // Steps 2a/2b/2c — run in parallel
              job.Status = VideoJobStatus.HighlightInProgress;
              await db.SaveChangesAsync();
              jobStatusService.PushStatus(jobId, job.Status);

              var (highlightPath, coachingFrames, totalMatchSeconds) = await RunParallelStepsAsync(job, segments);

              if (!string.IsNullOrEmpty(highlightPath))
                  job.HighlightFilePath = highlightPath;
              job.Status = VideoJobStatus.HighlightComplete;
              await db.SaveChangesAsync();
              jobStatusService.PushStatus(jobId, job.Status);

              logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);

              // Step 4: Coaching Report
              job.Status = VideoJobStatus.ReportInProgress;
              await db.SaveChangesAsync();
              jobStatusService.PushStatus(jobId, job.Status);

              var savedSegments = await db.RallySegments.Where(r => r.VideoJobId == jobId).ToListAsync();
              var durations = savedSegments.Select(s => s.EndSeconds - s.StartSeconds).ToList();

              var summary = new MatchSummary(
                  RallyCount: savedSegments.Count,
                  AverageRallySeconds: durations.Count > 0 ? durations.Average() : 0,
                  LongestRallySeconds: durations.Count > 0 ? durations.Max() : 0,
                  TotalMatchSeconds: totalMatchSeconds);

              coachingStreamService.CreateStream(jobId);
              try
              {
                  var report = await coachingEngine.GenerateReportHtmlAsync(
                      summary,
                      coachingFrames,
                      onChunk: chunk => coachingStreamService.WriteChunk(jobId, chunk));

                  db.CoachingReports.Add(new CoachingReport
                  {
                      Id = Guid.NewGuid(),
                      VideoJobId = jobId,
                      HtmlContent = report
                  });
              }
              finally
              {
                  coachingStreamService.CompleteStream(jobId);
              }

              job.Status = VideoJobStatus.ReportComplete;
              job.CompletedAt = DateTime.UtcNow;
              await db.SaveChangesAsync();
              jobStatusService.PushStatus(jobId, job.Status);

              logger.LogInformation("Job {JobId} completed successfully", jobId);
          }
          catch (Exception ex)
          {
              logger.LogError(ex, "Job {JobId} failed", jobId);
              job.Status = VideoJobStatus.Failed;
              job.ErrorMessage = ex.Message;
              await db.SaveChangesAsync();
              jobStatusService.PushStatus(jobId, job.Status);
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

- [ ] **Step 2: Build to verify**

  ```bash
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj -q
  ```

  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

  ```bash
  git add src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs
  git commit -m "feat: push job status updates via IJobStatusService on each transition"
  ```

---

## Task 3: Register singleton and update Results.razor

**Files:**
- Modify: `src/PickleIQ.Web/Program.cs`
- Modify: `src/PickleIQ.Web/Components/Pages/Results.razor`

- [ ] **Step 1: Register JobStatusService as singleton in Program.cs**

  After `builder.Services.AddSingleton<ICoachingStreamService, CoachingStreamService>();`, add:

  ```csharp
  builder.Services.AddSingleton<IJobStatusService, JobStatusService>();
  ```

- [ ] **Step 2: Update Results.razor**

  Read the current file first. Apply these changes:

  **Add @using and @inject** (after existing `@using PickleIQ.Core.Interfaces`):
  ```razor
  @inject IJobStatusService JobStatusService
  ```

  **Replace the `@code` block** with the updated version below. Key changes:
  - Remove `System.Threading.Timer? _pollTimer` field
  - Add `CancellationTokenSource _statusCts = new()`
  - Replace `OnInitializedAsync` polling timer with `ConsumeStatusAsync` background task
  - Replace `DisposeTimerAsync` with `Unsubscribe` call
  - Update `DisposeAsync` and `RetryAsync`
  - Add `ConsumeStatusAsync` method
  - Keep a 60-second fallback timer only (not 5-second polling)

  Replace the entire `@code { ... }` block with:

  ```csharp
  @code {
      [Parameter] public Guid JobId { get; set; }

      private static readonly MarkdownPipeline _markdigPipeline =
          new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

      private VideoJob? _job;
      private List<RallySegment> _segments = [];
      private CoachingReport? _report;
      private bool _retrying;
      private string _streamingReport = string.Empty;
      private CancellationTokenSource _streamCts = new();
      private CancellationTokenSource _statusCts = new();
      private System.Threading.Timer? _fallbackTimer;

      private string DownloadUrl => $"/download/{JobId}/highlights";

      protected override async Task OnInitializedAsync()
      {
          await RefreshAsync();

          if (_job?.Status != VideoJobStatus.ReportComplete && _job?.Status != VideoJobStatus.Failed)
          {
              JobStatusService.Subscribe(JobId);

              // 60-second fallback timer in case the page loads after the job already finished
              _fallbackTimer = new System.Threading.Timer(async _ =>
              {
                  await RefreshAsync();
                  await InvokeAsync(StateHasChanged);
              }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

              _ = Task.Run(() => ConsumeStatusAsync(_statusCts.Token));
              _ = Task.Run(() => ConsumeStreamAsync(_streamCts.Token));
          }
      }

      private async Task ConsumeStatusAsync(CancellationToken ct)
      {
          try
          {
              var reader = JobStatusService.TryGetReader(JobId);
              if (reader is null) return;

              await foreach (var status in reader.ReadAllAsync(ct).ConfigureAwait(false))
              {
                  await RefreshAsync();
                  await InvokeAsync(StateHasChanged);

                  if (status == VideoJobStatus.ReportComplete || status == VideoJobStatus.Failed)
                  {
                      JobStatusService.Unsubscribe(JobId);
                      await DisposeFallbackTimerAsync();
                      break;
                  }
              }
          }
          catch (OperationCanceledException)
          {
              // Expected on dispose or retry
          }
      }

      private async Task ConsumeStreamAsync(CancellationToken ct)
      {
          try
          {
              ChannelReader<string>? reader = null;
              for (var i = 0; i < 120 && !ct.IsCancellationRequested; i++)
              {
                  reader = CoachingStreamService.TryGetReader(JobId);
                  if (reader is not null) break;
                  await Task.Delay(500, ct).ConfigureAwait(false);
              }

              if (reader is null) return;

              await foreach (var chunk in reader.ReadAllAsync(ct).ConfigureAwait(false))
              {
                  await InvokeAsync(() => { _streamingReport += chunk; StateHasChanged(); });
              }
          }
          catch (OperationCanceledException)
          {
              // Expected on dispose or retry
          }
      }

      private async Task RefreshAsync()
      {
          _job = await Db.VideoJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == JobId);

          if (_job?.Status == VideoJobStatus.ReportComplete)
          {
              _segments = await Db.RallySegments.AsNoTracking()
                  .Where(s => s.VideoJobId == JobId)
                  .ToListAsync();

              _report = await Db.CoachingReports.AsNoTracking()
                  .FirstOrDefaultAsync(r => r.VideoJobId == JobId);

              LoadFileInfo(_job);
          }
      }

      private void LoadFileInfo(VideoJob job)
      {
          if (File.Exists(job.FilePath))
          {
              var info = new FileInfo(job.FilePath);
              _sourceSize = info.Length;
              _sourceDate = info.LastWriteTimeUtc;
          }
          if (!string.IsNullOrEmpty(job.HighlightFilePath) && File.Exists(job.HighlightFilePath))
          {
              var info = new FileInfo(job.HighlightFilePath);
              _highlightSize = info.Length;
              _highlightDate = info.LastWriteTimeUtc;
          }
      }

      private long? _sourceSize;
      private DateTime? _sourceDate;
      private long? _highlightSize;
      private DateTime? _highlightDate;

      private static string FormatSize(long? bytes) => bytes switch
      {
          null => "—",
          < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
          < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
          _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
      };

      private static string StatusMessage(VideoJobStatus status) => status switch
      {
          VideoJobStatus.Queued => "Queued — waiting to start...",
          VideoJobStatus.RallyDetectionInProgress => "Detecting rallies...",
          VideoJobStatus.RallyDetectionComplete => "Rallies detected — generating highlights...",
          VideoJobStatus.HighlightInProgress => "Creating highlight reel...",
          VideoJobStatus.HighlightComplete => "Highlights ready — generating coaching report...",
          VideoJobStatus.ReportInProgress => "Generating AI coaching report...",
          _ => "Processing..."
      };

      private static int ProgressPercent(VideoJobStatus status) => status switch
      {
          VideoJobStatus.Queued => 5,
          VideoJobStatus.RallyDetectionInProgress => 25,
          VideoJobStatus.RallyDetectionComplete => 50,
          VideoJobStatus.HighlightInProgress => 65,
          VideoJobStatus.HighlightComplete => 80,
          VideoJobStatus.ReportInProgress => 90,
          _ => 10
      };

      private async Task DisposeFallbackTimerAsync()
      {
          var t = _fallbackTimer;
          _fallbackTimer = null;
          if (t is not null)
              await t.DisposeAsync();
      }

      private async Task ConfirmReprocessAsync()
      {
          var confirmed = await DialogService.ShowMessageBoxAsync(
              "Reprocess Video",
              "This will delete the existing analysis results and reprocess the video from scratch.",
              yesText: "Reprocess", cancelText: "Cancel");
          if (confirmed != true) return;
          await RetryAsync();
      }

      private async Task RetryAsync()
      {
          JobStatusService.Unsubscribe(JobId);
          await DisposeFallbackTimerAsync();

          _streamingReport = string.Empty;
          _streamCts.Cancel();
          _streamCts.Dispose();
          _streamCts = new CancellationTokenSource();
          _statusCts.Cancel();
          _statusCts.Dispose();
          _statusCts = new CancellationTokenSource();

          _retrying = true;
          StateHasChanged();

          var job = await Db.VideoJobs.FindAsync(JobId);
          if (job is not null && (job.Status == VideoJobStatus.Failed || job.Status == VideoJobStatus.ReportComplete))
          {
              Db.RallySegments.RemoveRange(Db.RallySegments.Where(s => s.VideoJobId == JobId));
              var report = await Db.CoachingReports.FirstOrDefaultAsync(r => r.VideoJobId == JobId);
              if (report is not null) Db.CoachingReports.Remove(report);
              job.Status = VideoJobStatus.Queued;
              job.ErrorMessage = null;
              job.HighlightFilePath = null;
              job.CompletedAt = null;
              await Db.SaveChangesAsync();
              try
              {
                  JobClient.Enqueue<VideoProcessingJob>(j => j.ProcessAsync(JobId));
              }
              catch
              {
                  // Enqueue failed after commit — job is Queued but no worker dispatched. User can retry again.
              }
          }

          _retrying = false;
          await RefreshAsync();

          JobStatusService.Subscribe(JobId);
          _fallbackTimer = new System.Threading.Timer(async _ =>
          {
              await RefreshAsync();
              await InvokeAsync(StateHasChanged);
          }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
          _ = Task.Run(() => ConsumeStatusAsync(_statusCts.Token));
          _ = Task.Run(() => ConsumeStreamAsync(_streamCts.Token));
      }

      public async ValueTask DisposeAsync()
      {
          _statusCts.Cancel();
          _streamCts.Cancel();
          JobStatusService.Unsubscribe(JobId);
          if (_fallbackTimer is not null)
              await _fallbackTimer.DisposeAsync();
      }
  }
  ```

- [ ] **Step 3: Build to verify**

  ```bash
  cd c:/apps/pickleball/PickleIQ
  dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj -q 2>&1 | grep -E "error CS|succeeded|Error\(s\)"
  ```

  App may be running and lock the .exe — Infrastructure + Core build is sufficient to confirm correctness:
  ```bash
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj -q
  ```

- [ ] **Step 4: Commit**

  ```bash
  git add src/PickleIQ.Web/Program.cs src/PickleIQ.Web/Components/Pages/Results.razor
  git commit -m "feat: replace 5s polling timer with instant job status push via IJobStatusService"
  ```
