# PickleIQ

AI-powered pickleball video analysis. Upload a match video and get back a highlight reel and personalized coaching report — no coach required.

## What It Does

1. **Rally detection** — YOLO person detection identifies active rally segments across the match
2. **Highlight reel** — top segments concatenated into a ~60-second MP4
3. **Coaching report** — AI-generated markdown report with strengths, improvement areas, and drill recommendations (powered by Ollama + qwen2.5vl running locally with GPU)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Web UI | Blazor Server (.NET 10) |
| API | ASP.NET Core Web API |
| Background jobs | Hangfire + SQL Server |
| Database | EF Core + SQL Server Express (LocalDB) |
| Video processing | FFMpegCore (wraps FFmpeg) |
| Person detection | YoloDotNet 4.2 + yolo11n ONNX model |
| AI coaching | OllamaSharp → Ollama (qwen2.5vl:7b vision model) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (installed with Visual Studio or standalone)
- [FFmpeg](https://ffmpeg.org/download.html) — must be on `PATH` or configured via `FFmpeg:BinaryFolder` (see below)
- [Ollama](https://ollama.com) with `qwen2.5vl:7b` pulled — requires an NVIDIA GPU with 14+ GB VRAM (falls back to a statistical summary if unavailable)
- NVIDIA GPU with 14+ GB VRAM (tested on 16 GB) for vision inference

```bash
ollama pull qwen2.5vl:7b
```

## YOLO Model

The YOLO model file is not committed. Download `yolo11n.onnx` (opset 17) and place it at:

```
src/PickleIQ.Infrastructure/Models/yolo11n.onnx
```

**Option A — Python export (recommended):**
```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo11n.pt').export(format='onnx', opset=17)"
copy yolo11n.onnx src\PickleIQ.Infrastructure\Models\
```

**Option B — Direct download:**  
Download `yolo11n.onnx` from [ultralytics/assets releases](https://github.com/ultralytics/assets/releases).

> If the model file is missing, rally detection falls back gracefully and logs a warning — the pipeline still runs with zero segments detected.

## Getting Started

### 1. Clone and restore

```bash
git clone https://github.com/fuji-nguyen/pickleball-ai.git
cd pickleball-ai
dotnet restore src/PickleIQ.slnx
```

### 2. Apply database migrations

```bash
dotnet ef database update --project src/PickleIQ.Infrastructure --startup-project src/PickleIQ.Web
```

This creates the `PickleIQ` database in LocalDB with the required tables.

### 3. Configure paths (optional)

The defaults write videos and highlights to `C:/temp/pickleiq/` and expect FFmpeg on the system `PATH`. Override in `appsettings.Development.json`:

```json
{
  "VideoStorage": {
    "BasePath": "C:/your/path/videos",
    "HighlightsPath": "C:/your/path/highlights"
  },
  "FFmpeg": {
    "BinaryFolder": "C:/path/to/ffmpeg/bin"
  }
}
```

> **Installing FFmpeg on Windows:** Run `winget install Gyan.FFmpeg` in a terminal, then restart VS Code so the updated `PATH` takes effect. Alternatively set `FFmpeg:BinaryFolder` to the `bin` folder of a manual FFmpeg download.

### 4. Run

Open the project folder in VS Code (`File → Open Folder → pickleball-ai`), then open two integrated terminals (`Ctrl+`` → Split Terminal`) and run each project in its own pane:

**Terminal 1 — Blazor Web (UI + background worker)**
```bash
dotnet run --project src/PickleIQ.Web
```

Open `https://localhost:5001/upload` to upload a match video. Results appear at `https://localhost:5001/results/{jobId}`.

> **Tip:** Watch the terminal for log output — it shows each pipeline stage (rally detection → highlight generation → coaching report) as it runs.

The Hangfire dashboard is served by the web project at `/hangfire`. No separate API process is needed.

## Project Structure

```
src/
  PickleIQ.Core/           # Entities, interfaces, no infrastructure dependencies
  PickleIQ.Infrastructure/ # EF Core, services, Hangfire job, AI engine
  PickleIQ.Api/            # ASP.NET Core Web API (Hangfire dashboard)
  PickleIQ.Web/            # Blazor Server UI + download endpoint + Hangfire worker
docs/
  product/                 # Vision, PRD, roadmap
  architecture/            # Architecture decisions and diagrams
  risks/                   # Risk register
  superpowers/             # Design specs and implementation plans
```

## Configuration Reference

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:DefaultConnection` | LocalDB `PickleIQ` | SQL Server connection string |
| `VideoStorage:BasePath` | `C:/temp/pickleiq/videos` | Where uploaded videos are saved |
| `VideoStorage:HighlightsPath` | `C:/temp/pickleiq/highlights` | Where highlight reels are written |
| `Coaching:Endpoint` | `http://localhost:11434` | Ollama server URL |
| `Coaching:Model` | `qwen2.5vl:7b` | Vision model for coaching reports |
| `Coaching:ContextWindow` | `12288` | Token context window (see VRAM table below) |
| `YoloModel:Path` | `Models/yolo11n.onnx` | Path to ONNX model file (relative to app base) |
| `FFmpeg:BinaryFolder` | _(system PATH)_ | Explicit path to FFmpeg `bin` folder — set if `ffmpeg` is not on PATH |

## Vision Model — Context Window & VRAM

Coaching frames are extracted at 640px wide (3 frames × top 2 rallies = 6 frames per job). Tested on an NVIDIA GPU with 16 GB VRAM running `qwen2.5vl:7b` (Q4_K_M):

| Context window | VRAM used | Tokens generated | Status |
|---------------|-----------|-----------------|--------|
| 4,096 | ~14.5 GB | 2 (blank report) | Too small — images exhaust context |
| 8,192 | ~12.5 GB | ~415 | Works |
| **12,288** | **13.17 GB** | **~544** | **Recommended — most output, safe margin** |
| 16,384 | — | — | GGML assertion error (model architecture limit) |

`Coaching:ContextWindow` is set to `12288` by default. Lower it if you have less than 16 GB VRAM.

> The prompt tokens for 6 frames at 640px is ~1,850 tokens. At 12,288 context this leaves ~10,400 tokens for the coaching response.

## Hangfire Dashboard

Available at `/hangfire`. Shows job queue, retries, and history.

## License

Source code: MIT  
YOLO models: [AGPL-3.0](https://www.ultralytics.com/license) — commercial use requires a separate Ultralytics license.
