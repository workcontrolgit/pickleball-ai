# Rally Detection — Ball Signal + Smarter Frame Sampling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace person-only rally detection with a ball+person AND gate so individual rallies are correctly separated, and raise frame sampling from 3 to 10 rallies to give Ollama full-match coverage within the 32K context window.

**Architecture:** Two targeted changes — (1) `RunConsumerAsync` in `RallyDetectionService` filters the existing detection result for `sports ball` (COCO class 32) alongside person count; a new internal static method `IsFrameActive` makes the composite logic unit-testable. (2) `CoachingFrameSampler` replaces the hardcoded `MaxRallies = 3` constant with a config-read value defaulting to `10`. No new files, no interface changes.

**Tech Stack:** .NET 10, YoloDotNet 4.2 (object detection, yolo26n.onnx, already detects all 80 COCO classes), xUnit, FFMpegCore, `IConfiguration`

---

## Files Modified

| File | Change |
|------|--------|
| `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` | Extract `IsFrameActive` static method; add ball detection to `RunConsumerAsync` |
| `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs` | Replace hardcoded `MaxRallies = 3` with config-read value |
| `src/PickleIQ.Web/appsettings.json` | Add `YoloModel:BallConfidenceThreshold` and `Coaching:MaxRallies` |
| `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs` | Add `IsFrameActive` unit tests |

---

## Task 1: Extract `IsFrameActive` and write failing tests

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`
- Modify: `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs`

The active frame condition is currently inline in `RunConsumerAsync`. Extract it to a static internal method so it can be unit-tested without a YOLO model.

- [ ] **Step 1: Add `IsFrameActive` static method to `RallyDetectionService`**

Add this method to `RallyDetectionService.cs` just above `GroupIntoSegments`:

```csharp
internal static bool IsFrameActive(
    int personCount,
    int minPlayers,
    bool ballDetected)
    => personCount >= minPlayers && ballDetected;
```

- [ ] **Step 2: Write failing tests for `IsFrameActive`**

Replace the contents of `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs` with:

```csharp
using PickleIQ.Infrastructure.Services;
using Xunit;

namespace PickleIQ.Tests.Services;

public class RallyDetectionServiceTests
{
    // --- ComputeScaledHeight (existing) ---

    [Theory]
    [InlineData(1920, 1080, 640, 360)]
    [InlineData(1280, 720,  640, 360)]
    [InlineData(3840, 2160, 640, 360)]
    [InlineData(1080, 1920, 640, 1138)]
    [InlineData(1920, 1081, 640, 360)] // odd → rounded up to even
    public void ComputeScaledHeight_ReturnsEvenHeight(int w, int h, int targetW, int expectedH)
    {
        var result = RallyDetectionService.ComputeScaledHeight(w, h, targetW);
        Assert.Equal(expectedH, result);
        Assert.Equal(0, result % 2);
    }

    // --- IsFrameActive ---

    [Fact]
    public void IsFrameActive_BothConditionsMet_ReturnsTrue()
    {
        Assert.True(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: true));
    }

    [Fact]
    public void IsFrameActive_NoBall_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 4, minPlayers: 2, ballDetected: false));
    }

    [Fact]
    public void IsFrameActive_TooFewPlayers_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 2, ballDetected: true));
    }

    [Fact]
    public void IsFrameActive_NoPlayersNoBall_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 0, minPlayers: 2, ballDetected: false));
    }

    [Fact]
    public void IsFrameActive_SinglePlayerMode_BallRequired()
    {
        // FollowCam/Training use minPlayers=1
        Assert.True(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 1, ballDetected: true));
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 1, ballDetected: false));
    }
}
```

- [ ] **Step 3: Run tests — expect failures on `IsFrameActive` tests**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet test src/PickleIQ.Tests --filter "FullyQualifiedName~IsFrameActive" -v
```

Expected: all 5 `IsFrameActive` tests fail with `method not found` or similar.

- [ ] **Step 4: Add the `IsFrameActive` method to `RallyDetectionService.cs`**

Open `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`. Add this method directly above `GroupIntoSegments` (around line 85):

```csharp
internal static bool IsFrameActive(
    int personCount,
    int minPlayers,
    bool ballDetected)
    => personCount >= minPlayers && ballDetected;
```

- [ ] **Step 5: Run tests — expect all pass**

```bash
dotnet test src/PickleIQ.Tests -v
```

Expected: all 10 tests pass (5 existing `ComputeScaledHeight` + 5 new `IsFrameActive`).

- [ ] **Step 6: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git add src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs
git commit -m "feat: extract IsFrameActive static method with unit tests"
```

---

## Task 2: Wire ball detection into `RunConsumerAsync`

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` (around lines 202–290)
- Modify: `src/PickleIQ.Web/appsettings.json`

- [ ] **Step 1: Add `BallConfidenceThreshold` to `appsettings.json`**

Open `src/PickleIQ.Web/appsettings.json`. Find the `YoloModel` section and add the new key:

