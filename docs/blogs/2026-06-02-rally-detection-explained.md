# How PickleIQ Detects and Counts Rallies

*Published: 2026-06-02 | Author: PickleIQ Team*

---

When you upload a pickleball match video to PickleIQ, the first thing the pipeline does is figure out where the rallies are. It does not watch the whole video in real time — it samples frames, runs an AI object detector on each one, and uses a simple grouping algorithm to stitch the active moments into rally segments.

This post walks through the entire detection pipeline with the actual code so you can understand exactly what a "rally" means to PickleIQ, why the count might differ from what you expected, and how to tune the behaviour.

---

## The Three-Step Pipeline

```
Video file
    │
    ▼
Step 1: Extract frames at 2fps (FFmpeg)
    │
    ▼
Step 2: Detect players in each frame (YOLO)
    │
    ▼
Step 3: Group active frames into rally segments
    │
    ▼
List of (StartSeconds, EndSeconds) tuples
```

---

## Step 1 — Frame Extraction

The entire video is never loaded into memory at once. Instead, FFmpeg samples one frame every 0.5 seconds (2 frames per second) and writes them as JPEG files to a temporary directory:

```csharp
private const double FrameRateFps = 2.0;

await FFMpegArguments
    .FromFileInput(videoPath)
    .OutputToFile(
        Path.Combine(outputDir, "frame-%05d.jpg"),
        overwrite: true,
        options => options
            .WithVideoFilters(f => f.Scale(640, -2))
            .WithFramerate(FrameRateFps)
            .ForceFormat("image2"))
    .ProcessAsynchronously(true, ffOptions);
```

A few things to note:

- **2fps is intentional.** Rallies last several seconds. Sampling every 0.5 seconds gives enough temporal resolution to detect when play starts and stops without processing hundreds of frames.
- **640px width.** Frames are scaled down to 640 pixels wide before YOLO sees them. YOLO does not need full 4K resolution to detect people — it just needs to see distinct shapes. Smaller frames mean faster inference.
- **`-2` height.** The `-2` tells FFmpeg to calculate the height automatically while ensuring it is an even number. YOLO requires even pixel dimensions.

For a typical 10-minute match video at 2fps, this produces around 1,200 frames — roughly 50–100 MB of temporary JPEG files, deleted immediately after processing.

---

## Step 2 — Player Detection with YOLO

Each frame is passed through a YOLO (You Only Look Once) object detection model. YOLO is a neural network trained to draw bounding boxes around objects it recognises — in this case, people.

```csharp
private const float PersonConfidenceThreshold = 0.4f;
private const int MinPlayersForActiveFrame = 2;

var detections = yolo.RunObjectDetection(bitmap, confidence: PersonConfidenceThreshold, iou: 0.5f);
var personCount = detections.Count(d => d.Label.Name == "person");

if (personCount >= MinPlayersForActiveFrame)
    activeTimestamps.Add(frameTimestamp);
```

The logic is deliberately simple: **if YOLO finds 2 or more people in the frame with at least 40% confidence, the frame is "active".**

This is the core assumption of the rally detection model: *a frame with 2+ visible players is probably a rally frame*. A frame with 0 or 1 players is probably a timeout, a replay, a scoreboard close-up, or dead time between points.

### GPU vs CPU for YOLO

The model tries to run YOLO on the GPU first using DirectML (Windows GPU acceleration), falling back to CPU if that fails:

```csharp
var useGpuYolo = bool.TryParse(configuration["Processing:UseGpuYolo"], out var gy) && gy;

yolo = new Yolo(new YoloOptions
{
    ExecutionProvider = new DirectMLExecutionProvider(modelPath)
});
// Falls back to CpuExecutionProvider if DirectML fails
```

On GPU, each frame takes a few milliseconds. On CPU, it can take 50–200ms per frame. For 1,200 frames, the difference is 6 seconds vs 4 minutes.

### Confidence and IoU

Two parameters control YOLO's sensitivity:

- **`confidence: 0.4f`** — YOLO only reports a detection if it is at least 40% confident it saw a person. Lower values = more detections, more false positives. Higher values = fewer detections, may miss partially visible players.
- **`iou: 0.5f`** — Intersection over Union. When two bounding boxes overlap by more than 50%, YOLO keeps only the higher-confidence one. This prevents double-counting one person as two.

---

## Step 3 — Grouping Frames into Rally Segments

Having a list of timestamps where players are visible is not the same as having a list of rallies. The raw list might look like this:

