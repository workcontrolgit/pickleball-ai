# PickleIQ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the PickleIQ Phase 1 MVP — a free web app where players upload a pickleball match video and receive an AI-generated highlight reel and coaching report within 5 minutes.

**Architecture:** Blazor Server frontend, ASP.NET Core API, Hangfire background processing, FFMpegCore for video operations, YoloDotNet for player/court detection, Ollama + Nemotron3 for AI coaching (swappable), SQL Server Express for persistence.

**Tech Stack:** .NET 10, Blazor Server, EF Core, Hangfire, FFMpegCore, YoloDotNet, OllamaSharp, SQL Server Express

**Traceability:** All tasks link to GitHub issues at https://github.com/workcontrolgit/pickleball-ai

---

## Phase 0 — Project Foundation

Epic: [#4](https://github.com/workcontrolgit/pickleball-ai/issues/4)

### Task 1: Project Structure Setup

**GitHub:** [#6](https://github.com/workcontrolgit/pickleball-ai/issues/6) | Feature: [#5](https://github.com/workcontrolgit/pickleball-ai/issues/5)

**Files:**
- Move: `PickleIQ/PickleIQ/` → `src/PickleIQ/`
- Create: `docs/product/`, `docs/architecture/`, `docs/risks/`
- Modify: `.gitignore`

- [ ] Move the existing .NET project: `mv PickleIQ/PickleIQ src/PickleIQ`
- [ ] Create doc directories: `mkdir -p docs/product docs/architecture docs/risks`
- [ ] Verify `dotnet build src/PickleIQ/PickleIQ.csproj` passes
- [ ] Commit: `git commit -m "chore: reorganize into src/ and docs/ layout"`

---

### Task 2: Product Vision Doc

**GitHub:** [#8](https://github.com/workcontrolgit/pickleball-ai/issues/8) | Feature: [#7](https://github.com/workcontrolgit/pickleball-ai/issues/7)

**Files:**
- Create: `docs/product/vision.md`

- [ ] Write `docs/product/vision.md` covering: elevator pitch, target customers, problem, solution, success metrics
- [ ] Commit: `git commit -m "docs: add product vision"`

---

### Task 3: PRD Phase 1

**GitHub:** [#9](https://github.com/workcontrolgit/pickleball-ai/issues/9) | Feature: [#7](https://github.com/workcontrolgit/pickleball-ai/issues/7)

**Files:**
- Create: `docs/product/prd-phase1.md`

- [ ] Write `docs/product/prd-phase1.md` with narrative + feature table (F01–F05) + out-of-scope section
- [ ] Commit: `git commit -m "docs: add Phase 1 PRD"`

---

### Task 4: Roadmap

**GitHub:** [#10](https://github.com/workcontrolgit/pickleball-ai/issues/10) | Feature: [#7](https://github.com/workcontrolgit/pickleball-ai/issues/7)

**Files:**
- Create: `docs/product/roadmap.md`

- [ ] Write `docs/product/roadmap.md` with phases 2–4 as bullet summaries
- [ ] Commit: `git commit -m "docs: add product roadmap"`

---

### Task 5: Architecture Overview

**GitHub:** [#12](https://github.com/workcontrolgit/pickleball-ai/issues/12) | Feature: [#11](https://github.com/workcontrolgit/pickleball-ai/issues/11)

**Files:**
- Create: `docs/architecture/overview.md`

- [ ] Write `docs/architecture/overview.md` with one prose section per decision (UI, video, CV, AI, DB, background) + alternatives table
- [ ] Commit: `git commit -m "docs: add architecture overview"`

---

### Task 6: Risk Register

**GitHub:** [#13](https://github.com/workcontrolgit/pickleball-ai/issues/13) | Feature: [#11](https://github.com/workcontrolgit/pickleball-ai/issues/11)

**Files:**
- Create: `docs/risks/risk-register.md`

- [ ] Write `docs/risks/risk-register.md` with 8 risks across two tiers, each with mitigation and owner
- [ ] Commit: `git commit -m "docs: add risk register"`

---

## Phase 1 — MVP Core Pipeline

Epic: [#14](https://github.com/workcontrolgit/pickleball-ai/issues/14)

### Task 7: .NET Solution Scaffold

**GitHub:** [#16](https://github.com/workcontrolgit/pickleball-ai/issues/16) | Feature: [#15](https://github.com/workcontrolgit/pickleball-ai/issues/15)

**Files:**
- Create: `src/PickleIQ.sln`
- Create: `src/PickleIQ.Web/`
- Create: `src/PickleIQ.Api/`
- Create: `src/PickleIQ.Core/`
- Create: `src/PickleIQ.Infrastructure/`

- [ ] `dotnet new sln -n PickleIQ -o src`
- [ ] `dotnet new blazorserver -n PickleIQ.Web -o src/PickleIQ.Web`
- [ ] `dotnet new webapi -n PickleIQ.Api -o src/PickleIQ.Api`
- [ ] `dotnet new classlib -n PickleIQ.Core -o src/PickleIQ.Core`
- [ ] `dotnet new classlib -n PickleIQ.Infrastructure -o src/PickleIQ.Infrastructure`
- [ ] Add all projects to solution and wire references (Web→Core, Api→Core, Infrastructure→Core)
- [ ] `dotnet build src/PickleIQ.sln` — expect 0 errors
- [ ] Commit: `git commit -m "feat: scaffold .NET solution with 4 projects"`

---

### Task 8: Database & EF Core

**GitHub:** [#17](https://github.com/workcontrolgit/pickleball-ai/issues/17) | Feature: [#15](https://github.com/workcontrolgit/pickleball-ai/issues/15)

**Files:**
- Create: `src/PickleIQ.Core/Entities/VideoJob.cs`
- Create: `src/PickleIQ.Core/Entities/RallySegment.cs`
- Create: `src/PickleIQ.Core/Entities/CoachingReport.cs`
- Create: `src/PickleIQ.Infrastructure/Data/AppDbContext.cs`

- [ ] Add EF Core + SQL Server NuGet packages to `PickleIQ.Infrastructure`
- [ ] Define `VideoJob`, `RallySegment`, `CoachingReport` entities with required properties
- [ ] Create `AppDbContext` with all 3 `DbSet<>` properties
- [ ] Add connection string to `appsettings.json` using SQL Server LocalDB
- [ ] `dotnet ef migrations add InitialCreate --project src/PickleIQ.Infrastructure --startup-project src/PickleIQ.Api`
- [ ] `dotnet ef database update` — verify database created
- [ ] Commit: `git commit -m "feat: add EF Core entities and initial migration"`

---

### Task 9: Hangfire Background Processing

**GitHub:** [#18](https://github.com/workcontrolgit/pickleball-ai/issues/18) | Feature: [#15](https://github.com/workcontrolgit/pickleball-ai/issues/15)

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/IVideoProcessingJob.cs`
- Create: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`
- Modify: `src/PickleIQ.Api/Program.cs`

- [ ] Add `Hangfire` and `Hangfire.SqlServer` NuGet to `PickleIQ.Infrastructure` and `PickleIQ.Api`
- [ ] Define `IVideoProcessingJob` interface with `ProcessAsync(Guid jobId)` method
- [ ] Implement stub `VideoProcessingJob` (logs job start/end only)
- [ ] Register Hangfire in `Program.cs` with SQL Server storage
- [ ] Add Hangfire Dashboard at `/hangfire`
- [ ] `dotnet run` — navigate to `/hangfire` to verify dashboard visible
- [ ] Commit: `git commit -m "feat: configure Hangfire with SQL Server storage"`

---

### Task 10: F01 — Video Upload

**GitHub:** [#20](https://github.com/workcontrolgit/pickleball-ai/issues/20) | Feature: [#19](https://github.com/workcontrolgit/pickleball-ai/issues/19)

**Files:**
- Create: `src/PickleIQ.Web/Pages/Upload.razor`
- Create: `src/PickleIQ.Core/Interfaces/IVideoStorageService.cs`
- Create: `src/PickleIQ.Infrastructure/Services/VideoStorageService.cs`

- [ ] Create Blazor `Upload.razor` page with `InputFile` component, 2GB limit, progress indicator
- [ ] Define `IVideoStorageService` with `SaveAsync(IBrowserFile file)` returning `(Guid jobId, string filePath)`
- [ ] Implement `VideoStorageService`: save file to configured path, create `VideoJob` record, enqueue Hangfire job
- [ ] Add storage path to `appsettings.json`: `"VideoStorage": { "BasePath": "c:/temp/pickleiq" }`
- [ ] Test: upload a small MP4, verify `VideoJob` row appears in DB with `Status = Queued`
- [ ] Test: attempt upload of file > 2GB, verify friendly error shown
- [ ] Commit: `git commit -m "feat(F01): video upload page and storage service"`

---

### Task 11: F02 — Rally Detection

**GitHub:** [#22](https://github.com/workcontrolgit/pickleball-ai/issues/22) | Feature: [#21](https://github.com/workcontrolgit/pickleball-ai/issues/21)

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/IRallyDetectionService.cs`
- Create: `src/PickleIQ.Infrastructure/Services/RallyDetectionService.cs`
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

- [ ] Add `FFMpegCore` and `YoloDotNet` NuGet packages to `PickleIQ.Infrastructure`
- [ ] Download pre-trained YOLO11n model (`yolo11n.onnx`) to `src/PickleIQ.Infrastructure/Models/`
- [ ] Define `IRallyDetectionService` with `DetectRalliesAsync(string videoPath)` returning `IList<(double Start, double End)>`
- [ ] Implement frame extraction at 2fps using FFMpegCore
- [ ] Run YoloDotNet player detection on each frame; flag frames with 2+ person detections as active
- [ ] Group consecutive active frames into segments (1s gap tolerance, 3s minimum length)
- [ ] Save segments to `RallySegments` table; update `VideoJob.Status = RallyDetectionComplete`
- [ ] Wire `RallyDetectionService` into `VideoProcessingJob.ProcessAsync`
- [ ] Test with a sample MP4: verify segments saved to DB with reasonable timestamps
- [ ] Commit: `git commit -m "feat(F02): rally detection via FFMpegCore + YoloDotNet"`

---

### Task 12: F03 — Highlight Reel

**GitHub:** [#24](https://github.com/workcontrolgit/pickleball-ai/issues/24) | Feature: [#23](https://github.com/workcontrolgit/pickleball-ai/issues/23)

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/IHighlightGenerationService.cs`
- Create: `src/PickleIQ.Infrastructure/Services/HighlightGenerationService.cs`
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

- [ ] Define `IHighlightGenerationService` with `GenerateAsync(Guid jobId, string videoPath)` returning `string highlightPath`
- [ ] Select rally segments (longest first) until ~60s total; add 2s padding per clip
- [ ] Extract clips using FFMpegCore; concatenate with FFmpeg concat demuxer
- [ ] Save output MP4 as `{jobId}-highlights.mp4`; update `VideoJob.HighlightFilePath` and `Status = HighlightComplete`
- [ ] Wire into `VideoProcessingJob.ProcessAsync` after rally detection
- [ ] Test: verify output MP4 plays and is 55–65 seconds
- [ ] Commit: `git commit -m "feat(F03): highlight reel generation via FFMpegCore"`

---

### Task 13: F04 — AI Coaching Report

**GitHub:** [#26](https://github.com/workcontrolgit/pickleball-ai/issues/26) | Feature: [#25](https://github.com/workcontrolgit/pickleball-ai/issues/25)

**Files:**
- Create: `src/PickleIQ.Core/Interfaces/ICoachingEngine.cs`
- Create: `src/PickleIQ.Infrastructure/AI/OllamaCoachingEngine.cs`
- Create: `src/PickleIQ.Infrastructure/AI/CoachingReportGenerator.cs`
- Modify: `src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs`

- [ ] Add `OllamaSharp` NuGet to `PickleIQ.Infrastructure`
- [ ] Define `ICoachingEngine` with `GenerateReportAsync(MatchSummary summary)` returning `string htmlReport`
- [ ] Implement `OllamaCoachingEngine` using `OllamaApiClient` with configured model (`nemotron3` default)
- [ ] Build prompt from `MatchSummary` (rally count, avg length, max length)
- [ ] Parse AI response into HTML with sections: strengths, improvements, drills, match summary
- [ ] Save HTML to `CoachingReports` table; update `VideoJob.Status = ReportComplete`
- [ ] Add Ollama config to `appsettings.json`: `"Ollama": { "Endpoint": "http://localhost:11434", "Model": "nemotron3" }`
- [ ] Test: with Ollama running locally, verify report contains all 4 sections
- [ ] Commit: `git commit -m "feat(F04): AI coaching report via OllamaSharp"`

---

### Task 14: F05 — Results Page & Video Export

**GitHub:** [#28](https://github.com/workcontrolgit/pickleball-ai/issues/28) | Feature: [#27](https://github.com/workcontrolgit/pickleball-ai/issues/27)

**Files:**
- Create: `src/PickleIQ.Web/Pages/Results.razor`
- Create: `src/PickleIQ.Api/Controllers/DownloadController.cs`

- [ ] Create `Results.razor` at route `/results/{jobId:guid}`
- [ ] Poll `VideoJob.Status` every 5s while status is not `ReportComplete` or `Failed`
- [ ] Render coaching report HTML inline when complete
- [ ] Add rally statistics section (rally count, avg length, longest rally)
- [ ] Add download button linking to `GET /api/download/{jobId}/highlights`
- [ ] Implement `DownloadController` returning the highlight MP4 as `FileStreamResult`
- [ ] Test: full end-to-end — upload MP4, wait for processing, view results, download highlight
- [ ] Commit: `git commit -m "feat(F05): results page and highlight download endpoint"`

---

## Phase 2 — Player Tracking & Heatmaps

Epic: [#29](https://github.com/workcontrolgit/pickleball-ai/issues/29) | Feature: [#30](https://github.com/workcontrolgit/pickleball-ai/issues/30)

User Story: [#31](https://github.com/workcontrolgit/pickleball-ai/issues/31)

> Detailed task breakdown to be written when Phase 1 is complete.

---

## Phase 3 — Ball Tracking & Shot Classification

Epic: [#32](https://github.com/workcontrolgit/pickleball-ai/issues/32) | Feature: [#33](https://github.com/workcontrolgit/pickleball-ai/issues/33)

User Story: [#34](https://github.com/workcontrolgit/pickleball-ai/issues/34)

> Detailed task breakdown to be written when Phase 2 is complete. Custom YOLO model training likely required (see R02, R03 in risk register).

---

## Traceability Index

| GitHub Issue | Type | Phase | Feature |
|---|---|---|---|
| [#4](https://github.com/workcontrolgit/pickleball-ai/issues/4) | Epic | 0 | Phase Foundation |
| [#5](https://github.com/workcontrolgit/pickleball-ai/issues/5) | Feature | 0 | Project Structure |
| [#6](https://github.com/workcontrolgit/pickleball-ai/issues/6) | User Story | 0 | Folder Reorganization |
| [#7](https://github.com/workcontrolgit/pickleball-ai/issues/7) | Feature | 0 | Product Documentation |
| [#8](https://github.com/workcontrolgit/pickleball-ai/issues/8) | User Story | 0 | Vision Doc |
| [#9](https://github.com/workcontrolgit/pickleball-ai/issues/9) | User Story | 0 | PRD Phase 1 |
| [#10](https://github.com/workcontrolgit/pickleball-ai/issues/10) | User Story | 0 | Roadmap |
| [#11](https://github.com/workcontrolgit/pickleball-ai/issues/11) | Feature | 0 | Technical Documentation |
| [#12](https://github.com/workcontrolgit/pickleball-ai/issues/12) | User Story | 0 | Architecture Overview |
| [#13](https://github.com/workcontrolgit/pickleball-ai/issues/13) | User Story | 0 | Risk Register |
| [#14](https://github.com/workcontrolgit/pickleball-ai/issues/14) | Epic | 1 | MVP Core Pipeline |
| [#15](https://github.com/workcontrolgit/pickleball-ai/issues/15) | Feature | 1 | Solution Infrastructure |
| [#16](https://github.com/workcontrolgit/pickleball-ai/issues/16) | User Story | 1 | .NET Solution Scaffold |
| [#17](https://github.com/workcontrolgit/pickleball-ai/issues/17) | User Story | 1 | Database & EF Core |
| [#18](https://github.com/workcontrolgit/pickleball-ai/issues/18) | User Story | 1 | Hangfire Setup |
| [#19](https://github.com/workcontrolgit/pickleball-ai/issues/19) | Feature | 1 | F01 Video Upload |
| [#20](https://github.com/workcontrolgit/pickleball-ai/issues/20) | User Story | 1 | F01 Upload Story |
| [#21](https://github.com/workcontrolgit/pickleball-ai/issues/21) | Feature | 1 | F02 Rally Detection |
| [#22](https://github.com/workcontrolgit/pickleball-ai/issues/22) | User Story | 1 | F02 Rally Story |
| [#23](https://github.com/workcontrolgit/pickleball-ai/issues/23) | Feature | 1 | F03 Highlight Reel |
| [#24](https://github.com/workcontrolgit/pickleball-ai/issues/24) | User Story | 1 | F03 Highlight Story |
| [#25](https://github.com/workcontrolgit/pickleball-ai/issues/25) | Feature | 1 | F04 Coaching Report |
| [#26](https://github.com/workcontrolgit/pickleball-ai/issues/26) | User Story | 1 | F04 Coaching Story |
| [#27](https://github.com/workcontrolgit/pickleball-ai/issues/27) | Feature | 1 | F05 Video Export |
| [#28](https://github.com/workcontrolgit/pickleball-ai/issues/28) | User Story | 1 | F05 Export Story |
| [#29](https://github.com/workcontrolgit/pickleball-ai/issues/29) | Epic | 2 | Player Tracking |
| [#30](https://github.com/workcontrolgit/pickleball-ai/issues/30) | Feature | 2 | Movement Tracking |
| [#31](https://github.com/workcontrolgit/pickleball-ai/issues/31) | User Story | 2 | Heatmap Story |
| [#32](https://github.com/workcontrolgit/pickleball-ai/issues/32) | Epic | 3 | Ball Tracking |
| [#33](https://github.com/workcontrolgit/pickleball-ai/issues/33) | Feature | 3 | Shot Classification |
| [#34](https://github.com/workcontrolgit/pickleball-ai/issues/34) | User Story | 3 | Shot Story |
