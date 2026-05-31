# PickleIQ — Product Requirements Document
## Phase 1 MVP

**Status:** Draft  
**Audience:** Developers, stakeholders  
**Last updated:** 2026-05-31

---

## Part 1 — Narrative

### Problem

Players lack access to objective self-analysis of their match footage. Watching raw video is time-consuming, and without structured feedback, players can't identify what to actually work on. Coaching is expensive and not always available.

### Solution

Upload a match video and receive an AI-generated highlight reel and coaching report — no coach required. PickleIQ detects rallies automatically, builds a 60-second highlight clip, and uses AI to summarize strengths, improvement areas, and recommended drills.

### Success Criteria

A player can go from raw MP4 to a shareable highlight reel and coaching report in **under 5 minutes**.

---

## Part 2 — Features

| ID | Feature | User Story | Acceptance Criteria | Priority |
|----|---------|-----------|---------------------|----------|
| F01 | Video Upload | As a player, I want to upload my match MP4 so I can have it analyzed | File accepted up to 2GB; stored on server; background job queued; player sees job ID confirmation | P0 |
| F02 | Rally Detection | As a player, I want my rallies auto-detected so I don't have to tag footage manually | Rally segments identified with start/end timestamps; minimum 3s length; segments saved to database | P0 |
| F03 | Highlight Reel | As a player, I want a 60-second highlight video of my best moments | FFmpeg-generated MP4, 55–65s duration; longest rallies prioritized; downloadable | P0 |
| F04 | Coaching Report | As a player, I want an AI coaching summary of my match so I know what to improve | HTML report with 4 sections: strengths, improvement areas, drill recommendations, match summary | P0 |
| F05 | Video Export | As a player, I want to share my highlights with others | Results page accessible via direct URL; highlight MP4 downloadable via browser | P1 |

---

## Part 3 — Feature Detail

### F01 — Video Upload

**Entry point:** `/upload` page in Blazor UI

**Flow:**
1. Player selects an MP4 file via file picker
2. File uploaded with progress indicator
3. File saved to server storage with unique filename
4. `VideoJob` record created in database (`Status = Queued`)
5. Hangfire background job enqueued
6. Player shown confirmation with job ID and link to results page

**Constraints:**
- Max file size: 2GB
- Accepted format: `.mp4` only
- No authentication required

---

### F02 — Rally Detection

**Runs as:** Hangfire background job (step 1 of pipeline)

**Pipeline:**
1. Extract frames from video at 2fps using FFMpegCore
2. Run YoloDotNet player detection on each frame
3. Flag frames with 2 or more person detections as "active"
4. Group consecutive active frames into rally segments (1s gap tolerance)
5. Discard segments shorter than 3 seconds
6. Save segments to `RallySegments` table

**Output:** List of `(StartSeconds, EndSeconds)` per rally

**Known Risk:** R01, R04 — pre-trained YOLO models may not reliably detect players in all court lighting conditions. Accuracy validated during development; manual tagging fallback documented for early testers.

---

### F03 — Highlight Reel Generation

**Runs as:** Hangfire background job (step 2 of pipeline, after F02)

**Pipeline:**
1. Sort rally segments by duration (longest first)
2. Select segments until ~60 seconds total accumulated
3. For each selected segment: add 2-second padding before and after
4. Extract each clip using FFMpegCore
5. Concatenate all clips into single MP4 using FFmpeg concat demuxer
6. Save output as `{jobId}-highlights.mp4`

**Constraints:**
- If total rally time < 60s, use all available rallies (no artificial padding)
- Output stored in configurable path (`appsettings.json`)

---

### F04 — AI Coaching Report

**Runs as:** Hangfire background job (step 3 of pipeline, after F03)

**AI Engine:** Ollama + Nemotron3 (local, default). Swappable to Claude API or OpenAI API via `ICoachingEngine` interface without code changes.

**Prompt inputs:**
- Rally count
- Average rally length (seconds)
- Longest rally (seconds)
- Total match duration

**Report sections:**
1. **Strengths** — 2–3 bullet points
2. **Areas for Improvement** — 2–3 bullet points
3. **Recommended Drills** — 2–3 drills with brief descriptions
4. **Match Summary** — 1 paragraph

**Output:** HTML string saved to `CoachingReports` table

**Known Risk:** R06 — report may be generic without richer match data. Prompt engineering is iterative; player skill level input planned for Phase 2.

---

### F05 — Results Page & Export

**Entry point:** `/results/{jobId}` page in Blazor UI

**Behavior:**
- Polls `VideoJob.Status` every 5 seconds while processing
- Shows progress indicator while job is running
- On completion: renders coaching report HTML inline + shows rally stats
- Download button fetches highlight MP4 via `GET /api/download/{jobId}/highlights`
- Direct URL is shareable (no login required)
- On failure: shows friendly error message with job ID for support

---

## Part 4 — Out of Scope for Phase 1

The following are explicitly **not** built in Phase 1:

| Item | Planned Phase |
|------|--------------|
| User authentication / accounts | Phase 4 |
| Player movement tracking | Phase 2 |
| Court positioning heatmaps | Phase 2 |
| Ball tracking | Phase 3 |
| Shot classification (dink, drive, ATP, Erne) | Phase 3 |
| Point winner identification | Phase 3 |
| Subscriptions and billing | Phase 4 |
| Mobile app | Post Phase 4 |
| Coach/academy management portal | Phase 4 |

---

## Part 5 — Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| End-to-end processing time | < 5 minutes for a 60-minute match |
| Supported video format | MP4 (H.264) |
| Max upload size | 2GB |
| Concurrent jobs | 1 per server (MVP — single Hangfire worker) |
| Availability | Local/self-hosted (MVP) |
| Data privacy | No video data leaves the server (local AI engine) |
