# Qwen2-VL Vision Coaching Engine Design

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the text-only nemotron-mini coaching engine with Qwen2-VL 7B running locally via Ollama, enabling visual analysis of player court positioning, paddle grip, footwork, and partner coordination from sampled rally frames.

**Architecture:** A new `CoachingFrameSampler` extracts 2–3 JPEG frames per rally (top 3 longest rallies, capped at 9 frames total) at 1280px wide. These frames are added to `MatchSummary` as byte arrays. `QwenVisionCoachingEngine` sends them as base64 images alongside rally stats to Ollama's multimodal API with an explicit `num_ctx` of 32768. The `ICoachingEngine` interface is unchanged, keeping the cloud swap path open.

**Tech Stack:** .NET 10, OllamaSharp, FFMpegCore (frame extraction), Qwen2-VL 7B via Ollama, SkiaSharp (already present)

---

## Architecture

### Pipeline

```
Video
  → YOLO (4fps, 640px)            → rally segments (unchanged)
  → CoachingFrameSampler (NEW)    → up to 9 JPEG frames @ 1280px
  → MatchSummary (updated)        → stats + frame bytes
  → QwenVisionCoachingEngine      → multimodal Ollama prompt → markdown report
  → Results page (unchanged)      → rendered via Markdig
```

### File Map

| Action | File |
|---|---|
| Modify | `src/PickleIQ.Core/Entities/MatchSummary.cs` |
| Create | `src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs` |
| Create | `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs` |
| Replace | `src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs` → `QwenVisionCoachingEngine.cs` |
| Modify | `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs` |
| Modify | `src/PickleIQ.Web/Program.cs` |
| Modify | `src/PickleIQ.Web/appsettings.json` |
| Modify | `src/PickleIQ.Web/appsettings.Development.json` |

---

## Section 1: MatchSummary

Add `CoachingFrames` to carry sampled JPEG bytes from the job to the coaching engine.

**`src/PickleIQ.Core/Entities/MatchSummary.cs`:**

```csharp
public record MatchSummary(
    int RallyCount,
    double AverageRallySeconds,
    double LongestRallySeconds,
    double TotalMatchSeconds,
    IReadOnlyList<byte[]> CoachingFrames);  // JPEG bytes, max 9
```

All existing callers of `MatchSummary` must be updated to pass `CoachingFrames`. If frame sampling fails, pass `Array.Empty<byte[]>()` — the engine falls back to stats-only text.

---

## Section 2: ICoachingFrameSampler Interface

**`src/PickleIQ.Core/Interfaces/ICoachingFrameSampler.cs`:**

```csharp
public interface ICoachingFrameSampler
{
    Task<IReadOnlyList<byte[]>> SampleAsync(
        string videoPath,
        IList<(double StartSeconds, double EndSeconds)> rallies,
        CancellationToken cancellationToken = default);
}
```

---

## Section 3: CoachingFrameSampler

**`src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs`**

**Sampling strategy:**
1. Sort rallies by duration descending, take top 3 (longest = most play content)
2. For each selected rally, extract frames at 25%, 50%, 75% of rally duration
3. Extract each frame using FFmpeg `ss` seek + single frame output at 1280px wide
4. Return list of JPEG byte arrays (max 9)

**FFmpeg call per frame:**
```
ffmpeg -ss {timestamp} -i {videoPath} -vframes 1 -vf scale=1280:-2 -f image2 {outputPath}
```

**Error handling:** If any single frame extraction fails, log and skip — partial frames are acceptable. If all fail, return empty list.

---

## Section 4: QwenVisionCoachingEngine

**`src/PickleIQ.Infrastructure/AI/QwenVisionCoachingEngine.cs`**

Replaces `OllamaCoachingEngine`. Registered as `ICoachingEngine`.

**OllamaSharp multimodal call:**

```csharp
var message = new Message
{
    Role = ChatRole.User,
    Content = BuildPrompt(summary),
    Images = summary.CoachingFrames
                    .Select(f => Convert.ToBase64String(f))
                    .ToList()
};

var request = new ChatRequest
{
    Model = model,
    Messages = [message],
    Options = new RequestOptions
    {
        NumCtx = contextWindow   // from config, default 32768
    }
};
```

**Prompt (pickleball-specific):**

```
You are a certified pickleball coach reviewing a recreational doubles match.
You are given {frameCount} frames sampled from the rallies alongside match statistics.

Match data:
- Rallies detected: {RallyCount}
- Average rally length: {AverageRallySeconds:F1} seconds
- Longest rally: {LongestRallySeconds:F1} seconds
- Total match duration: {TotalMatchSeconds / 60:F0} minutes

Analyse what you can see in the frames:
- Court positioning — are players at the kitchen line, baseline, or transition zone?
- Ready position — paddle up, athletic stance, weight forward between shots?
- Footwork — split-step, shuffle steps, crossover footwork visible?
- Paddle and grip — continental vs eastern, wrist position, paddle height?
- Partner coordination — side-by-side, stacking, covering the middle?

Write a coaching report in markdown with exactly these four sections:
## Strengths
## Areas for Improvement
## Recommended Drills
## Match Summary

Use bullet points. Keep tone encouraging and actionable. Be specific to pickleball.
```

**Fallback:** If `summary.CoachingFrames` is empty, send a text-only prompt (same structure, drop the frame-analysis paragraph and `Images`). This preserves behaviour when frame sampling fails.

---

## Section 5: VideoProcessingJob Changes

After `DetectRalliesAsync`, call `ICoachingFrameSampler.SampleAsync` before building `MatchSummary`:

```csharp
var frames = await _frameSampler.SampleAsync(job.FilePath, segments, cancellationToken);

var summary = new MatchSummary(
    RallyCount: segments.Count,
    AverageRallySeconds: segments.Average(s => s.EndSeconds - s.StartSeconds),
    LongestRallySeconds: segments.Max(s => s.EndSeconds - s.StartSeconds),
    TotalMatchSeconds: totalDuration,
    CoachingFrames: frames);
```

`ICoachingFrameSampler` is injected via constructor (scoped).

---

## Section 6: Configuration

**`appsettings.json`:**
```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen2-vl:7b",
  "ContextWindow": 32768
}
```

**`appsettings.Development.json`** — same values (explicit, not relying on fallback defaults).

**`Program.cs` DI registration:**
```csharp
builder.Services.AddScoped<ICoachingFrameSampler, CoachingFrameSampler>();
builder.Services.AddScoped<ICoachingEngine, QwenVisionCoachingEngine>();
// Remove old: AddScoped<ICoachingEngine, OllamaCoachingEngine>()
```

---

## Section 7: Cloud Swap Path (future)

No code changes required in job, sampler, or UI. Only:

1. Implement `OpenAiCoachingEngine : ICoachingEngine` (or `GeminiCoachingEngine`)
2. Switch DI registration in `Program.cs` based on config:

```csharp
var provider = builder.Configuration["Ollama:Provider"] ?? "ollama";
if (provider == "openai")
    builder.Services.AddScoped<ICoachingEngine, OpenAiCoachingEngine>();
else
    builder.Services.AddScoped<ICoachingEngine, QwenVisionCoachingEngine>();
```

3. Add cloud API key to environment / Key Vault — no other changes.

---

## Out of Scope

- Pose estimation (MediaPipe, OpenPose) — not needed; Qwen2-VL handles visual reasoning
- Per-frame individual analysis — batch prompt is sufficient and cheaper
- UI changes — Results page already renders markdown via Markdig
- Frame storage — frames are in-memory byte arrays, not persisted to disk
