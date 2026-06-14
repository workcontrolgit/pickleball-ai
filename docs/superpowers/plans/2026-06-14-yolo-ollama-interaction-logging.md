# YOLO & Ollama Interaction Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-frame YOLO detection logging (always on at Debug) and Ollama prompt/response summary logging (gated by `Coaching:LogInteraction` config flag).

**Architecture:** Inline log statements in two existing services — no new files, no new interfaces. YOLO logs one Debug line per frame after `RunObjectDetection`. Ollama logs one Information line before the HTTP request and one after streaming completes, both gated by a boolean config key.

**Tech Stack:** .NET 10, `Microsoft.Extensions.Logging.ILogger<T>`, `System.Diagnostics.Stopwatch`, YoloDotNet, OllamaSharp

---

## Files Modified

| File | Change |
|------|--------|
| `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` | Add `LogDebug` per frame in `RunConsumerAsync` after `RunObjectDetection` |
| `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs` | Read `Coaching:LogInteraction` flag; add `Stopwatch`; log prompt summary before and response summary after `ChatAsync` |
| `src/PickleIQ.Web/appsettings.json` | Add `"LogInteraction": false` under `"Coaching"` |

---

## Task 1: Add per-frame YOLO detection logging

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` (lines 252–256, inside `RunConsumerAsync`)

> Note: These are observational log statements with no branching logic. Unit testing them would require mocking the YOLO model and logger — impractical without the `.onnx` file. Verify manually by running the app with `"Logging:LogLevel:Default": "Debug"` in `appsettings.Development.json`.

- [ ] **Step 1: Replace the detection block in `RunConsumerAsync`**

Find this block (around line 252):

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
if (personCount >= minPlayers)
    activeTimestamps.Add(index * (1.0 / FrameRateFps));
```

Replace with:

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
var isActive = personCount >= minPlayers;
if (isActive)
    activeTimestamps.Add(index * (1.0 / FrameRateFps));

