---
title: PickleIQ PM Package Design
date: 2026-05-30
status: approved
audience: small team (developers + stakeholders)
---

# PickleIQ PM Package Design

## Purpose

This document records the design decisions made during the PM package brainstorming session. It defines the folder structure, the documents to be produced, and the structure of each document for the PickleIQ Phase 1 MVP.

---

## Decisions Made

### 1. Folder Structure

**Choice:** Flat Sibling Layout — `docs/` and `src/` at the root, side by side.

```
PickleIQ/
├── docs/
│   ├── product/
│   │   ├── vision.md
│   │   ├── prd-phase1.md
│   │   └── roadmap.md
│   ├── architecture/
│   │   └── overview.md
│   └── risks/
│       └── risk-register.md
├── src/
│   └── PickleIQ/
│       ├── PickleIQ.csproj
│       ├── Program.cs
│       └── ...
└── README.md
```

The existing project under `PickleIQ/PickleIQ/` moves into `src/PickleIQ/`. `.superpowers/` is added to `.gitignore`.

---

### 2. Documents to Produce

Five documents across three concern areas:

| Document | Path | Purpose |
|----------|------|---------|
| Product Vision | `docs/product/vision.md` | Elevator pitch, target customers, success metrics |
| PRD Phase 1 | `docs/product/prd-phase1.md` | Features, user stories, acceptance criteria |
| Roadmap | `docs/product/roadmap.md` | Phases 2–4 as a brief list |
| Architecture Overview | `docs/architecture/overview.md` | Tech stack decisions + alternatives |
| Risk Register | `docs/risks/risk-register.md` | Tabular risk log with mitigations |

---

### 3. PRD Structure (`docs/product/prd-phase1.md`)

**Approach:** Hybrid — narrative for stakeholders, structured feature table for developers.

**Part 1 — Narrative**
- Problem: Players lack access to objective self-analysis of their match footage
- Solution: Upload a match video and receive an AI-generated highlight reel and coaching report — no coach required
- Success criteria: A player can go from raw MP4 to shareable highlight reel + coaching report in under 5 minutes

**Part 2 — Feature Table**

| ID | Feature | User Story | Acceptance Criteria | Priority |
|----|---------|-----------|---------------------|----------|
| F01 | Video Upload | As a player, I want to upload my match MP4 so I can have it analyzed | File accepted up to 2GB, stored, job queued | P0 |
| F02 | Rally Detection | As a player, I want my rallies auto-detected so I don't have to tag footage manually | Segments identified with start/end timestamps | P0 |
| F03 | Highlight Reel | As a player, I want a 60-second highlight video of my best moments | FFmpeg-generated MP4, downloadable | P0 |
| F04 | Coaching Report | As a player, I want an AI coaching summary of my match | HTML report with strengths, improvement areas, drill recommendations | P0 |
| F05 | Video Export | As a player, I want to share my highlights with others | Shareable link or downloadable file | P1 |

**Part 3 — Out of Scope for Phase 1**
- Authentication / user accounts
- Player tracking and movement heatmaps
- Ball tracking
- Shot classification
- Subscriptions and billing

---

### 4. Architecture Overview Structure (`docs/architecture/overview.md`)

**Approach:** Short prose decision section per component + "why not X" alternatives table.

**Decision 1 — UI Layer**
Blazor Server. Chosen for .NET team familiarity, real-time progress updates via SignalR, no separate frontend build pipeline.
Alternatives considered: React + API (frontend overhead), Razor Pages (less interactive).

**Decision 2 — Video Processing**
FFmpeg via FFMpegCore. Frame extraction, trimming, and highlight stitching. Industry standard, MIT-licensed, runs locally.
Alternatives considered: Azure Media Services, Mux — deferred to post-MVP to avoid cloud cost.

**Decision 3 — Computer Vision**
YoloDotNet with pre-trained YOLO models for player, paddle, and court detection. Runs locally, no external API calls.
Alternatives considered: Azure Computer Vision, Roboflow — add cloud cost and latency, revisit in Phase 3.

**Decision 4 — AI Coaching Engine**
Recommended default: Ollama + Nemotron3 (local, free, private — no data leaves the machine).
Alternatives documented (swappable via one service class):
- Claude API — stronger reasoning, per-token cost
- OpenAI API — highest capability, same cost trade-off

**Decision 5 — Data Storage**
SQL Server Express for MVP. Free, locally hosted, full SQL Server compatibility. Clean upgrade path to SQL Server Standard/Enterprise for Academy edition.

**Decision 6 — Background Processing**
Hangfire for long-running video processing jobs. ASP.NET Hosted Services as a lightweight alternative for simpler tasks.

---

### 5. Risk Register Structure (`docs/risks/risk-register.md`)

**Approach:** Tabular with likelihood, impact, mitigation, and owner per risk. Two tiers.

**Tier 1 — High Technical Risk**

| ID | Risk | Likelihood | Impact | Mitigation | Owner |
|----|------|-----------|--------|------------|-------|
| R01 | Rally start/end detection unreliable | High | High | Manual tagging fallback in early releases; custom YOLO training in Phase 3 | Tech |
| R02 | Point winner identification inaccurate | High | High | Skip in MVP; flag as Phase 3 feature | Tech |
| R03 | Third-shot drop / shot classification fails | High | Medium | Exclude from MVP; treat as Phase 3 | Tech |
| R04 | YoloDotNet model accuracy insufficient for pickleball | Medium | High | Evaluate pre-trained models early; budget for custom model training | Tech |

**Tier 2 — Medium Product/Market Risk**

| ID | Risk | Likelihood | Impact | Mitigation | Owner |
|----|------|-----------|--------|------------|-------|
| R05 | Users don't upload videos (adoption risk) | Medium | High | Shareable highlight reels drive organic growth; frictionless free tier | PM |
| R06 | AI coaching report quality too generic | Medium | High | Prompt engineering iteration; include player skill level as input | Tech/PM |
| R07 | Video processing too slow for acceptable UX | Medium | Medium | Background jobs with progress indicator; set upfront expectations | Tech |
| R08 | FFmpeg licensing non-compliance for commercial use | Low | High | FFmpeg is LGPL; verify binary distribution compliance before launch | PM |