```json
"YoloModel": {
  "Path": "Models/yolo26n.onnx",
  "BallConfidenceThreshold": 0.25
},
```

- [ ] **Step 2: Update `RunConsumerAsync` to read threshold and detect ball**

In `RallyDetectionService.cs`, find `RunConsumerAsync`. The block starting around line 250:

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
var isActive = personCount >= minPlayers;
if (isActive)
    activeTimestamps.Add(index * (1.0 / FrameRateFps));
```

Replace with:

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
var ballDetected = detections.Any(d =>
    d.Label.Name == "sports ball" && d.Confidence >= ballConfidenceThreshold);
var isActive = IsFrameActive(personCount, minPlayers, ballDetected);
if (isActive)
    activeTimestamps.Add(index * (1.0 / FrameRateFps));
```

- [ ] **Step 3: Read `ballConfidenceThreshold` from config at the top of `RunConsumerAsync`**

Find where `modelPath` and `useGpu` are read at the top of `RunConsumerAsync` (around line 209):

```csharp
var modelPath = configuration["YoloModel:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "Models", "yolo26n.onnx");

var useGpu = workerId == 0
    && bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;
```

Add one line after `modelPath`:

```csharp
var modelPath = configuration["YoloModel:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "Models", "yolo26n.onnx");
var ballConfidenceThreshold = float.TryParse(
    configuration["YoloModel:BallConfidenceThreshold"], out var bt) ? bt : 0.25f;

var useGpu = workerId == 0
    && bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;
```

- [ ] **Step 4: Update the Debug log line to include ball status**

Find the existing `logger.LogDebug` call in `RunConsumerAsync`:

```csharp
logger.LogDebug(
    "Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {Labels} → {Status}",
    workerId, index, timestamp, labelSummary, status);
```

Replace with:

```csharp
var ballStatus = ballDetected ? "ball✓" : "no ball";
logger.LogDebug(
    "Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {Labels} | {BallStatus} → {Status}",
    workerId, index, timestamp, labelSummary, ballStatus, status);
```

Also update `status` to reflect the new composite condition:

```csharp
var status = isActive ? "ACTIVE" : personCount < minPlayers
    ? $"inactive (persons={personCount} min={minPlayers})"
    : "inactive (no ball)";
```

- [ ] **Step 5: Build Infrastructure project**

```bash
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Run all tests**

```bash
dotnet test src/PickleIQ.Tests -v
```

Expected: all 10 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git add src/PickleIQ.Web/appsettings.json
git commit -m "feat: require sports ball detection for active rally frames"
```

---

## Task 3: Make `MaxRallies` configurable in `CoachingFrameSampler`

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs`
- Modify: `src/PickleIQ.Web/appsettings.json`

- [ ] **Step 1: Add `MaxRallies` to `appsettings.json`**

Open `src/PickleIQ.Web/appsettings.json`. Find the `Coaching` section and add the new key:

```json
"Coaching": {
  "Endpoint": "http://localhost:11434",
  "Model": "qwen3-vl:8b",
  "ContextWindow": 32768,
  "LogInteraction": false,
  "MaxRallies": 10
},
```

- [ ] **Step 2: Replace hardcoded constant in `CoachingFrameSampler.cs`**

Open `src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs`.

Remove:
```csharp
private const int MaxRallies = 3;
```

In `SampleAsync`, replace:
```csharp
var topRallies = rallies
    .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
    .Take(MaxRallies)
    .ToList();
```

With:
```csharp
var maxRallies = int.TryParse(configuration["Coaching:MaxRallies"], out var mr) ? mr : 10;
var topRallies = rallies
    .OrderByDescending(r => r.EndSeconds - r.StartSeconds)
    .Take(maxRallies)
    .ToList();
```

- [ ] **Step 3: Full solution build**

```bash
dotnet build src/PickleIQ.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Run all tests**

```bash
dotnet test src/PickleIQ.Tests -v
```

Expected: all 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs
git add src/PickleIQ.Web/appsettings.json
git commit -m "feat: make MaxRallies configurable, default 10 for 30-frame Ollama coverage"
```

---

## Manual Verification

Enable Debug logging for `RallyDetectionService` in `appsettings.Development.json`:

```json
"Logging": {
  "LogLevel": {
    "PickleIQ.Infrastructure.Services.RallyDetectionService": "Debug"
  }
}
```

Submit a match video. Expected per-frame log output:

```
[DBG] RallyDetectionService: Consumer 0: Frame 12 (t=6.0s) — person×4, sports ball×1 | ball✓ → ACTIVE
[DBG] RallyDetectionService: Consumer 1: Frame 13 (t=6.5s) — person×4 | no ball → inactive (no ball)
[DBG] RallyDetectionService: Consumer 0: Frame 14 (t=7.0s) — (none) | no ball → inactive (persons=0 min=2)
```

Expected coaching log:
```
[INF] CoachingFrameSampler: Sampled 30 coaching frames from 10 rallies
```

If fewer than 10 rallies are detected, sampler takes all available — no error.
