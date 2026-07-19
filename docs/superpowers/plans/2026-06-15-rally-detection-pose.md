# Rally Detection — Pose Estimation Shot Signal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a third active-frame signal — wrist-above-shoulder pose detection — so only frames where a player is mid-swing are counted active, eliminating false positives where ball+players are present but no shot is happening (e.g., serve tosses, resets).

**Architecture:** A second `Yolo` instance is initialised in `RunConsumerAsync` using `yolo26n-pose.onnx`. Per frame, pose inference runs alongside existing object detection. `IsFrameActive` is extended with a `anyPlayerSwinging` parameter. If `YoloModel:PosePath` is missing or the file doesn't exist, the consumer logs a warning and falls back to Phase 1 behaviour (ball + person only) with no crash. Phase 1 must be implemented first — this plan assumes `IsFrameActive` already exists.

**Tech Stack:** .NET 10, YoloDotNet 4.2 pose estimation (17 COCO keypoints), `yolo26n-pose.onnx` (exported by user), xUnit

**Prerequisite — export the pose model:**
```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo26n.pt').export(format='onnx', opset=17, task='pose')"
copy yolo26n-pose.onnx src\PickleIQ.Infrastructure\Models\
```

---

## Files Modified

| File | Change |
|------|--------|
| `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` | Extend `IsFrameActive`; add pose `Yolo` instance; add `AnyPlayerSwinging` helper; wire into `RunConsumerAsync` |
| `src/PickleIQ.Web/appsettings.json` | Add `YoloModel:PosePath` |
| `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs` | Add `IsFrameActive` (4-param) and `AnyPlayerSwinging` unit tests |

---

## COCO Pose Keypoint Indices (reference)

YoloDotNet returns keypoints as an indexed list per detected person. These are the relevant indices:

| Index | Keypoint |
|---|---|
| 5 | Left shoulder |
| 6 | Right shoulder |
| 9 | Left wrist |
| 10 | Right wrist |

Y increases downward in image coordinates. Wrist above shoulder = `wrist.Y < shoulder.Y`.

---

## Task 1: Extend `IsFrameActive` and `AnyPlayerSwinging` with tests

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`
- Modify: `src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs`

- [ ] **Step 1: Write failing tests for new `IsFrameActive` overload and `AnyPlayerSwinging`**

Add these test methods to `RallyDetectionServiceTests.cs` (append inside the class, after existing tests):

```csharp
// --- IsFrameActive with pose ---

[Fact]
public void IsFrameActive_WithPose_AllConditionsMet_ReturnsTrue()
{
    Assert.True(RallyDetectionService.IsFrameActive(
        personCount: 2, minPlayers: 2, ballDetected: true, anyPlayerSwinging: true));
}

[Fact]
public void IsFrameActive_WithPose_NoSwing_ReturnsFalse()
{
    Assert.False(RallyDetectionService.IsFrameActive(
        personCount: 2, minPlayers: 2, ballDetected: true, anyPlayerSwinging: false));
}

[Fact]
public void IsFrameActive_WithPose_NoBall_ReturnsFalse()
{
    Assert.False(RallyDetectionService.IsFrameActive(
        personCount: 2, minPlayers: 2, ballDetected: false, anyPlayerSwinging: true));
}

// --- AnyPlayerSwinging ---

[Fact]
public void AnyPlayerSwinging_WristAboveShoulder_ReturnsTrue()
{
    // Image coords: Y increases downward. Wrist above shoulder = smaller Y.
    // Person: left shoulder Y=200, left wrist Y=150 (wrist above shoulder)
    var keypoints = new (float X, float Y, float Confidence)[17];
    keypoints[5] = (100f, 200f, 0.9f);  // left shoulder
    keypoints[6] = (200f, 210f, 0.9f);  // right shoulder
    keypoints[9] = (100f, 150f, 0.9f);  // left wrist — above shoulder
    keypoints[10] = (200f, 230f, 0.9f); // right wrist — below shoulder

    Assert.True(RallyDetectionService.AnyPlayerSwinging(
        new[] { keypoints }));
}

