# Qwen2-VL Vision Coaching Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the text-only nemotron-mini coaching engine with Qwen2-VL 7B running locally via Ollama, enabling visual coaching analysis of player court positioning, paddle grip, footwork, and partner coordination from sampled rally frames.

**Architecture:** A new `CoachingFrameSampler` extracts 2–3 JPEG frames per rally (top 3 longest, max 9 frames) at 1280px wide using FFMpegCore. Frames are stored as byte arrays in an updated `MatchSummary`. `QwenVisionCoachingEngine` sends them as base64 images to Ollama's multimodal API with an explicit `num_ctx` of 32768.

**Tech Stack:** .NET 10, Blazor Server, OllamaSharp 5.4.25, FFMpegCore 5.4.0, Qwen2-VL 7B via Ollama

---

## File Map

| Action | File |
|---|---|
| Modify | `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs` |
| Create | `src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs` |
| Create | `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs` |
| Create | `src/PickleIQ.Infrastructure/AI/QwenVisionCoachingEngine.cs` |
| Delete | `src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs` |
| Modify | `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` |
| Modify | `src/PickleIQ.Web/Program.cs` |
| Modify | `src/PickleIQ.Web/appsettings.json` |
| Modify | `src/PickleIQ.Web/appsettings.Development.json` |

---

## Task 1: Update MatchSummary and ICoachingFrameSampler Interface

`MatchSummary` is currently defined in `ICoachingEngine.cs`. We add `CoachingFrames` and create the sampler interface. We also patch `VideoProcessingJob` temporarily to compile (Task 4 does the full wiring).

**Files:**
- Modify: `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs`
- Create: `src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs`
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` (minimal compile fix)

- [ ] **Step 1: Update ICoachingEngine.cs — add CoachingFrames to MatchSummary**

Replace the entire contents of `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs`:

```csharp
namespace PickleIQ.Core.Interfaces;

public record MatchSummary(
    int RallyCount,
    double AverageRallySeconds,
    double LongestRallySeconds,
    double TotalMatchSeconds,
    IReadOnlyList<byte[]> CoachingFrames);

