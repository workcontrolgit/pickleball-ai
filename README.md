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
- [FFmpeg](https://ffmpeg.org/download.html) — must be on `PATH`
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

### 3. Configure storage paths (optional)

The defaults write videos and highlights to `C:/temp/pickleiq/`. Override in `appsettings.Development.json`:

```json
{
  "VideoStorage": {
    "BasePath": "C:/your/path/videos",
    "HighlightsPath": "C:/your/path/highlights"
  }
}
```

### 4. Run

Start both projects. The Web project hosts the UI and the Hangfire background worker:

```bash
# Terminal 1 — Blazor Web (port 5001)
dotnet run --project src/PickleIQ.Web

# Terminal 2 — API (port 5000) — only needed for the Hangfire dashboard
dotnet run --project src/PickleIQ.Api
```

Open `https://localhost:5001/upload` and upload a match video.

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

## Hangfire Dashboard

Available at `http://localhost:5000/hangfire` when the API is running. Shows job queue, retries, and history.

## License

Source code: MIT  
YOLO models: [AGPL-3.0](https://www.ultralytics.com/license) — commercial use requires a separate Ultralytics license.