if (logger.IsEnabled(LogLevel.Debug))
{
    var timestamp = index * (1.0 / FrameRateFps);
    var labelSummary = detections.Count > 0
        ? string.Join(", ", detections
            .GroupBy(d => d.Label.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}×{g.Count()}"))
        : "(none)";
    var status = isActive ? "ACTIVE" : $"inactive (min={minPlayers})";
    logger.LogDebug(
        "Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {Labels} → {Status}",
        workerId, index, timestamp, labelSummary, status);
}
```

- [ ] **Step 2: Verify the file builds**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git commit -m "feat: log YOLO per-frame detection results at Debug level"
```

---

## Task 2: Add Ollama prompt/response logging with feature flag

**Files:**
- Modify: `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs`

> Note: Ollama logging is gated by a flag. Verify manually by setting `Coaching:LogInteraction: true` in `appsettings.Development.json` and running a coaching job.

- [ ] **Step 1: Add `using System.Diagnostics;` at the top of the file**

The file currently has these usings:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
```

Add `using System.Diagnostics;` as the first line.

- [ ] **Step 2: Replace the body of `GenerateReportHtmlAsync`**

Find the existing method body starting at line 22:

```csharp
var endpoint = configuration["Coaching:Endpoint"] ?? "http://localhost:11434";
var model = configuration["Coaching:Model"] ?? "qwen3-vl:8b";
var contextWindow = int.TryParse(configuration["Coaching:ContextWindow"], out var cw) ? cw : 4096;

var frameCount = coachingFrames?.Count ?? 0;
logger.LogInformation(
    "Generating vision coaching report via {Model} with {FrameCount} frames",
    model, frameCount);
```

Replace from that line through the end of the `try` block's return statement with:

```csharp
var endpoint = configuration["Coaching:Endpoint"] ?? "http://localhost:11434";
var model = configuration["Coaching:Model"] ?? "qwen3-vl:8b";
var contextWindow = int.TryParse(configuration["Coaching:ContextWindow"], out var cw) ? cw : 4096;
var logInteraction = bool.TryParse(configuration["Coaching:LogInteraction"], out var li) && li;

var frameCount = coachingFrames?.Count ?? 0;
logger.LogInformation(
    "Generating vision coaching report via {Model} with {FrameCount} frames",
    model, frameCount);

try
{
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10), BaseAddress = new Uri(endpoint) };
    var client = new OllamaApiClient(httpClient, model, null);

    var prompt = BuildPrompt(summary, mode, frameCount);

    var message = new Message
    {
        Role = ChatRole.User,
        Content = prompt,
        Images = frameCount > 0
            ? coachingFrames!.Select(f => Convert.ToBase64String(f)).ToArray()
            : null
    };

    var request = new ChatRequest
    {
        Model = model,
        Messages = [message],
        Options = new RequestOptions { NumCtx = contextWindow },
        Stream = true
    };

    if (logInteraction)
    {
        var promptPreview = prompt.Length > 200 ? prompt[..200] : prompt;
        logger.LogInformation(
            "Ollama request — model={Model} endpoint={Endpoint} ctx={ContextWindow} frames={FrameCount} prompt={PromptLength} chars | \"{PromptPreview}\"",
            model, endpoint, contextWindow, frameCount, prompt.Length, promptPreview);
    }

    var sw = Stopwatch.StartNew();
    var sb = new System.Text.StringBuilder();
    await foreach (var chunk in client.ChatAsync(request, cancellationToken))
    {
        var content = chunk?.Message?.Content;
        if (!string.IsNullOrEmpty(content))
        {
            var cleaned = StripSpecialTokens(content);
            if (!string.IsNullOrEmpty(cleaned))
            {
                sb.Append(cleaned);
                onChunk?.Invoke(cleaned);
            }
        }
    }
    sw.Stop();

    if (logInteraction)
    {
        var response = sb.ToString();
        var responsePreview = response.Length > 200 ? response[..200] : response;
        logger.LogInformation(
            "Ollama response — {ResponseLength} chars in {ElapsedSeconds:F1}s | \"{ResponsePreview}\"",
            response.Length, sw.Elapsed.TotalSeconds, responsePreview);
    }

    return sb.ToString();
}
```

- [ ] **Step 3: Verify the file builds**

```bash
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs
git commit -m "feat: log Ollama prompt and response summary behind Coaching:LogInteraction flag"
```

---

## Task 3: Add `LogInteraction` key to appsettings.json

**Files:**
- Modify: `src/PickleIQ.Web/appsettings.json`

- [ ] **Step 1: Add `LogInteraction` under the `Coaching` section**

Find:

```json
"Coaching": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen3-vl:8b",
  "ContextWindow": 12288
},
```

Replace with:

```json
"Coaching": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen3-vl:8b",
  "ContextWindow": 12288,
  "LogInteraction": false
},
```

- [ ] **Step 2: Full solution build**

```bash
dotnet build src/PickleIQ.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/appsettings.json
git commit -m "config: add Coaching:LogInteraction flag (default false)"
```

---

## Manual Verification

To see YOLO debug logs, add to `src/PickleIQ.Web/appsettings.Development.json`:

```json
"Logging": {
  "LogLevel": {
    "PickleIQ.Infrastructure.Services.RallyDetectionService": "Debug"
  }
}
```

Expected log output during a processing job:
```
[DBG] RallyDetectionService: Consumer 0: Frame 0 (t=0.0s) — person×3, sports ball×1 → ACTIVE
[DBG] RallyDetectionService: Consumer 1: Frame 1 (t=0.5s) — person×2 → ACTIVE
[DBG] RallyDetectionService: Consumer 0: Frame 2 (t=1.0s) — (none) → inactive (min=2)
```

To see Ollama interaction logs, set `"LogInteraction": true` in `appsettings.json` (or `appsettings.Development.json`). Expected:
```
[INF] OllamaVisionCoachingEngine: Ollama request — model=qwen3-vl:8b endpoint=http://localhost:11434 ctx=12288 frames=6 prompt=847 chars | "You are a certified pickleball coach..."
[INF] OllamaVisionCoachingEngine: Ollama response — 1243 chars in 12.4s | "## Strengths..."
```
