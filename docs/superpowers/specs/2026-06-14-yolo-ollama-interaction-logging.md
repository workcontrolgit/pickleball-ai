# YOLO & Ollama Interaction Logging — Design Spec

**Date:** 2026-06-14
**Status:** Approved

## Overview

Add structured diagnostic logging to two services:

1. **RallyDetectionService** — log YOLO detection results per frame (all labels, not just persons)
2. **OllamaVisionCoachingEngine** — log prompt summary and response summary, gated by a feature flag

## YOLO Logging (`RallyDetectionService`)

### Where

`RunConsumerAsync` — immediately after `yolo.RunObjectDetection(...)`.

### What

One `LogDebug` line per frame containing:
- Worker ID
- Frame index and timestamp in seconds (derived from `index * (1.0 / FrameRateFps)`)
- All detected labels grouped by name with counts (e.g. `person×3, sports ball×1`)
- Whether the frame was counted as active (`ACTIVE` / `inactive`) and the `minPlayers` threshold

### Format

```
Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {labels} → ACTIVE
Consumer {WorkerId}: Frame {Index} (t={Timestamp:F1}s) — {labels} → inactive (min={MinPlayers})
```

If no detections at all: `— (none)`

### Rationale

- Always on at `Debug` level — zero cost in production with default minimum log level of `Information`
- Surfaces ball detections (`sports ball`) and any other COCO labels the model returns beyond persons
- No court class exists in COCO; absence is itself informative

## Ollama Logging (`OllamaVisionCoachingEngine`)

### Feature Flag

```json
"Coaching": {
  "LogInteraction": false
}
```

Default `false`. Set to `true` in development to inspect prompts and responses.

### Prompt Summary (logged before request)

One `LogInformation` line:
- Model name, endpoint, context window size
- Frame count
- Prompt character length
- First 200 characters of prompt text

```
Ollama request — model={Model} endpoint={Endpoint} ctx={ContextWindow} frames={FrameCount} prompt={Length} chars | "{First200}"
```

### Response Summary (logged after streaming completes)

One `LogInformation` line:
- Total response character length
- Elapsed time in seconds
- First 200 characters of response text

```
Ollama response — {Length} chars in {Elapsed:F1}s | "{First200}"
```

### Timing

Start a `Stopwatch` before `client.ChatAsync(...)` and stop it after the `await foreach` loop completes.

### Log Level

`Information` — acceptable because the flag gates the output; no need to push to `Debug`.

## Configuration Changes

`src/PickleIQ.Web/appsettings.json` — add `LogInteraction` key under existing `Coaching` section:

```json
"Coaching": {
  "LogInteraction": false
}
```

## Files Changed

| File | Change |
|------|--------|
| `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs` | Add per-frame `LogDebug` after `RunObjectDetection` |
| `src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs` | Add prompt/response `LogInformation` gated by flag; add `Stopwatch` |
| `src/PickleIQ.Web/appsettings.json` | Add `Coaching:LogInteraction: false` |

## Out of Scope

- No new classes or interfaces
- No changes to detection logic or confidence thresholds
- No logging of individual streaming chunks from Ollama