```
Active: 0.5s, 1.0s, 1.5s, 2.0s, 2.5s
Gap (scoreboard shot): 3.0s, 3.5s   ← no players
Active: 4.0s, 4.5s, 5.0s
Gap (timeout): 5.5s ... 8.5s
Active: 9.0s, 9.5s, 10.0s, 10.5s, 11.0s
```

The grouping algorithm walks this list and applies two rules:

```csharp
private const double GapToleranceSeconds = 1.0;
private const double MinRallySeconds = 3.0;
```

**Rule 1 — Gap tolerance: 1 second.**  
If two active frames are less than 1 second apart, they belong to the same rally. This handles brief moments where a player ducks below frame or a camera pan momentarily loses both players mid-rally.

**Rule 2 — Minimum rally length: 3 seconds.**  
A segment is only counted as a rally if it lasts at least 3 seconds. This filters out false positives — a player walking onto the court, a spectator crossing the frame, or a brief camera focus on players between points.

Here is the algorithm in full:

```csharp
var segments = new List<(double Start, double End)>();
var segStart = activeTimestamps[0];
var segEnd   = activeTimestamps[0];

for (int i = 1; i < activeTimestamps.Count; i++)
{
    var gap = activeTimestamps[i] - segEnd;

    if (gap <= GapToleranceSeconds)
    {
        // Still in the same rally — extend the end
        segEnd = activeTimestamps[i];
    }
    else
    {
        // Gap is too large — close the current segment and start a new one
        if (segEnd - segStart >= MinRallySeconds)
            segments.Add((segStart, segEnd));

        segStart = activeTimestamps[i];
        segEnd   = activeTimestamps[i];
    }
}

// Don't forget the last segment
if (segEnd - segStart >= MinRallySeconds)
    segments.Add((segStart, segEnd));
```

Applied to the example above:

| Segment | Duration | Counted? |
|---------|----------|----------|
| 0.5s – 2.5s | 2.0s | No (< 3s minimum) |
| 4.0s – 5.0s | 1.0s | No (< 3s minimum) |
| 9.0s – 11.0s | 2.0s | No (< 3s minimum) |

All three segments would be filtered out. In a real 10-minute match with longer rallies, segments typically run 5–20 seconds and pass the 3-second minimum easily.

---

## Why the Rally Count Might Surprise You

A few common reasons the count does not match your mental model:

**"It counted more rallies than I expected"**
- The camera might cut to spectators briefly, splitting one long rally into two segments
- A warm-up period at the start of the video where players are hitting back and forth is indistinguishable from a real rally to the detector

**"It counted fewer rallies than I expected"**
- Very short exchanges (serve + return + error = 1.5 seconds) fall below the 3-second minimum and are filtered out
- If only one player is in frame (the other is off-camera at the baseline), that stretch is not counted as active

**"The rally times don't match the scoreboard"**
- The detector measures *time with 2+ visible players*, not time from serve to point. Dead ball time where players are retrieving the ball or discussing a call can be included if both players remain visible.

---

## Configuration Reference

All tuning constants are hardcoded in `RallyDetectionService.cs`. To change behaviour, modify these values:

| Constant | Default | Effect of increasing |
|----------|---------|---------------------|
| `FrameRateFps` | `2.0` | More temporal precision, more frames to process |
| `MinRallySeconds` | `3.0` | Requires longer continuous play to count as a rally |
| `GapToleranceSeconds` | `1.0` | More lenient with camera drops mid-rally |
| `PersonConfidenceThreshold` | `0.4f` | Higher = stricter, fewer false positives |
| `MinPlayersForActiveFrame` | `2` | Raise to `3` for doubles if you want all 4 players visible |

GPU acceleration for YOLO is controlled via `appsettings.json`:

```json
"Processing": {
  "UseGpuYolo": true
}
```

---

## Data Flow Summary

```
VideoJob.FilePath
    │
    ▼ FFMpegCore (2fps, 640px)
Temp JPEG frames (~1,200 for a 10-min match)
    │
    ▼ YoloDotNet + yolo11n.onnx
List<double> activeTimestamps
  [0.5, 1.0, 1.5, ..., 587.0, 587.5, 588.0]
    │
    ▼ GroupIntoSegments()
List<(double Start, double End)>
  [(12.5, 24.0), (38.0, 51.5), (67.0, 82.0), ...]
    │
    ▼ Saved as RallySegment rows in SQL Server
    │
    ▼ RallyCount, AverageRallySeconds, LongestRallySeconds
       displayed on Results page and fed to coaching prompt
```

The temporary frame directory is deleted in a `finally` block immediately after processing — no disk space accumulates between jobs.
