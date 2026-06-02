# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.
| 03:00 | Registered IJobStatusService singleton in Program.cs; rewrote Results.razor @code to replace 5s polling with push via ConsumeStatusAsync + 60s fallback timer | Program.cs, Results.razor | committed a2b9146 | ~800 |
| Task3 | Created QwenVisionCoachingEngine (vision+stats coaching via Qwen2-VL), deleted OllamaCoachingEngine | QwenVisionCoachingEngine.cs | committed; Program.cs ref pending Task5 | ~600 |
| Task4 | Registered ICoachingStreamService singleton in Program.cs; updated Results.razor with live streaming via ConsumeStreamAsync + ChannelReader | Program.cs, Results.razor | build succeeded, committed 4e1aa07 | ~400 |
| CodeReview | Final code review of Qwen2-VL vision coaching engine feature | ICoachingEngine.cs, ICoachingFrameSampler.cs, CoachingFrameSampler.cs, QwenVisionCoachingEngine.cs, VideoProcessingJob.cs | Build passes 0 errors 0 warnings; 2 issues found (TotalMatchSeconds hardcoded 0, finally block deletes tempDir before async reads complete) | ~900 |
| 16:48 | GPU acceleration: h264_nvenc for FFmpeg (with CPU fallback), CUDA for YOLO (with CPU fallback) | HighlightGenerationService.cs, RallyDetectionService.cs, appsettings.json | build success | ~800 |
| 00:03 | Installed MudBlazor 9.5.0, wired DI/CSS, removed Bootstrap | Program.cs, App.razor, _Imports.razor, app.css, PickleIQ.Web.csproj | Build succeeded, committed | ~800 |
| 00:31 | Replaced MainLayout + NavMenu with MudBlazor — MudTheme green, MudAppBar top nav, MudAppBar bottom (mobile); MudBottomNavigation removed in v9 so replaced with bottom MudAppBar | MainLayout.razor, NavMenu.razor | Build succeeded 0 errors, committed | ~600 |
| 09:00 | Removed dead LocationChanged handler from NavMenu.razor; fixed Secondary color (#FFFFFF→#757575) and made _theme readonly in MainLayout.razor | NavMenu.razor, MainLayout.razor | Build succeeded, committed 63d90fc | ~300 |
| 00:30 | Migrated Home page from Bootstrap to MudBlazor | src/PickleIQ.Web/Components/Pages/Home.razor | Build succeeded, committed | ~2100 |
| 09:15 | Migrated Upload page to MudBlazor (MudText, MudPaper, MudButton, MudAlert, MudStack, MudProgressCircular); used InputFile hidden + JS interop to trigger file picker | src/PickleIQ.Web/Components/Pages/Upload.razor | Build succeeded 0 errors, committed 947f475 | ~700 |
| 09:30 | Fixed Upload.razor: replaced fragile JS eval+InputFile pattern with proper MudFileUpload (CustomContent+OpenFilePickerAsync); OnFileSelected now takes IBrowserFile; removed IJSRuntime | src/PickleIQ.Web/Components/Pages/Upload.razor | Build succeeded 0 errors, committed fb3aa81 | ~500 |
| 00:42 | Migrated Jobs.razor from Bootstrap to MudBlazor 9.5.0 | src/PickleIQ.Web/Components/Pages/Jobs.razor | Build succeeded, committed | ~800 |
| 00:46 | Applied 3 code quality fixes to Jobs.razor (StateHasChanged after Add, JobClient.Enqueue try/catch, @key on mobile foreach) | src/PickleIQ.Web/Components/Pages/Jobs.razor | success | ~800 |
| 00:48 | Task 6: migrated Results.razor from Bootstrap to MudBlazor | src/PickleIQ.Web/Components/Pages/Results.razor | Build succeeded 0 errors 0 warnings |
| 12:00 | Applied 3 bug fixes to Results.razor: DisposeTimerAsync helper, self-disposal race in timer callbacks (x2), redundant InvokeAsync in RetryAsync button handler | src/PickleIQ.Web/Components/Pages/Results.razor | Build succeeded 0 errors 0 warnings |
| 00:53 | Deleted Counter.razor and Weather.razor (leftover Blazor template pages) | src/PickleIQ.Web/Components/Pages/ | Build succeeded, 0 errors | ~50 tok |
| 16:41 | Task 1: Updated MatchSummary and ICoachingFrameSampler | ICoachingEngine.cs, ICoachingFrameSampler.cs, VideoProcessingJob.cs | SUCCESS: CoachingFrames param added, interface created, compile verified | ~2500 |
| 14:XX | Task: Parallelize VideoProcessingJob steps 2/3 with Task.WhenAll | VideoProcessingJob.cs | Build succeeded 0 errors 0 warnings; committed dcc5eda | ~800 |

## Session: 2026-06-02 21:32

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|

## Session: 2026-06-02 21:32

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|

## Session: 2026-06-02 21:35

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 21:36 | Edited src/PickleIQ.Web/Program.cs | 3→5 lines | ~40 |
| 21:37 | Session end: 1 writes across 1 files (Program.cs) | 1 reads | ~42 tok |
| 21:40 | Session end: 1 writes across 1 files (Program.cs) | 2 reads | ~42 tok |
| 21:43 | Session end: 1 writes across 1 files (Program.cs) | 2 reads | ~42 tok |
| 21:49 | Edited src/PickleIQ.Web/appsettings.json | 32768 → 4096 | ~8 |
| 21:50 | Session end: 2 writes across 2 files (Program.cs, appsettings.json) | 3 reads | ~50 tok |
| 21:50 | Session end: 2 writes across 2 files (Program.cs, appsettings.json) | 3 reads | ~50 tok |
| 21:54 | Session end: 2 writes across 2 files (Program.cs, appsettings.json) | 3 reads | ~50 tok |
| 21:54 | Session end: 2 writes across 2 files (Program.cs, appsettings.json) | 3 reads | ~50 tok |
| 21:57 | Session end: 2 writes across 2 files (Program.cs, appsettings.json) | 5 reads | ~300 tok |
| 22:00 | Created docs/superpowers/plans/2026-06-02-coaching-report-streaming.md | — | ~7451 |
| 22:00 | Session end: 3 writes across 3 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md) | 6 reads | ~8383 tok |
| 22:03 | Created src/PickleIQ.Core/Interfaces/ICoachingStreamService.cs | — | ~77 |
| 22:03 | Created src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs | — | ~286 |
| 22:06 | Task 1: Created ICoachingStreamService & CoachingStreamService for channel-based coaching report streaming | ICoachingStreamService.cs, CoachingStreamService.cs | Build succeeded 0 errors 0 warnings; committed 2171a75 | ~400 |
| 22:06 | Edited src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs | modified CreateStream() | ~84 |
| 22:06 | Edited src/PickleIQ.Infrastructure/Services/CoachingStreamService.cs | modified WriteChunk() | ~46 |
| 22:07 | Fix 2 code quality issues in CoachingStreamService: TryAdd instead of indexer, discard TryWrite result | CoachingStreamService.cs | Build succeeded 0 errors 0 warnings; committed 807dbe8 | ~200 |
| 22:08 | Edited src/PickleIQ.Core/Interfaces/ICoachingEngine.cs | 7→8 lines | ~69 |
| 22:08 | Edited src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs | 4→5 lines | ~63 |
| 22:08 | Edited src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs | added 1 condition(s) | ~115 |
| 22:09 | Task 2: Add onChunk callback to ICoachingEngine + OllamaVisionCoachingEngine streaming loop | ICoachingEngine.cs, OllamaVisionCoachingEngine.cs | Build succeeded 0 errors; committed fb37985 | ~400 |
| 22:11 | Edited src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs | modified VideoProcessingJob() | ~94 |
| 22:11 | Edited src/PickleIQ.Infrastructure/Jobs/VideoProcessingJob.cs | modified MatchSummary() | ~387 |
| 22:13 | Task 3: Wire ICoachingStreamService into VideoProcessingJob (add constructor param, wrap report generation in try/finally with streaming) | VideoProcessingJob.cs | Build succeeded 0 errors 0 warnings; committed 24afffd | ~400 |
| 22:13 | Edited src/PickleIQ.Web/Program.cs | 1→2 lines | ~37 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 6→9 lines | ~82 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | added 1 condition(s) | ~265 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 1→3 lines | ~36 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 9→10 lines | ~128 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | added 2 condition(s) | ~229 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | modified RetryAsync() | ~60 |
| 22:13 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 10→11 lines | ~132 |
| 22:15 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 5→4 lines | ~51 |
| 22:16 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | added error handling | ~244 |
| 22:16 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 4→5 lines | ~50 |
| 22:18 | Fix 3 threading/disposal bugs in Results.razor: InvokeAsync wraps mutation+StateHasChanged, try/catch OperationCanceledException in ConsumeStreamAsync, dispose old CTS in RetryAsync | Results.razor | Build succeeded 0 errors; committed f05dd2a | ~280 |
| 22:19 | Session end: 23 writes across 9 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 15 reads | ~22491 tok |
| 22:35 | Session end: 23 writes across 9 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 15 reads | ~22491 tok |
| 22:37 | Edited src/PickleIQ.Web/appsettings.json | 5→5 lines | ~34 |
| 22:37 | Edited src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs | 3→3 lines | ~71 |
| 22:38 | Session end: 25 writes across 9 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 15 reads | ~22601 tok |
| 22:38 | Session end: 25 writes across 9 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 15 reads | ~22601 tok |
| 22:40 | Created docs/superpowers/plans/2026-06-02-job-status-push.md | — | ~5457 |
| 22:41 | Session end: 26 writes across 10 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 15 reads | ~28448 tok |
| 22:45 | Created IJobStatusService interface and JobStatusService implementation | IJobStatusService.cs, JobStatusService.cs | Task 1 completed: Channel-based singleton for pushing job status updates to UI | ~500 |
| 22:46 | Edited src/PickleIQ.Web/Program.cs | 1→2 lines | ~42 |
| 22:46 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | 1→2 lines | ~26 |
| 22:47 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | modified OnInitializedAsync() | ~2138 |
| 22:49 | Edited src/PickleIQ.Web/Components/Pages/Results.razor | modified DisposeAsync() | ~82 |
| 22:49 | Session end: 30 writes across 10 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 17 reads | ~31380 tok |
| 22:51 | Session end: 30 writes across 10 files (Program.cs, appsettings.json, 2026-06-02-coaching-report-streaming.md, ICoachingStreamService.cs, CoachingStreamService.cs) | 17 reads | ~31380 tok |