[Fact]
public void AnyPlayerSwinging_WristsBelow_ReturnsFalse()
{
    var keypoints = new (float X, float Y, float Confidence)[17];
    keypoints[5] = (100f, 200f, 0.9f);  // left shoulder
    keypoints[6] = (200f, 210f, 0.9f);  // right shoulder
    keypoints[9] = (100f, 280f, 0.9f);  // left wrist — below shoulder
    keypoints[10] = (200f, 290f, 0.9f); // right wrist — below shoulder

    Assert.False(RallyDetectionService.AnyPlayerSwinging(
        new[] { keypoints }));
}

[Fact]
public void AnyPlayerSwinging_EmptyList_ReturnsFalse()
{
    Assert.False(RallyDetectionService.AnyPlayerSwinging(
        Array.Empty<(float X, float Y, float Confidence)[]>()));
}

[Fact]
public void AnyPlayerSwinging_LowConfidenceKeypoints_ReturnsFalse()
{
    // Keypoints with confidence below threshold should be ignored
    var keypoints = new (float X, float Y, float Confidence)[17];
    keypoints[5] = (100f, 200f, 0.1f);  // left shoulder — low confidence
    keypoints[9] = (100f, 150f, 0.1f);  // left wrist — low confidence

    Assert.False(RallyDetectionService.AnyPlayerSwinging(
        new[] { keypoints }));
}
```

- [ ] **Step 2: Run tests — expect failures**

```bash
cd c:/apps/pickleball/PickleIQ
dotnet test src/PickleIQ.Tests --filter "FullyQualifiedName~AnyPlayerSwinging|FullyQualifiedName~WithPose" -v
```

Expected: all 7 new tests fail.

- [ ] **Step 3: Add the 4-parameter `IsFrameActive` overload to `RallyDetectionService.cs`**

The existing 3-parameter `IsFrameActive` remains unchanged. Add an overload directly below it:

```csharp
internal static bool IsFrameActive(
    int personCount,
    int minPlayers,
    bool ballDetected,
    bool anyPlayerSwinging)
    => personCount >= minPlayers && ballDetected && anyPlayerSwinging;
```

- [ ] **Step 4: Add `AnyPlayerSwinging` static method to `RallyDetectionService.cs`**

Add this method below the `IsFrameActive` overload. The `minKeypointConfidence` constant filters out low-quality pose keypoints:

```csharp
private const float MinKeypointConfidence = 0.5f;

internal static bool AnyPlayerSwinging(
    IEnumerable<(float X, float Y, float Confidence)[]> personsKeypoints)
{
    foreach (var kps in personsKeypoints)
    {
        if (kps.Length < 11) continue;

        var leftShoulder  = kps[5];
        var rightShoulder = kps[6];
        var leftWrist     = kps[9];
        var rightWrist    = kps[10];

        // Left arm: wrist above shoulder (smaller Y) with sufficient confidence
        if (leftWrist.Confidence  >= MinKeypointConfidence &&
            leftShoulder.Confidence >= MinKeypointConfidence &&
            leftWrist.Y < leftShoulder.Y)
            return true;

        // Right arm
        if (rightWrist.Confidence  >= MinKeypointConfidence &&
            rightShoulder.Confidence >= MinKeypointConfidence &&
            rightWrist.Y < rightShoulder.Y)
            return true;
    }
    return false;
}
```

- [ ] **Step 5: Run tests — expect all pass**

```bash
dotnet test src/PickleIQ.Tests -v
```

Expected: all 17 tests pass (10 existing + 7 new).

- [ ] **Step 6: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git add src/PickleIQ.Tests/Services/RallyDetectionServiceTests.cs
git commit -m "feat: add IsFrameActive(pose) overload and AnyPlayerSwinging with unit tests"
```

---

## Task 2: Add pose model config and wire into `RunConsumerAsync`

