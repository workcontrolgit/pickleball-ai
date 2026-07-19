# Cancel In-Progress Job — Design Spec

**Date:** 2026-06-12  
**Status:** Approved

## Problem

Jobs stuck in any in-progress state (Queued, RallyDetectionInProgress, RallyDetectionComplete, HighlightInProgress, HighlightComplete, ReportInProgress) show only a "View Progress" button. There is no way to dismiss or reset a stuck job from the UI without directly modifying the database.

## Solution

Add a **Cancel** button to the in-progress action branch in `Jobs.razor`. Cancelling resets the job to `Failed` with `ErrorMessage = "Cancelled by user"`, preserving the video file. The existing Retry button then becomes available for reprocessing.

## Scope

Single file change: `src/PickleIQ.Web/Components/Pages/Jobs.razor`

## UI

The in-progress `else` branch in `ActionButtons` gains a Cancel button alongside "View Progress":

```
[View Progress]  [Cancel]
```

- Cancel button: `Variant.Outlined`, `Color.Error`, `Size.Small`, `Icons.Material.Filled.Cancel`
- Disabled when `_cancelling.Contains(job.Id)`
- Label: "Cancelling…" while in progress, "Cancel" otherwise

## Confirm Dialog

```
Title:   Cancel Processing
Message: Cancel processing for "{filename}"? The video file will be kept. You can reprocess it later.
Yes:     Cancel Processing
Cancel:  Keep Waiting
```

## CancelAsync Logic

1. Add `jobId` to `_cancelling`
2. Show confirm dialog — return if not confirmed
3. Load job from DB
4. If job exists and status is NOT `Failed` or `ReportComplete`:
   - `job.Status = VideoJobStatus.Failed`
   - `job.ErrorMessage = "Cancelled by user"`
   - `job.HighlightFilePath = null`
   - `job.CompletedAt = null`
   - Remove all `RallySegments` for this job
   - Remove `CoachingReport` for this job if present
   - `SaveChangesAsync()`
5. Remove `jobId` from `_cancelling`
6. `LoadJobsAsync()`

## State Tracking

Add `private readonly HashSet<Guid> _cancelling = [];` — same pattern as `_deleting`, `_retrying`.

## Hangfire Worker Behavior

The Hangfire worker for the cancelled job may still be running. When it attempts to save status updates to a `Failed` job, the DB writes will succeed but the job will already be in Failed state. No data corruption occurs. The worker will eventually complete or time out without user-visible impact.

## Unchanged

- `DeleteAsync` — no changes
- `RetryAsync` — no changes  
- `RetagAndReprocessAsync` — no changes
- All other pages — no changes
