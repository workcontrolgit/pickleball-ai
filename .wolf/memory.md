# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.
| Task3 | Created QwenVisionCoachingEngine (vision+stats coaching via Qwen2-VL), deleted OllamaCoachingEngine | QwenVisionCoachingEngine.cs | committed; Program.cs ref pending Task5 | ~600 |
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