**Files:**
- Modify: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`
- Modify: `src/PickleIQ.Web/appsettings.json`

- [ ] **Step 1: Add `PosePath` to `appsettings.json`**

Open `src/PickleIQ.Web/appsettings.json`. Find the `YoloModel` section:

```json
"YoloModel": {
  "Path": "Models/yolo26n.onnx",
  "BallConfidenceThreshold": 0.25,
  "PosePath": "Models/yolo26n-pose.onnx"
},
```

- [ ] **Step 2: Add pose `Yolo` initialisation at the top of `RunConsumerAsync`**

In `RallyDetectionService.cs`, find `RunConsumerAsync`. After the block that initialises the object detection `yolo` instance (around line 233), add pose model initialisation:

```csharp
// Pose model — optional, graceful fallback if not configured or file missing
var posePath = configuration["YoloModel:PosePath"];
Yolo? poseYolo = null;
if (!string.IsNullOrEmpty(posePath) && File.Exists(posePath))
{
    try
    {
        poseYolo = new Yolo(new YoloOptions
        {
            ExecutionProvider = new CpuExecutionProvider(posePath)
        });
        logger.LogInformation("Consumer {WorkerId}: Pose model loaded from {PosePath}", workerId, posePath);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Consumer {WorkerId}: Failed to load pose model — falling back to ball+person only", workerId);
    }
}
else if (!string.IsNullOrEmpty(posePath))
{
    logger.LogWarning("Consumer {WorkerId}: Pose model not found at {PosePath} — falling back to ball+person only", workerId, posePath);
}
```

- [ ] **Step 3: Wrap `poseYolo` in `using` and update the detection block**

The `using (yolo)` block wraps the consumer loop. Extend it to also dispose `poseYolo`. Replace:

```csharp
using (yolo)
{
```

With:

```csharp
using (yolo)
using (poseYolo)
{
```

- [ ] **Step 4: Add pose inference inside the per-frame try block**

Find the detection block inside the `await foreach` loop (after the `IsFrameActive` call from Phase 1). Replace:

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
var ballDetected = detections.Any(d =>
    d.Label.Name == "sports ball" && d.Confidence >= ballConfidenceThreshold);
var isActive = IsFrameActive(personCount, minPlayers, ballDetected);
```

With:

```csharp
var detections = yolo.RunObjectDetection(
    frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");
var ballDetected = detections.Any(d =>
    d.Label.Name == "sports ball" && d.Confidence >= ballConfidenceThreshold);

bool isActive;
if (poseYolo is not null)
{
    var poses = poseYolo.RunPoseEstimation(frame, confidence: PersonConfidenceThreshold, iou: 0.5f);
    var personsKeypoints = poses.Select(p => p.KeyPoints
        .Select(kp => (kp.X, kp.Y, kp.Confidence))
        .ToArray())
        .ToArray();
    var anySwinging = AnyPlayerSwinging(personsKeypoints);
    isActive = IsFrameActive(personCount, minPlayers, ballDetected, anySwinging);
}
else
{
    isActive = IsFrameActive(personCount, minPlayers, ballDetected);
}
```

- [ ] **Step 5: Update the Debug log to include swing status**

Find the `var status = ...` line inside the `if (logger.IsEnabled(LogLevel.Debug))` block. Replace:

```csharp
var status = isActive ? "ACTIVE" : personCount < minPlayers
    ? $"inactive (persons={personCount} min={minPlayers})"
    : "inactive (no ball)";
```

With:

```csharp
var status = isActive
    ? "ACTIVE"
    : personCount < minPlayers
        ? $"inactive (persons={personCount} min={minPlayers})"
        : !ballDetected
            ? "inactive (no ball)"
            : "inactive (no swing)";
```

- [ ] **Step 6: Build Infrastructure project**

```bash
dotnet build src/PickleIQ.Infrastructure/PickleIQ.Infrastructure.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Run all tests**

```bash
dotnet test src/PickleIQ.Tests -v
```

Expected: all 17 tests pass.

- [ ] **Step 8: Full solution build**

```bash
dotnet build src/PickleIQ.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs
git add src/PickleIQ.Web/appsettings.json
git commit -m "feat: add pose estimation shot detection to rally active frame signal"
```

---

## Manual Verification

With `yolo26n-pose.onnx` in place and Debug logging enabled:

```
[INF] RallyDetectionService: Consumer 0: Pose model loaded from Models/yolo26n-pose.onnx
[DBG] RallyDetectionService: Consumer 0: Frame 12 (t=6.0s) — person×4, sports ball×1 | ball✓ → ACTIVE
[DBG] RallyDetectionService: Consumer 1: Frame 13 (t=6.5s) — person×4 | ball✓ → inactive (no swing)
[DBG] RallyDetectionService: Consumer 0: Frame 14 (t=7.0s) — person×4 | no ball → inactive (no ball)
```

Without `yolo26n-pose.onnx` (fallback mode):

```
[WRN] RallyDetectionService: Consumer 0: Pose model not found at Models/yolo26n-pose.onnx — falling back to ball+person only
```

The pipeline continues normally — same behaviour as Phase 1.
