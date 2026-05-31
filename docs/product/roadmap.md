# PickleIQ — Product Roadmap

> Phase 1 is the current focus. Phases 2–4 are directional — scope will be refined after Phase 1 ships.

---

## Phase 1 — MVP: Core Pipeline *(current)*

Free, no-auth web app. Players upload a match video and receive a highlight reel and AI coaching report.

- Video upload (MP4, up to 2GB)
- Rally detection via YoloDotNet
- 60-second highlight reel generation via FFmpeg
- AI coaching report (strengths, improvements, drills)
- Shareable results page with highlight download

---

## Phase 2 — Player Tracking & Positioning

Enrich the coaching report with spatial data from the match.

- Player bounding box tracking frame-by-frame during rallies
- Court zone classification (kitchen, transition zone, baseline)
- Time-in-zone statistics per rally and match
- Player movement heatmap as PNG overlay on court diagram
- Heatmap and positioning stats added to coaching report

---

## Phase 3 — Ball Tracking & Shot Classification

Add shot-level analysis. Highest technical risk phase — may require custom-trained YOLO models.

- Ball detection and trajectory tracking
- Shot type classification: drive, dink, drop, smash, ATP, Erne
- Shot frequency counts per player
- Automated rally-ending shot identification
- Shot statistics integrated into coaching report

---

## Phase 4 — Academy Platform & Subscriptions

Scale from individual players to coaches and academies.

- User accounts and authentication
- Coach portal: manage students, view their reports
- Academy multi-coach licensing and team management
- Shared video library across a team
- Subscription tiers: Community (free), Coach, Academy
- Branded coaching report export (coach logo, player name)
