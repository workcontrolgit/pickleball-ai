# Rally Detection — Ball + Pose Signal Upgrade

**Date:** 2026-06-15  
**Status:** Approved  

## Problem

`RallyDetectionService` uses person count alone to determine whether a frame is active. Players are visible throughout the entire match (between points, during serve reset, walking back) so the 1-second `GapToleranceSeconds` never fires, causing the entire match to collapse into a single rally segment.

---

## Phase 1: Sports Ball + Person Composite Signal

### Goal

Require both a visible pickleball AND sufficient players before a frame is counted active. The ball disappears between points (pocket, floor, serve toss) — this gap naturally separates individual rallies.

### Active Frame Condition

```
isActive = personCount >= minPlayers AND ballDetected
```

Where `ballDetected` = any detection with `Label.Name == "sports ball"` and `Confidence >= BallConfidenceThreshold`.

### Changes

**`RallyDetectionService.cs` — `RunConsumerAsync` only:**
- Read `YoloModel:BallConfidenceThreshold` from config (default `0.25f`)
- After computing `personCount` from existing `detections`, add:
  ```csharp
  var ballDetected = detections.Any(d =>
      d.Label.Name == "sports ball" && d.Confidence >= ballConfidenceThreshold);
  var isActive = personCount >= minPlayers && ballDetected;
  ```
- Extend the existing Debug log line to include ball status:
  ```
  Consumer 0: Frame 42 (t=21.0s) — person×4, sports ball×1 → ACTIVE
  Consumer 0: Frame 43 (t=21.5s) — person×4 → inactive (no ball)
  ```

**`src/PickleIQ.Web/appsettings.json` — `YoloModel` section:**
```json
"YoloModel": {
  "Path": "Models/yolo26n.onnx",
  "BallConfidenceThreshold": 0.25
}
```

### No new files. No interface changes. No migration.

### Tuning guidance

`BallConfidenceThreshold` defaults to `0.25` — lower than person threshold (`0.4`) because a small fast-moving pickleball has inherently lower YOLO confidence than a stationary human. If too many rallies are missed (ball not detected mid-rally), lower it. If non-rally frames are triggering (false ball detections), raise it.

---

## Phase 2: Pose Estimation — Wrist Elevation Shot Signal

### Goal

Add a third signal: a player's wrist raised above their shoulder indicates a shot in progress. This distinguishes active play (arms moving, swinging) from standing/walking (arms at sides).

### Prerequisites

Export `yolo26n-pose.onnx` from Ultralytics:
```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo26n.pt').export(format='onnx', opset=17, task='pose')"
```
Place at the path configured by `YoloModel:PosePath`.

### Active Frame Condition

```
isActive = personCount >= minPlayers AND ballDetected AND anyPlayerSwinging
```

Where `anyPlayerSwinging` = any detected person has wrist keypoint Y < shoulder keypoint Y (image coordinates — Y increases downward, so wrist above shoulder means smaller Y value).

**COCO keypoint indices used:**
| Index | Keypoint |
|---|---|
| 5 | Left shoulder |
| 6 | Right shoulder |
| 9 | Left wrist |
| 10 | Right wrist |

### Changes

**`RallyDetectionService.cs` — `RunConsumerAsync`:**
- Read `YoloModel:PosePath` from config
- On startup, instantiate a second `Yolo` instance for pose inference if `PosePath` is configured and the file exists
- Per frame: run pose inference on same `SKBitmap`, check wrist/shoulder Y coordinates
- Incorporate `anyPlayerSwinging` into `isActive`

**`src/PickleIQ.Web/appsettings.json`:**
```json
"YoloModel": {
  "Path": "Models/yolo26n.onnx",
  "BallConfidenceThreshold": 0.25,
  "PosePath": "Models/yolo26n-pose.onnx"
}
```

### Graceful degradation

If `PosePath` is missing from config or the file does not exist at startup, `RunConsumerAsync` logs a warning and falls back to Phase 1 behavior (ball + person only). No crash, no exception thrown.

### No cross-frame state. Shot detection is purely per-frame.

---

## What Does Not Change

- `FrameRateFps = 2.0` — unchanged
- `GapToleranceSeconds = 1.0` — may be further tunable but not in scope
- `MinRallySeconds = 3.0` — unchanged
- `GroupIntoSegments` algorithm — unchanged
- All interfaces (`IRallyDetectionService`, `ICoachingEngine`, etc.) — unchanged
- Database schema — unchanged

---

## Testing

Both phases are observable-only changes with no unit-testable branching (same model-dependency limitation as current YOLO tests). Verify manually:

1. Enable Debug logging for `RallyDetectionService` in `appsettings.Development.json`
2. Submit a match video with known rally count
3. Confirm per-frame log shows ball detections during rallies and absence between points
4. Confirm segment count matches expected rally count
