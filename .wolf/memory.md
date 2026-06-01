# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.
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
