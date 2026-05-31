# PickleIQ

AI-powered pickleball video analysis. Upload a match video and get back a highlight reel and personalized coaching report — no coach required.

## What It Does

1. **Rally detection** — YOLO person detection identifies active rally segments across the match
2. **Highlight reel** — top segments concatenated into a ~60-second MP4
3. **Coaching report** — AI-generated HTML report with strengths, improvement areas, and drill recommendations (powered by Ollama + nemotron-mini running locally)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Web UI | Blazor Server (.NET 10) |
| API | ASP.NET Core Web API |
| Background jobs | Hangfire + SQL Server |
| Database | EF Core + SQL Server Express (LocalDB) |
| Video processing | FFMpegCore (wraps FFmpeg) |
| Person detection | YoloDotNet 4.2 + yolo11n ONNX model |
| AI coaching | OllamaSharp → Ollama (nemotron-mini) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (installed with Visual Studio or standalone)
- [FFmpeg](https://ffmpeg.org/download.html) — must be on `PATH` or configured via `FFmpeg:BinaryFolder` (see below)
- [Ollama](https://ollama.com) with `nemotron-mini` pulled (optional — falls back to statistical summary if unavailable)

```bash
ollama pull nemotron-mini
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

**Terminal 2 — API (optional, Hangfire dashboard only)**
```bash
dotnet run --project src/PickleIQ.Api
```

Only needed if you want to inspect the job queue at `http://localhost:5000/hangfire`. Not required for normal use.

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
| `Ollama:Endpoint` | `http://localhost:11434` | Ollama server URL |
| `Ollama:Model` | `nemotron-mini` | Model to use for coaching reports |
| `YoloModel:Path` | `Models/yolo11n.onnx` | Path to ONNX model file (relative to app base) |
| `FFmpeg:BinaryFolder` | _(system PATH)_ | Explicit path to FFmpeg `bin` folder — set if `ffmpeg` is not on PATH |

## Hangfire Dashboard

Available at `http://localhost:5000/hangfire` when the API is running. Shows job queue, retries, and history.

## License

Source code: MIT  
YOLO models: [AGPL-3.0](https://www.ultralytics.com/license) — commercial use requires a separate Ultralytics license.
