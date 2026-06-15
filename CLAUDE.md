# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Superpowers

At the start of every session, invoke the `superpowers:using-superpowers` skill via the Skill tool before taking any other action.

# OpenWolf

@.wolf/OPENWOLF.md

This project uses OpenWolf for context management. Read and follow .wolf/OPENWOLF.md every session. Check .wolf/cerebrum.md before generating code. Check .wolf/anatomy.md before reading files.

## Commands

```bash
# Restore dependencies
dotnet restore src/PickleIQ.slnx

# Run the app (Blazor UI + Hangfire worker in one process)
dotnet run --project src/PickleIQ.Web

# Run all tests
dotnet test src/PickleIQ.Tests

# Run a single test
dotnet test src/PickleIQ.Tests --filter "FullyQualifiedName~TestMethodName"

# Apply DB migrations
dotnet ef database update --project src/PickleIQ.Infrastructure --startup-project src/PickleIQ.Web

# Add a new migration
dotnet ef migrations add <MigrationName> --project src/PickleIQ.Infrastructure --startup-project src/PickleIQ.Web
```

## Architecture

**Single-process design:** `PickleIQ.Web` hosts both the Blazor Server UI and the Hangfire background worker. There is no separate API process.

**Video processing pipeline** (`VideoProcessingJob.ProcessAsync`):
1. Rally detection — YoloDotNet + yolo11n ONNX model scans frames for persons, produces `(start, end)` segments
2. Parallel: highlight reel generation (FFMpegCore) + frame sampling (for coaching) + video probe
3. Coaching report — sampled frames sent to Ollama (`qwen2.5vl:7b`) via OllamaSharp; streams chunks via `ICoachingStreamService`

**Real-time UI updates** use two singleton services:
- `IJobStatusService` — channel-based push of `VideoJobStatus` enum changes from background job → Blazor pages
- `ICoachingStreamService` — streams coaching report markdown chunks to the Results page as they arrive

**Layer boundaries:**
- `PickleIQ.Core` — entities (`VideoJob`, `RallySegment`, `CoachingReport`) and interfaces only; no infrastructure deps
- `PickleIQ.Infrastructure` — EF Core, services, Hangfire job, AI engines
- `PickleIQ.Web` — Blazor Server pages + DI wiring + download endpoint

**Key configuration** (override in `appsettings.Development.json`):
- `VideoStorage:BasePath` / `VideoStorage:HighlightsPath` — default `C:/temp/pickleiq/`
- `Coaching:ContextWindow` — set to `12288` (safe for 16 GB VRAM with qwen2.5vl:7b Q4_K_M); lower for less VRAM
- `YoloModel:Path` — relative path to `yolo11n.onnx`; file is not committed, must be downloaded manually
- `FFmpeg:BinaryFolder` — auto-detected from WinGet packages if omitted

**YOLO model:** `src/PickleIQ.Infrastructure/Models/yolo11n.onnx` (opset 17) must be placed manually — not committed to git.

## MudBlazor Notes (v9.5.0)

- `MudBottomNavigation` / `MudBottomNavigationItem` do not exist — use a fixed bottom `MudAppBar` with `Bottom="true"`
- `Color.Default` removed — use `Color.Inherit` or `Color.Surface`
- `MudFileUpload<IBrowserFile>`: use `CustomContent` (not `ActivatorContent`); context = the component instance; call `context.OpenFilePickerAsync()` from button's `OnClick`; `FilesChanged` is `EventCallback<IBrowserFile>`

## Coaching Frame Sizing

Do not send frames larger than 640px wide to Ollama. At 1280px each frame is ~1200 visual tokens; 6 frames exhaust a 4096 context window and the model outputs near-zero tokens. Use 640px frames (~300 tokens each) with `Coaching:ContextWindow` ≥ 8192.
