# Coaching Report Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stream Ollama coaching report tokens live to the Results page as they are generated, replacing the 60–90 second spinner with a word-by-word report build-up.

**Architecture:** A singleton `CoachingStreamService` (backed by `System.Threading.Channels`) bridges the Hangfire job and the Blazor circuit within the same process. `VideoProcessingJob` writes each chunk from Ollama into the channel; `Results.razor` reads from the channel via `await foreach` and calls `StateHasChanged()` per chunk. The existing DB save and polling continue unchanged as fallback for late page loads.

**Tech Stack:** .NET 10, `System.Threading.Channels`, Blazor Server (`InvokeAsync`/`StateHasChanged`), OllamaSharp streaming (`ChatAsync` IAsyncEnumerable), no new NuGet packages

---

## Files Changed

| Action | File |
|--------|------|
| Create | `src/PickleIQ.Core/Interfaces/ICoachingStreamService.cs` |
| Create | `src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs` |
| Modify | `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs` |
| Modify | `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs` |
| Modify | `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` |
| Modify | `src/PickleIQ.Web/Program.cs` |
| Modify | `src/PickleIQ.Web/Components/Pages/Results.razor` |

---

## Task 1: ICoachingStreamService interface + CoachingStreamService implementation

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/ICoachingStreamService.cs`
- Create: `src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs`

- [ ] **Step 1: Create the interface**

  Create `src/PickleIQ.Core/Interfaces/ICoachingStreamService.cs`:

  ```csharp
  using System.Threading.Channels;

  namespace PickleIQ.Core.Interfaces;

  public interface ICoachingStreamService
  {
      void CreateStream(Guid jobId);
      void WriteChunk(Guid jobId, string chunk);
      void CompleteStream(Guid jobId);
      ChannelReader<string>? TryGetReader(Guid jobId);
  }
  ```

- [ ] **Step 2: Create the implementation**

  Create `src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs`:

  ```csharp
  using System.Collections.Concurrent;
  using System.Threading.Channels;
  using PickleIQ.Core.Interfaces;

  namespace PickleIQ.Infrastructure.Services;

  public class CoachingStreamService : ICoachingStreamService
  {
      private readonly ConcurrentDictionary<Guid, Channel<string>> _channels = new();

      public void CreateStream(Guid jobId)
      {
          var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
          {
              SingleWriter = true,
              SingleReader = true,
              AllowSynchronousContinuations = false
          });
          _channels[jobId] = channel;
      }

      public void WriteChunk(Guid jobId, string chunk)
      {
          if (_channels.TryGetValue(jobId, out var channel))
              channel.Writer.TryWrite(chunk);
      }

      public void CompleteStream(Guid jobId)
      {
          if (_channels.TryRemove(jobId, out var channel))
              channel.Writer.TryComplete();
      }

      public ChannelReader<string>? TryGetReader(Guid jobId) =>
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
  git add src/PickleIQ.Core/Interfaces/ICoachingStreamService.cs src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs
  git commit -m "feat: add ICoachingStreamService and CoachingStreamService for streaming coaching chunks"
  ```

---

## Task 2: Add onChunk callback to ICoachingEngine and OllamaVisionCoachingEngine

**Files:**
- Modify: `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs`
- Modify: `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs`

- [ ] **Step 1: Add optional onChunk to ICoachingEngine**

  Replace the contents of `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs` with:

  ```csharp
  namespace PickleIQ.Core.Interfaces;

  public record MatchSummary(
      int RallyCount,
      double AverageRallySeconds,
      double LongestRallySeconds,
      double TotalMatchSeconds);

  public interface ICoachingEngine
  {
      Task<string> GenerateReportHtmlAsync(
          MatchSummary summary,
          IReadOnlyList<byte[]>? coachingFrames = null,
          Action<string>? onChunk = null,
          CancellationToken cancellationToken = default);
  }
  ```

- [ ] **Step 2: Update OllamaVisionCoachingEngine to call onChunk**

  In `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs`, update the method signature and the streaming loop.

  Change the method signature from:
  ```csharp
  public async Task<string> GenerateReportHtmlAsync(
      MatchSummary summary,
      IReadOnlyList<byte[]>? coachingFrames = null,
      CancellationToken cancellationToken = default)
  ```

  To:
  ```csharp
  public async Task<string> GenerateReportHtmlAsync(
      MatchSummary summary,
      IReadOnlyList<byte[]>? coachingFrames = null,
      Action<string>? onChunk = null,
      CancellationToken cancellationToken = default)
  ```

  Change the streaming loop from:
  ```csharp
  var sb = new System.Text.StringBuilder();
  await foreach (var chunk in client.ChatAsync(request, cancellationToken))
      sb.Append(chunk?.Message?.Content);

  return sb.ToString();
  ```

  To:
  ```csharp
  var sb = new System.Text.StringBuilder();
  await foreach (var chunk in client.ChatAsync(request, cancellationToken))
  {
      var content = chunk?.Message?.Content;
      if (!string.IsNullOrEmpty(content))
      {
          sb.Append(content);
          onChunk?.Invoke(content);
      }
  }

  return sb.ToString();
  ```

- [ ] **Step 3: Build to verify**

  ```bash
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj -q
  ```

  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

  ```bash
  git add src/PickleIQ.Core/Interfaces/ICoachingEngine.cs src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs
  git commit -m "feat: add onChunk callback to ICoachingEngine for streaming tokens"
  ```

---

## Task 3: Wire CoachingStreamService into VideoProcessingJob

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

- [ ] **Step 1: Add ICoachingStreamService to constructor and wire up channel**

  Replace the contents of `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` with:

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

              // Step 4: Coaching Report (with live streaming)
              job.Status = VideoJobStatus.ReportInProgress;
              await db.SaveChangesAsync();

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

- [ ] **Step 2: Build to verify**

  ```bash
  dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj -q
  ```

  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

  ```bash
  git add src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs
  git commit -m "feat: wire CoachingStreamService into VideoProcessingJob for live chunk delivery"
  ```

---

## Task 4: Register CoachingStreamService and update Results.razor

**Files:**
- Modify: `src/PickleIQ.Web/Program.cs`
- Modify: `src/PickleIQ.Web/Components/Pages/Results.razor`

- [ ] **Step 1: Register CoachingStreamService as singleton in Program.cs**

  In `src/PickleIQ.Web/Program.cs`, add after the existing `AddScoped` service registrations (after line `builder.Services.AddScoped<VideoProcessingJob>();`):

  ```csharp
  builder.Services.AddSingleton<ICoachingStreamService, CoachingStreamService>();
  ```

  Also add the using at the top of the file if needed — `CoachingStreamService` is in `PickleIQ.Infrastructure.Services` which is already referenced.

- [ ] **Step 2: Update Results.razor to stream live chunks**

  Replace the full contents of `src/PickleIQ.Web/Components/Pages/Results.razor` with:

  ```razor
  @page "/results/{JobId:guid}"
  @using Microsoft.EntityFrameworkCore
  @using PickleIQ.Core.Entities
  @using PickleIQ.Core.Interfaces
  @using PickleIQ.Infrastructure.Data
  @using Hangfire
  @using PickleIQ.Infrastructure.Jobs
  @using Markdig
  @inject AppDbContext Db
  @inject NavigationManager Navigation
  @inject IBackgroundJobClient JobClient
  @inject IDialogService DialogService
  @inject ICoachingStreamService CoachingStreamService
  @implements IAsyncDisposable

  <PageTitle>Results — PickleIQ</PageTitle>

  <MudText Typo="Typo.h4" Class="mb-4">Match Results</MudText>

  @if (_job is null)
  {
      <MudStack AlignItems="AlignItems.Center" Class="mt-8" Spacing="3">
          <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
          <MudText Color="Color.Secondary">Loading job status...</MudText>
      </MudStack>
  }
  else if (_job.Status == VideoJobStatus.Failed)
  {
      <MudAlert Severity="Severity.Error" Class="mb-4">
          <MudText Typo="Typo.h6">Processing failed</MudText>
          <MudText>@(_job.ErrorMessage ?? "An unexpected error occurred.")</MudText>
          <MudText Typo="Typo.body2" Color="Color.Secondary">Job ID: @JobId</MudText>
      </MudAlert>
      <MudStack Row="true" Spacing="2">
          <MudButton Variant="Variant.Filled" Color="Color.Warning" OnClick="RetryAsync" Disabled="_retrying">
              @(_retrying ? "Retrying…" : "Retry")
          </MudButton>
          <MudButton Href="/upload" Variant="Variant.Filled" Color="Color.Primary">Try another video</MudButton>
      </MudStack>
  }
  else if (_job.Status != VideoJobStatus.ReportComplete)
  {
      <MudStack AlignItems="AlignItems.Center" Class="mt-6" Spacing="4">
          @if (string.IsNullOrEmpty(_streamingReport))
          {
              <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
              <MudText Color="Color.Secondary">@StatusMessage(_job.Status)</MudText>
              <MudProgressLinear Color="Color.Primary" Striped="true" Rounded="true"
                                 Value="@ProgressPercent(_job.Status)" Class="my-2" Style="width:100%;max-width:400px;" />
          }
          else
          {
              <MudText Color="Color.Secondary" Class="align-self-start">Generating AI coaching report...</MudText>
              <MudPaper Elevation="1" Class="pa-4 mb-4" Style="width:100%">
                  <div class="coaching-report">@((MarkupString)Markdown.ToHtml(_streamingReport, _markdigPipeline))</div>
              </MudPaper>
          }
          <MudText Typo="Typo.body2" Color="Color.Secondary">Job ID: @JobId</MudText>
      </MudStack>
  }
  else
  {
      <!-- Rally Statistics -->
      <MudText Typo="Typo.h5" Class="mb-3">Rally Statistics</MudText>
      <MudGrid Spacing="3" Class="mb-4">
          <MudItem xs="12" sm="4">
              <MudPaper Elevation="2" Class="pa-4 text-center">
                  <MudText Typo="Typo.h3" Color="Color.Primary">@_segments.Count</MudText>
                  <MudText Typo="Typo.body2" Color="Color.Secondary">Rallies Detected</MudText>
              </MudPaper>
          </MudItem>
          <MudItem xs="12" sm="4">
              <MudPaper Elevation="2" Class="pa-4 text-center">
                  <MudText Typo="Typo.h3" Color="Color.Primary">
                      @(_segments.Count > 0 ? _segments.Average(s => s.DurationSeconds).ToString("F1") : "—")s
                  </MudText>
                  <MudText Typo="Typo.body2" Color="Color.Secondary">Avg Rally Length</MudText>
              </MudPaper>
          </MudItem>
          <MudItem xs="12" sm="4">
              <MudPaper Elevation="2" Class="pa-4 text-center">
                  <MudText Typo="Typo.h3" Color="Color.Primary">
                      @(_segments.Count > 0 ? _segments.Max(s => s.DurationSeconds).ToString("F1") : "—")s
                  </MudText>
                  <MudText Typo="Typo.body2" Color="Color.Secondary">Longest Rally</MudText>
              </MudPaper>
          </MudItem>
      </MudGrid>

      @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
      {
          <MudButton Href="@DownloadUrl" Variant="Variant.Filled" Color="Color.Success" Class="mb-4"
                     StartIcon="@Icons.Material.Filled.Download">
              Download Highlight Reel
          </MudButton>
      }

      <!-- File Info -->
      <MudText Typo="Typo.h5" Class="mb-2 mt-4">File Info</MudText>
      <MudSimpleTable Elevation="1" Class="mb-4">
          <thead>
              <tr>
                  <th>File</th>
                  <th>Path on Disk</th>
                  <th>Size</th>
                  <th>Date</th>
              </tr>
          </thead>
          <tbody>
              <tr>
                  <td><MudText Typo="Typo.body2" Color="Color.Secondary">Source Video</MudText></td>
                  <td><MudText Typo="Typo.body2" Style="font-family:monospace;word-break:break-all;">@_job.FilePath</MudText></td>
                  <td><MudText Typo="Typo.body2">@FormatSize(_sourceSize)</MudText></td>
                  <td><MudText Typo="Typo.body2">@_sourceDate?.ToLocalTime().ToString("MMM d, yyyy h:mm tt")</MudText></td>
              </tr>
              @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
              {
                  <tr>
                      <td><MudText Typo="Typo.body2" Color="Color.Secondary">Highlight Reel</MudText></td>
                      <td><MudText Typo="Typo.body2" Style="font-family:monospace;word-break:break-all;">@_job.HighlightFilePath</MudText></td>
                      <td><MudText Typo="Typo.body2">@FormatSize(_highlightSize)</MudText></td>
                      <td><MudText Typo="Typo.body2">@_highlightDate?.ToLocalTime().ToString("MMM d, yyyy h:mm tt")</MudText></td>
                  </tr>
              }
          </tbody>
      </MudSimpleTable>

      <!-- Coaching Report -->
      <MudText Typo="Typo.h5" Class="mb-2 mt-4">Coaching Report</MudText>
      @if (_report is not null)
      {
          <MudPaper Elevation="1" Class="pa-4 mb-4">
              <div class="coaching-report">@((MarkupString)Markdown.ToHtml(_report.HtmlContent, _markdigPipeline))</div>
          </MudPaper>
      }
      else
      {
          <MudText Color="Color.Secondary">Coaching report not available.</MudText>
      }

      <MudStack Row="true" Spacing="2" Class="mt-4" Wrap="Wrap.Wrap">
          <MudButton Href="/upload" Variant="Variant.Outlined" Color="Color.Primary">Analyze another video</MudButton>
          @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
          {
              <MudButton Href="@DownloadUrl" Variant="Variant.Filled" Color="Color.Success"
                         StartIcon="@Icons.Material.Filled.Download">
                  Download Highlight Reel
              </MudButton>
          }
          <MudButton Variant="Variant.Outlined" Color="Color.Warning" OnClick="ConfirmReprocessAsync" Disabled="_retrying">
              @(_retrying ? "Reprocessing…" : "Reprocess")
          </MudButton>
      </MudStack>
  }

  @code {
      [Parameter] public Guid JobId { get; set; }

      private static readonly MarkdownPipeline _markdigPipeline =
          new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

      private VideoJob? _job;
      private List<RallySegment> _segments = [];
      private CoachingReport? _report;
      private System.Threading.Timer? _pollTimer;
      private bool _retrying;
      private string _streamingReport = string.Empty;
      private CancellationTokenSource _streamCts = new();

      private long? _sourceSize;
      private DateTime? _sourceDate;
      private long? _highlightSize;
      private DateTime? _highlightDate;

      private string DownloadUrl => $"/download/{JobId}/highlights";

      protected override async Task OnInitializedAsync()
      {
          await RefreshAsync();

          if (_job?.Status != VideoJobStatus.ReportComplete && _job?.Status != VideoJobStatus.Failed)
          {
              _pollTimer = new System.Threading.Timer(async _ =>
              {
                  await RefreshAsync();
                  await InvokeAsync(StateHasChanged);
                  if (_job?.Status == VideoJobStatus.ReportComplete || _job?.Status == VideoJobStatus.Failed)
                      await DisposeTimerAsync();
              }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

              // Start streaming if report generation is in progress or about to start
              _ = Task.Run(() => ConsumeStreamAsync(_streamCts.Token));
          }
      }

      private async Task ConsumeStreamAsync(CancellationToken ct)
      {
          // Poll until a stream is available (job may not have started report gen yet)
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
              _streamingReport += chunk;
              await InvokeAsync(StateHasChanged);
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

      private async Task DisposeTimerAsync()
      {
          var t = _pollTimer;
          _pollTimer = null;
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
          await DisposeTimerAsync();
          _streamingReport = string.Empty;
          _streamCts.Cancel();
          _streamCts = new CancellationTokenSource();
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
          _pollTimer = new System.Threading.Timer(async _ =>
          {
              await RefreshAsync();
              await InvokeAsync(StateHasChanged);
              if (_job?.Status == VideoJobStatus.ReportComplete || _job?.Status == VideoJobStatus.Failed)
                  await DisposeTimerAsync();
          }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
          _ = Task.Run(() => ConsumeStreamAsync(_streamCts.Token));
      }

      public async ValueTask DisposeAsync()
      {
          _streamCts.Cancel();
          if (_pollTimer is not null)
              await _pollTimer.DisposeAsync();
      }
  }
  ```

- [ ] **Step 3: Build the full solution to verify**

  ```bash
  cd c:/apps/pickleball/PickleIQ
  dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj -q
  ```

  Expected: `Build succeeded. 0 Error(s)` (app may be locked — kill it first if needed)

- [ ] **Step 4: Commit**

  ```bash
  git add src/PickleIQ.Web/Program.cs src/PickleIQ.Web/Components/Pages/Results.razor
  git commit -m "feat: stream coaching report tokens live to Results page via CoachingStreamService"
  ```