## Session: 2026-06-02 22:55

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 22:56 | Edited src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs | added 1 condition(s) | ~161 |
| 22:56 | Edited src/PickleIQ.Infrastructure/AI/OllamaVisionCoachingEngine.cs | modified StripSpecialTokens() | ~116 |
| 22:56 | Fixed coaching report showing Qwen special tokens (<|im_start|>) by adding StripSpecialTokens() to OllamaVisionCoachingEngine | OllamaVisionCoachingEngine.cs | bug-007 logged | ~200 |
| 22:56 | Session end: 2 writes across 1 files (OllamaVisionCoachingEngine.cs) | 4 reads | ~1602 tok |
| 23:06 | Edited src/PickleIQ.Infrastructure/Services/CoachingFrameSampler.cs | 8→8 lines | ~140 |
| 23:06 | Edited src/PickleIQ.Web/appsettings.json | 4096 → 8192 | ~8 |
| 23:07 | Root-caused blank coaching report: 1280px frames exhausted 4096 ctx window; fixed by 640px frames + 8192 ctx | CoachingFrameSampler.cs, appsettings.json, OllamaVisionCoachingEngine.cs | bug-007 updated | ~400 |
| 23:07 | Session end: 4 writes across 3 files (OllamaVisionCoachingEngine.cs, CoachingFrameSampler.cs, appsettings.json) | 8 reads | ~3036 tok |
| 23:16 | Session end: 4 writes across 3 files (OllamaVisionCoachingEngine.cs, CoachingFrameSampler.cs, appsettings.json) | 8 reads | ~3036 tok |
| 23:16 | Edited src/PickleIQ.Web/appsettings.json | 8192 → 12288 | ~8 |
| 23:16 | Session end: 5 writes across 3 files (OllamaVisionCoachingEngine.cs, CoachingFrameSampler.cs, appsettings.json) | 8 reads | ~3044 tok |
| 23:17 | Edited README.md | inline fix | ~46 |
| 23:17 | Edited README.md | inline fix | ~18 |
| 23:17 | Edited README.md | pulled() → VRAM() | ~70 |
| 23:18 | Edited README.md | 9→10 lines | ~211 |
| 23:18 | Edited README.md | expanded (+15 lines) | ~243 |
| 23:18 | Edited README.md | 6→1 lines | ~28 |
| 23:18 | Session end: 11 writes across 4 files (OllamaVisionCoachingEngine.cs, CoachingFrameSampler.cs, appsettings.json, README.md) | 9 reads | ~3702 tok |
