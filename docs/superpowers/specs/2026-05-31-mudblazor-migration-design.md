# MudBlazor Migration Design

## Goal
Replace Bootstrap with MudBlazor across all pages, delivering a mobile-first UI with dark green theme, a bottom tab bar on mobile, and full app bar on desktop.

## Theme
- **Primary:** `#2E7D32` (dark green — pickleball court)
- **Secondary:** `#FFFFFF`
- **Surface/background:** `#F5F5F5`
- **AppBar:** dark green background, white text/icons

## Navigation
- `MudAppBar` on all screen sizes — PickleIQ logo + title
- Desktop: nav links rendered inside the app bar (`MudButton`/`MudNavLink`)
- Mobile (xs/sm): `MudBottomNavigation` with two items — Upload Video, My Videos
- No sidebar/drawer needed

## Pages

### Home
Full-screen hero: large `MudText` heading, tagline, `MudButton` size Large "Upload Video" CTA. Below the fold: 3 `MudCard` feature tiles (Rally Detection, Highlight Reel, Coaching Report) in a responsive `MudGrid` (3-col on md+, 1-col on xs).

### Upload
Centered `MudPaper` card. `MudFileUpload` for file selection, `MudText` shows selected file name and size, `MudButton` to submit. `MudProgressCircular` + overlay during upload. `MudAlert` for errors.

### My Videos (Jobs)
- Desktop: `MudTable` with columns: File, Status, Uploaded, Actions
- Mobile: stacked `MudCard` list (using `MudHidden` to switch)
- Status shown as `MudChip` with color matching state
- Action buttons: View Results (`Outlined`), Reprocess (`Warning`), Delete (`Error`)

### Results
- While processing: `MudProgressLinear` (striped, animated) + `MudText` status message
- On failure: `MudAlert` severity Error + Retry button
- On complete:
  - 3 stat `MudPaper` tiles (Rallies, Avg Rally, Longest Rally) in a `MudGrid`
  - File info in `MudSimpleTable` (Source Video + Highlight Reel rows with path, size, date)
  - Download `MudButton` color Success
  - Coaching report in `MudPaper` with Markdig-rendered HTML

## Layout
- `MudThemeProvider` + `MudSnackbarProvider` + `MudDialogProvider` in `MainLayout`
- `MudLayout` → `MudAppBar` → `MudMainContent` wrapping `@Body`
- `MudContainer MaxWidth="MaxWidth.Large"` wraps page content
- Remove Bootstrap CSS entirely from `App.razor`
- Remove leftover Blazor template pages: Counter.razor, Weather.razor

## Files Changed
- `PickleIQ.Web.csproj` — add MudBlazor package
- `Program.cs` — add `builder.Services.AddMudServices()`
- `Components/App.razor` — remove Bootstrap, add MudBlazor CSS/font/JS
- `Components/_Imports.razor` — add `@using MudBlazor`
- `Components/Layout/MainLayout.razor` — replace with MudLayout
- `Components/Layout/NavMenu.razor` — replace with MudAppBar + MudBottomNavigation
- `Components/Pages/Home.razor` — hero + feature cards
- `Components/Pages/Upload.razor` — MudFileUpload flow
- `Components/Pages/Jobs.razor` — MudTable/MudCard list
- `Components/Pages/Results.razor` — progress, stats, file info, coaching report
- `wwwroot/app.css` — remove Bootstrap overrides, keep only MudBlazor custom CSS
