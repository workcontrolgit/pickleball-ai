# Pipeline Parallelization Design

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Parallelize the three independent post-detection pipeline steps (highlight generation, frame sampling, FFProbe duration) so they run concurrently instead of sequentially, reducing total processing time by 30–60 seconds per video.

**Architecture:** After rally detection completes and segments are saved to the DB, `Task.WhenAll` runs highlight generation, frame sampling, and FFProbe simultaneously. The coaching report step waits for all three results. No service implementations change — only `VideoProcessingJob.ProcessAsync` is modified.

**Tech Stack:** .NET 10, Hangfire, FFMpegCore, existing services unchanged

---

## Current Pipeline (sequential)

```
Rally Detection
  → Highlight Generation       (~20–40s)
  → Frame Sampling             (~10–20s)
  → FFProbe duration           (~1–2s)
  → Coaching Report
```

## New Pipeline (parallel steps 2a/2b/2c)

```
Rally Detection
  → Task.WhenAll(
      Highlight Generation,    (~20–40s)
      Frame Sampling,          (~10–20s)
      FFProbe duration         (~1–2s)
    )
  → Coaching Report
```

**Expected saving:** ~20–40 seconds per video (the longest of the three parallel tasks determines total time, rather than their sum).

---

## Changes

### `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

Only this file changes. Replace steps 2, 3 (highlight + frame sampling + FFProbe) with:

```csharp
// Steps 2a/2b/2c — run in parallel
job.Status = VideoJobStatus.HighlightInProgress;
await db.SaveChangesAsync();

var (highlightPath, coachingFrames, totalMatchSeconds) = await RunParallelStepsAsync(job, segments);

if (!string.IsNullOrEmpty(highlightPath))
    job.HighlightFilePath = highlightPath;
job.Status = VideoJobStatus.HighlightComplete;
await db.SaveChangesAsync();
```

With a private helper:

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

### Status progression

| Status | Meaning (unchanged) |
|---|---|
| `RallyDetectionInProgress` | YOLO running |
| `RallyDetectionComplete` | Segments saved |
| `HighlightInProgress` | All three parallel tasks running |
| `HighlightComplete` | All three parallel tasks done |
| `ReportInProgress` | Coaching LLM running |
| `ReportComplete` | Done |

`HighlightInProgress` and `HighlightComplete` now cover the combined parallel step — no status enum changes needed.

---

## Constraints and Safety

- **FFmpeg concurrency:** `HighlightGenerationService` (GPU encode via h264_nvenc) and `CoachingFrameSampler` (JPEG frame extraction, CPU decode) run as separate `ffmpeg` child processes simultaneously. This is safe — FFmpeg instances are fully independent. Peak VRAM usage will be slightly higher during overlap.
- **Error handling:** If any of the three tasks throws, `Task.WhenAll` propagates the first exception. The outer `catch` in `ProcessAsync` marks the job as Failed — same behaviour as today.
- **No service changes:** `HighlightGenerationService`, `CoachingFrameSampler`, and `FFProbe` are not modified.

---

## Out of Scope

- Scene-change-based frame sampling (future sub-project)
- YOLO batch inference (future sub-project)
- UI streaming of coaching report (separate sub-project)
- DB polling optimisation (separate sub-project)
