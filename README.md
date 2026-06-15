# PickleIQ

AI-powered pickleball video analysis. Upload a match video and get back a highlight reel and personalized coaching report — no coach required.

## What It Does

1. **Rally detection** — YOLO person + ball detection identifies active rally segments across the match
2. **Highlight reel** — top segments concatenated into a ~60-second MP4
3. **Coaching report** — AI-generated markdown report with strengths, improvement areas, and drill recommendations (powered by Ollama + qwen3-vl running locally with GPU)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Web UI | Blazor Server (.NET 10) |
| API | ASP.NET Core Web API |
| Background jobs | Hangfire + SQL Server |
| Database | EF Core + SQL Server Express (LocalDB) |
| Video processing | FFMpegCore (wraps FFmpeg) |
| Person detection | YoloDotNet 4.2 + yolo26n ONNX model |
| AI coaching | OllamaSharp → Ollama (qwen3-vl:8b vision model) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (installed with Visual Studio or standalone)
- [FFmpeg](https://ffmpeg.org/download.html) — must be on `PATH` or configured via `FFmpeg:BinaryFolder` (see below)
- [Ollama](https://ollama.com) with `qwen3-vl:8b` pulled — requires an NVIDIA GPU with 8+ GB VRAM (falls back to a statistical summary if unavailable)
- NVIDIA GPU with 8+ GB VRAM (tested on 16 GB)

```bash
ollama pull qwen3-vl:8b
```

## YOLO Model

The YOLO model file is not committed. Export `yolo26n.onnx` (opset 17) and place it at:

```
src/PickleIQ.Infrastructure/Models/yolo26n.onnx
```

**Python export:**
```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo26n.pt').export(format='onnx', opset=17)"
copy yolo26n.onnx src\PickleIQ.Infrastructure\Models\
```

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
| `Coaching:Model` | `qwen3-vl:8b` | Vision model for coaching reports |
| `Coaching:ContextWindow` | `12288` | Token context window (see VRAM table below) |
| `YoloModel:Path` | `Models/yolo26n.onnx` | Path to ONNX model file (relative to app base) |
| `FFmpeg:BinaryFolder` | _(system PATH)_ | Explicit path to FFmpeg `bin` folder — set if `ffmpeg` is not on PATH |

## Vision Model — Context Window & VRAM

Coaching frames are extracted at 640px wide (3 frames × top 2 rallies = 6 frames per job). `qwen3-vl:8b` (Q4_K_M, 6.1 GB weights) is dramatically more memory-efficient than its predecessor — tested on 16 GB VRAM:

| Context window | Est. VRAM | Status |
|---------------|-----------|--------|
| 8,192 | ~8–9 GB | Works |
| 12,288 | ~9–10 GB | Works (default) |
| **16,384** | **~10–11 GB** | **Works — recommended on 16 GB** |
| 32,768 | ~11–12 GB | Works with ~4 GB headroom |

`Coaching:ContextWindow` defaults to `12288`. On a 16 GB GPU you can safely raise it to `16384` or `32768` for longer coaching reports.

> qwen3-vl natively supports a 256K-token context window. The practical limit on 16 GB VRAM is VRAM, not the model. GGML assertion errors in qwen3-vl are image-size driven (large/high-res frames), not context-size driven — keep frames at 640px wide to avoid them.

> The prompt tokens for 6 frames at 640px is ~1,850 tokens. At 16,384 context this leaves ~14,500 tokens for the coaching response.

## Hangfire Dashboard

Available at `/hangfire`. Shows job queue, retries, and history.

## License

Source code: MIT  
YOLO models: [AGPL-3.0](https://www.ultralytics.com/license) — commercial use requires a separate Ultralytics license.
