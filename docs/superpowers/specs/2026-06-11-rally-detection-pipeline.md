# Rally Detection Pipeline — Design Spec

**Date:** 2026-06-11  
**Status:** Approved

## Problem

`RallyDetectionService.DetectActiveFrames` processes frames one at a time:

1. FFmpeg extracts **all** frames to a temp directory (blocks until complete)
2. YOLO then processes each frame sequentially in a `for` loop
3. GPU fires in tiny single-frame bursts, sitting idle between dispatches
4. CPU stalls on disk I/O between each frame read

For a 64-minute video at 2fps (7,680 frames), CPU and GPU utilization is low throughout.

## Solution

Replace the extract-then-detect serial approach with an in-memory producer-consumer pipeline:

- FFmpeg streams raw frames via stdout pipe — no temp files
- A producer task reads frames from the pipe and enqueues them
- N consumer tasks run YOLO inference in parallel, each owning their own `Yolo` instance
- YOLO starts on frame 1 while FFmpeg is still encoding frame 500

## Architecture

```
FFmpeg process (stdout: rawvideo rgb24)
  → FrameProducer task
  → Channel<(int index, SKBitmap frame)>  [bounded capacity: 64]
  → N FrameConsumer tasks (each owns a Yolo instance)
  → ConcurrentBag<double> activeTimestamps
  → sort → GroupIntoSegments (unchanged)
```

## Components

### RallyDetectionService.cs

Replace two methods:

| Old | New |
|---|---|
| `ExtractFramesAsync` (FFmpeg → disk) | Removed |
| `DetectActiveFrames` (sequential for loop) | `RunDetectionPipelineAsync` (producer-consumer) |

`RunDetectionPipelineAsync` steps:
1. Probe video dimensions with `FFProbe.AnalyseAsync`
2. Compute `scaledH = nearest even of (640 / W × H)`, `frameSize = 640 × scaledH × 3`
3. Launch FFmpeg process with `rawvideo -pix_fmt rgb24` output to stdout pipe
4. Start producer task: read `frameSize` bytes per iteration → decode `SKBitmap` → write to channel
5. Start N consumer tasks: each reads from channel → `yolo.RunObjectDetection` → add timestamp if `personCount >= minPlayers`
6. `await Task.WhenAll(producer, consumers...)` 
7. Sort `activeTimestamps` → pass to `GroupIntoSegments`

### appsettings.json

```json
"Processing": {
  "PipelineWorkers": 2
}
```

Default `2`. Increase on machines with more VRAM/cores.

## Frame Format

`rawvideo -pix_fmt rgb24` chosen over MJPEG pipe because:
- Frame boundaries are deterministic: exactly `frameSize` bytes each
- No JPEG stream parsing needed
- Memory cost: 64 frames × 640×scaledH×3 bytes ≈ 44 MB max buffered

## Channel

- Type: `Channel<(int Index, SKBitmap Frame)>`
- Bounded capacity: `64` (hardcoded — backpressures producer if consumers fall behind)
- Index carried through so timestamps can be computed as `index × (1.0 / FrameRateFps)`

## Worker Count

Each consumer creates its own `Yolo` instance (ONNX Runtime is not thread-safe across instances). Worker 0 tries DirectML (GPU); workers 1..N use CPU. This means with `PipelineWorkers: 2` you get 1 GPU worker + 1 CPU worker running in parallel.

## Error Handling

| Scenario | Behavior |
|---|---|
| FFmpeg exits non-zero | Producer completes channel with error; consumers drain remaining frames; exception thrown after all consumers finish |
| Consumer throws | `CancellationTokenSource` cancelled; all other consumers stop; exception propagated to caller |
| Frame decode returns null | Frame skipped (same as current behavior) |
| SKBitmap disposal | Each consumer disposes its own bitmaps immediately after inference |

## Temp Directory

Eliminated entirely. `DetectRalliesAsync` no longer creates or deletes a temp frames directory.

## Unchanged

- `GroupIntoSegments` — no changes
- `GapToleranceSeconds`, `MinRallySeconds`, `PersonConfidenceThreshold` — no changes
- All callers of `DetectRalliesAsync` — no signature change

## Expected Gains

| Metric | Before | After |
|---|---|---|
| GPU utilization | Low (single-frame bursts) | Higher (continuous, parallel) |
| Temp disk usage | ~1-2 GB per job | 0 |
| YOLO start time | After full FFmpeg extract | Immediately (frame 1) |
| Wall time (estimate) | Baseline | ~2-4× faster |
