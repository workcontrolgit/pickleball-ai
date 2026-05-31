# PickleIQ — Risk Register

**Status:** Active  
**Last updated:** 2026-05-31  
**Owner:** PM / Tech Lead

Risks are reviewed at the start of each phase. Update likelihood and status as new information emerges.

---

## Tier 1 — High Technical Risk

These risks could block delivery or significantly reduce the value of Phase 1 if not mitigated.

| ID | Risk | Likelihood | Impact | Mitigation | Owner | Status |
|----|------|-----------|--------|------------|-------|--------|
| R01 | Rally start/end detection is unreliable | High | High | Accept lower accuracy in MVP; provide manual timestamp override as fallback; plan custom YOLO training in Phase 3 | Tech | Open |
| R02 | Point winner identification is inaccurate | High | High | Excluded from Phase 1 entirely; treated as Phase 3 feature after ball tracking is available | Tech | Mitigated (deferred) |
| R03 | Third-shot drop and shot classification fails | High | Medium | Excluded from Phase 1; planned for Phase 3 with dedicated shot-classification model | Tech | Mitigated (deferred) |
| R04 | YoloDotNet + pre-trained YOLO model accuracy is insufficient for pickleball courts | Medium | High | Evaluate accuracy on real match footage during development; if accuracy < 70% rally detection rate, evaluate Roboflow custom training or Azure CV as fallback before shipping | Tech | Open |

---

## Tier 2 — Medium Product & Market Risk

These risks could reduce adoption or product quality but do not block delivery.

| ID | Risk | Likelihood | Impact | Mitigation | Owner | Status |
|----|------|-----------|--------|------------|-------|--------|
| R05 | Players don't upload videos (low adoption) | Medium | High | Shareable highlight reels drive organic word-of-mouth; frictionless free tier removes every barrier to first use; no account required | PM | Open |
| R06 | AI coaching report is too generic to be useful | Medium | High | Iterative prompt engineering during development; capture player skill level as optional input to improve specificity; compare against manual coaching notes from a real coach | Tech/PM | Open |
| R07 | Video processing takes too long for acceptable UX | Medium | Medium | Background job with real-time status indicator sets expectations; target < 5 minutes; if exceeded, add email notification for "ready" state | Tech | Open |
| R08 | FFmpeg LGPL licensing non-compliance for commercial distribution | Low | High | FFmpeg is LGPL — dynamic linking is compliant; verify binary distribution approach before any commercial launch; consult legal if bundling FFmpeg binaries directly | PM | Open |

---

## Risk Review History

| Date | Reviewer | Notes |
|------|----------|-------|
| 2026-05-31 | PM | Initial register created. R02 and R03 mitigated by deferral to Phase 3. R01 and R04 remain open — to be validated during Phase 1 development with real match footage. |