public interface ICoachingEngine
{
    Task<string> GenerateReportHtmlAsync(MatchSummary summary, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Fix VideoProcessingJob to compile (temporary — Task 4 completes this)**

In `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`, find the `MatchSummary` constructor call (around line 70) and add the empty frames argument:

```csharp
var summary = new MatchSummary(
    RallyCount: savedSegments.Count,
    AverageRallySeconds: durations.Count > 0 ? durations.Average() : 0,
    LongestRallySeconds: durations.Count > 0 ? durations.Max() : 0,
    TotalMatchSeconds: 0,
    CoachingFrames: Array.Empty<byte[]>());
```

- [ ] **Step 3: Create ICoachingFrameSampler.cs**

Create `src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs`:

```csharp
namespace PickleIQ.Core.Interfaces;

public interface ICoachingFrameSampler
{
    Task<IReadOnlyList<byte[]>> SampleAsync(
        string videoPath,
        IList<(double StartSeconds, double EndSeconds)> rallies,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Verify the solution builds**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/PickleIQ.Core/Interfaces/ICoachingEngine.cs \
        src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs \
        src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs
git commit -m "feat: add CoachingFrames to MatchSummary and ICoachingFrameSampler interface"
```

---

## Task 2: Implement CoachingFrameSampler

Extracts JPEG frames at 25%, 50%, 75% of each of the top 3 longest rallies using FFMpegCore.

**Files:**
- Create: `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs`

- [ ] **Step 1: Create CoachingFrameSampler.cs**

Create `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs`:

```csharp
using FFMpegCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.Services;

public class CoachingFrameSampler(
    IConfiguration configuration,
    ILogger<CoachingFrameSampler> logger) : ICoachingFrameSampler
{
    private const int MaxRallies = 3;

    public async Task<IReadOnlyList<byte[]>> SampleAsync(
        string videoPath,
        IList<(double StartSeconds, double EndSeconds)> rallies,
        CancellationToken cancellationToken = default)
    {
        var ffOptions = FFmpegLocator.GetOptions(configuration);
        var tempDir = Path.Combine(Path.GetTempPath(), $"pickleiq-coaching-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var topRallies = rallies
                .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
                .Take(MaxRallies)
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
                                .WithVideoFilters(f => f.Scale(1280, -2))
                                .ForceFormat("image2"))
                            .ProcessAsynchronously(true, ffOptions);

                        if (File.Exists(framePath))
                            frames.Add(await File.ReadAllBytesAsync(framePath, cancellationToken));
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
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs
git commit -m "feat: add CoachingFrameSampler — extracts rally frames for vision coaching"
```

---

## Task 3: Create QwenVisionCoachingEngine

Replaces `OllamaCoachingEngine`. Sends frames as base64 images with an explicit context window.

**Files:**
- Create: `src/PickleIQ.Infrastructure/AI/QwenVisionCoachingEngine.cs`
- Delete: `src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs`

- [ ] **Step 1: Create QwenVisionCoachingEngine.cs**

Create `src/PickleIQ.Infrastructure/AI/QwenVisionCoachingEngine.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.AI;

public class QwenVisionCoachingEngine(
    IConfiguration configuration,
    ILogger<QwenVisionCoachingEngine> logger) : ICoachingEngine
{
    public async Task<string> GenerateReportHtmlAsync(
        MatchSummary summary, CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["Ollama:Model"] ?? "qwen2-vl:7b";
        var contextWindow = long.TryParse(configuration["Ollama:ContextWindow"], out var cw) ? cw : 32768L;

        logger.LogInformation(
            "Generating vision coaching report via {Model} with {FrameCount} frames",
            model, summary.CoachingFrames.Count);

        try
        {
            var client = new OllamaApiClient(new Uri(endpoint));

            var message = new Message
            {
                Role = ChatRole.User,
                Content = BuildPrompt(summary),
                Images = summary.CoachingFrames.Count > 0
                    ? summary.CoachingFrames.Select(f => Convert.ToBase64String(f)).ToList()
                    : null
            };

            var request = new ChatRequest
            {
                Model = model,
                Messages = [message],
                Options = new RequestOptions { NumCtx = contextWindow },
                Stream = true
            };

            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in client.ChatAsync(request, cancellationToken))
                sb.Append(chunk?.Message?.Content);

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama unavailable — using fallback coaching report");
            return GenerateFallbackMarkdown(summary);
        }
    }

    private static string BuildPrompt(MatchSummary summary)
    {
        var frameSection = summary.CoachingFrames.Count > 0
            ? $"""
               You are given {summary.CoachingFrames.Count} frames sampled from the rallies. Analyse what you can see:
               - Court positioning — are players at the kitchen line, baseline, or transition zone?
               - Ready position — paddle up, athletic stance, weight forward between shots?
               - Footwork — split-step, shuffle steps, crossover footwork visible?
               - Paddle and grip — continental vs eastern, wrist position, paddle height?
               - Partner coordination — side-by-side, stacking, covering the middle?
               """
            : "No video frames were available. Base your coaching on the match statistics only.";

        return $"""
                You are a certified pickleball coach reviewing a recreational doubles match.

                Match data:
                - Rallies detected: {summary.RallyCount}
                - Average rally length: {summary.AverageRallySeconds:F1} seconds
                - Longest rally: {summary.LongestRallySeconds:F1} seconds
                - Total match duration: {summary.TotalMatchSeconds / 60:F0} minutes

                {frameSection}

                Write a coaching report in markdown with exactly these four sections:
                ## Strengths
                ## Areas for Improvement
                ## Recommended Drills
                ## Match Summary

                Use bullet points under each section. Keep tone encouraging and actionable. Be specific to pickleball.
                """;
    }

    private static string GenerateFallbackMarkdown(MatchSummary summary) =>
        $"""
         > AI coaching engine unavailable. Showing statistical summary.

         ## Match Statistics

         - Rallies detected: {summary.RallyCount}
         - Average rally length: {summary.AverageRallySeconds:F1} seconds
         - Longest rally: {summary.LongestRallySeconds:F1} seconds
         - Total match: {summary.TotalMatchSeconds / 60:F0} minutes

         Start Ollama locally with `ollama run qwen2-vl:7b` and reprocess to get AI coaching feedback.
         """;
}
```

- [ ] **Step 2: Delete the old engine**

```bash
rm src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PickleIQ.sln
```

Expected: Build succeeded, 0 errors. (Program.cs still registers `OllamaCoachingEngine` — Task 5 fixes that, but the type is gone so the build will fail. That's expected — continue to Task 5 immediately if you see this error.)

> **Note:** If the build fails because `Program.cs` references `OllamaCoachingEngine`, proceed directly to Task 5 Step 1 to fix the DI registration, then return here to verify.

- [ ] **Step 4: Commit**

```bash
git add src/PickleIQ.Infrastructure/AI/QwenVisionCoachingEngine.cs
git rm src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs
git commit -m "feat: add QwenVisionCoachingEngine, remove OllamaCoachingEngine"
```

---

## Task 4: Update VideoProcessingJob

Inject `ICoachingFrameSampler`, call it after rally detection, and pass real frames into `MatchSummary`.

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

- [ ] **Step 1: Replace VideoProcessingJob.cs entirely**

Replace the full contents of `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`:

```csharp
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

            // Step 2: Highlight Generation
            job.Status = VideoJobStatus.HighlightInProgress;
            await db.SaveChangesAsync();

            var highlightPath = await highlightGenerationService.GenerateAsync(jobId, job.FilePath);
            if (!string.IsNullOrEmpty(highlightPath))
                job.HighlightFilePath = highlightPath;
            job.Status = VideoJobStatus.HighlightComplete;
            await db.SaveChangesAsync();

            logger.LogInformation("Job {JobId}: highlight reel at {Path}", jobId, highlightPath);

            // Step 3: Sample coaching frames
            var coachingFrames = await frameSampler.SampleAsync(job.FilePath, segments);

            // Step 4: Coaching Report
            job.Status = VideoJobStatus.ReportInProgress;
            await db.SaveChangesAsync();

            var savedSegments = await db.RallySegments.Where(r => r.VideoJobId == jobId).ToListAsync();
            var durations = savedSegments.Select(s => s.EndSeconds - s.StartSeconds).ToList();

            var summary = new MatchSummary(
                RallyCount: savedSegments.Count,
                AverageRallySeconds: durations.Count > 0 ? durations.Average() : 0,
                LongestRallySeconds: durations.Count > 0 ? durations.Max() : 0,
                TotalMatchSeconds: 0,
                CoachingFrames: coachingFrames);

            var report = await coachingEngine.GenerateReportHtmlAsync(summary);

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
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.sln
```

Expected: Build succeeded, 0 errors. (May still fail on Program.cs if Task 5 not done — do Task 5 first if so.)

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs
git commit -m "feat: wire CoachingFrameSampler into VideoProcessingJob"
```

---

## Task 5: DI Registration and Configuration

Register new services, swap model config to qwen2-vl:7b, add ContextWindow setting.

**Files:**
- Modify: `src/PickleIQ.Web/Program.cs`
- Modify: `src/PickleIQ.Web/appsettings.json`
- Modify: `src/PickleIQ.Web/appsettings.Development.json`

- [ ] **Step 1: Update Program.cs DI registrations**

In `src/PickleIQ.Web/Program.cs`, find these two lines:

```csharp
builder.Services.AddScoped<ICoachingEngine, OllamaCoachingEngine>();
builder.Services.AddScoped<VideoProcessingJob>();
```

Replace with:

```csharp
builder.Services.AddScoped<ICoachingFrameSampler, CoachingFrameSampler>();
builder.Services.AddScoped<ICoachingEngine, QwenVisionCoachingEngine>();
builder.Services.AddScoped<VideoProcessingJob>();
```

Also update the using imports at the top — replace:

```csharp
using PickleIQ.Infrastructure.AI;
```

with:

```csharp
using PickleIQ.Infrastructure.AI;
using PickleIQ.Infrastructure.Services;
```

(If `PickleIQ.Infrastructure.Services` is already listed, skip adding it again.)

- [ ] **Step 2: Update appsettings.json — Ollama section**

In `src/PickleIQ.Web/appsettings.json`, replace:

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "nemotron-mini"
}
```

with:

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen2-vl:7b",
  "ContextWindow": 32768
}
```

- [ ] **Step 3: Update appsettings.Development.json — add Ollama override**

In `src/PickleIQ.Web/appsettings.Development.json`, add an Ollama section (preserving existing keys):

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "FFmpeg": {
    "BinaryFolder": "C:/Users/Fuji Nguyen/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-8.1.1-full_build/bin"
  },
  "YoloModel": {
    "Path": "C:/apps/pickleball/PickleIQ/src/PickleIQ.Infrastructure/Models/yolo11n.onnx"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "qwen2-vl:7b",
    "ContextWindow": 32768
  }
}
```

- [ ] **Step 4: Full build**

```bash
dotnet build src/PickleIQ.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/PickleIQ.Web/Program.cs \
        src/PickleIQ.Web/appsettings.json \
        src/PickleIQ.Web/appsettings.Development.json
git commit -m "feat: register QwenVisionCoachingEngine and CoachingFrameSampler, switch model to qwen2-vl:7b"
```

---

## Task 6: Pull qwen2-vl:7b and Integration Test

Ensure the model is available locally and smoke-test the full pipeline.

**Files:** None — verification only.

- [ ] **Step 1: Pull the model via Ollama CLI**

```bash
ollama pull qwen2-vl:7b
```

Expected: Model downloads (approx 4.4 GB). This may take several minutes depending on connection speed.

- [ ] **Step 2: Verify model is available**

```bash
ollama list
```

Expected: `qwen2-vl:7b` appears in the list.

- [ ] **Step 3: Start the app**

```bash
cd c:/apps/pickleball/PickleIQ/src/PickleIQ.Web
dotnet run
```

Expected: App starts on `https://localhost:7xxx` without errors.

- [ ] **Step 4: Reprocess an existing video**

1. Open the app in the browser
2. Go to My Videos (`/jobs`)
3. Click the kebab menu (⋮) on any completed job → **Reprocess**
4. Confirm the dialog
5. Navigate to Results (`/results/{jobId}`) and watch the status progress through: Rally Detection → Highlights → Report

- [ ] **Step 5: Verify coaching report has visual insights**

When the report completes, the coaching report should contain pickleball-specific observations referencing visible elements such as:
- Court positioning (kitchen line, baseline)
- Ready position or paddle height
- Footwork mentions

If the report only contains generic stats (no visual observations), check the app logs for:
- `"Sampled X coaching frames"` — confirms CoachingFrameSampler ran
- `"Generating vision coaching report via qwen2-vl:7b with X frames"` — confirms engine received frames

- [ ] **Step 6: Commit (if any minor fixes were needed)**

```bash
git add -p   # stage only intentional changes
git commit -m "fix: <description of any integration fix>"
```

---

## Self-Review Notes

**Spec coverage check:**
- ✅ MatchSummary updated with CoachingFrames — Task 1
- ✅ ICoachingFrameSampler interface — Task 1
- ✅ CoachingFrameSampler (top 3 rallies, 25/50/75%, 1280px) — Task 2
- ✅ QwenVisionCoachingEngine with base64 images and NumCtx — Task 3
- ✅ OllamaCoachingEngine deleted — Task 3
- ✅ VideoProcessingJob wired — Task 4
- ✅ Config: Model=qwen2-vl:7b, ContextWindow=32768 — Task 5
- ✅ Cloud swap path documented in design spec (no implementation needed now)
- ✅ Fallback to stats-only if frames empty — QwenVisionCoachingEngine.BuildPrompt

**Type consistency:**
- `IReadOnlyList<byte[]> CoachingFrames` used consistently in MatchSummary, ICoachingFrameSampler, VideoProcessingJob
- `ICoachingFrameSampler.SampleAsync` signature matches `CoachingFrameSampler` implementation
- `FFmpegLocator.GetOptions` used consistently with RallyDetectionService pattern
