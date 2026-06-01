# MudBlazor Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Bootstrap with MudBlazor across all pages, delivering a mobile-first dark-green themed UI with bottom tab nav on mobile and app bar nav on desktop.

**Architecture:** Install MudBlazor, wire up theme/DI/layout in the shell files, then migrate each page independently. Bootstrap is removed entirely — no hybrid. Each task is independently deployable.

**Tech Stack:** .NET 10, Blazor Server, MudBlazor 7.x, Markdig (already installed)

---

## File Map

| File | Change |
|------|--------|
| `src/PickleIQ.Web/PickleIQ.Web.csproj` | Add MudBlazor NuGet |
| `src/PickleIQ.Web/Program.cs` | Add `AddMudServices()` |
| `src/PickleIQ.Web/Components/App.razor` | Swap Bootstrap for MudBlazor CSS/JS |
| `src/PickleIQ.Web/Components/_Imports.razor` | Add `@using MudBlazor` |
| `src/PickleIQ.Web/Components/Layout/MainLayout.razor` | Replace with MudLayout + theme providers |
| `src/PickleIQ.Web/Components/Layout/NavMenu.razor` | Replace with MudAppBar + MudBottomNavigation |
| `src/PickleIQ.Web/wwwroot/app.css` | Strip Bootstrap overrides, minimal MudBlazor resets |
| `src/PickleIQ.Web/Components/Pages/Home.razor` | Hero + feature cards |
| `src/PickleIQ.Web/Components/Pages/Upload.razor` | MudFileUpload flow |
| `src/PickleIQ.Web/Components/Pages/Jobs.razor` | MudTable desktop / MudCard mobile |
| `src/PickleIQ.Web/Components/Pages/Results.razor` | Progress, stats, file info, coaching report |
| `src/PickleIQ.Web/Components/Pages/Counter.razor` | Delete |
| `src/PickleIQ.Web/Components/Pages/Weather.razor` | Delete |

---

### Task 1: Install MudBlazor and wire up DI + shell

**Files:**
- Modify: `src/PickleIQ.Web/PickleIQ.Web.csproj`
- Modify: `src/PickleIQ.Web/Program.cs`
- Modify: `src/PickleIQ.Web/Components/App.razor`
- Modify: `src/PickleIQ.Web/Components/_Imports.razor`
- Modify: `src/PickleIQ.Web/wwwroot/app.css`

- [ ] **Step 1: Add MudBlazor package**

```bash
cd src/PickleIQ.Web
dotnet add package MudBlazor
```

Expected: `PackageReference for package 'MudBlazor' version '7.x.x' added`

- [ ] **Step 2: Add MudServices to DI in Program.cs**

In `src/PickleIQ.Web/Program.cs`, add after the existing `builder.Services.AddRazorComponents()` block:

```csharp
builder.Services.AddMudServices();
```

- [ ] **Step 3: Replace App.razor head — remove Bootstrap, add MudBlazor**

Replace the entire content of `src/PickleIQ.Web/Components/App.razor`:

```razor
<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <ResourcePreloader />
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="@Assets["app.css"]" />
    <link rel="stylesheet" href="@Assets["PickleIQ.Web.styles.css"]" />
    <ImportMap />
    <link rel="icon" type="image/png" href="favicon.png" />
    <HeadOutlet />
</head>

<body>
    <Routes />
    <ReconnectModal />
    <script src="@Assets["_framework/blazor.web.js"]"></script>
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>

</html>
```

- [ ] **Step 4: Add MudBlazor using to _Imports.razor**

Add to the bottom of `src/PickleIQ.Web/Components/_Imports.razor`:

```razor
@using MudBlazor
```

- [ ] **Step 5: Clear app.css**

Replace `src/PickleIQ.Web/wwwroot/app.css` with:

```css
/* MudBlazor custom overrides */
.coaching-report h2 { margin-top: 1.5rem; margin-bottom: .5rem; font-size: 1.25rem; font-weight: 600; }
.coaching-report ul { padding-left: 1.5rem; }
.coaching-report li { margin-bottom: .25rem; }
.coaching-report blockquote { border-left: 4px solid #ccc; padding-left: 1rem; color: #666; }
```

- [ ] **Step 6: Build to verify no errors**

```bash
cd src/PickleIQ.Web
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add src/PickleIQ.Web/PickleIQ.Web.csproj src/PickleIQ.Web/Program.cs src/PickleIQ.Web/Components/App.razor src/PickleIQ.Web/Components/_Imports.razor src/PickleIQ.Web/wwwroot/app.css
git commit -m "feat: install MudBlazor and wire up DI/CSS"
```

---

### Task 2: Replace layout — MainLayout + NavMenu

**Files:**
- Modify: `src/PickleIQ.Web/Components/Layout/MainLayout.razor`
- Modify: `src/PickleIQ.Web/Components/Layout/NavMenu.razor`

- [ ] **Step 1: Replace MainLayout.razor**

Replace entire content of `src/PickleIQ.Web/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<MudThemeProvider Theme="_theme" />
<MudSnackbarProvider />
<MudDialogProvider />

<MudLayout>
    <NavMenu />
    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="py-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    private MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2E7D32",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#FFFFFF",
            AppbarBackground = "#2E7D32",
            AppbarText = "#FFFFFF",
            Background = "#F5F5F5",
            Surface = "#FFFFFF",
        }
    };
}
```

- [ ] **Step 2: Replace NavMenu.razor**

Replace entire content of `src/PickleIQ.Web/Components/Layout/NavMenu.razor`:

```razor
@inject NavigationManager Navigation
@implements IDisposable

<MudAppBar Elevation="1" Color="Color.Primary">
    <MudText Typo="Typo.h6" Class="ml-2" Style="font-weight:700;letter-spacing:.5px;">🥒 PickleIQ</MudText>
    <MudSpacer />
    <MudHidden Breakpoint="Breakpoint.SmAndDown">
        <MudButton Href="/upload" Color="Color.Inherit" StartIcon="@Icons.Material.Filled.CloudUpload">Upload Video</MudButton>
        <MudButton Href="/jobs" Color="Color.Inherit" StartIcon="@Icons.Material.Filled.VideoLibrary">My Videos</MudButton>
    </MudHidden>
</MudAppBar>

<MudHidden Breakpoint="Breakpoint.MdAndUp">
    <MudBottomNavigation @bind-SelectedIndex="_selectedIndex" Color="Color.Primary" Fixed="true">
        <MudBottomNavigationItem Title="Upload" Icon="@Icons.Material.Filled.CloudUpload" Href="/upload" />
        <MudBottomNavigationItem Title="My Videos" Icon="@Icons.Material.Filled.VideoLibrary" Href="/jobs" />
    </MudBottomNavigation>
</MudHidden>

@code {
    private int _selectedIndex = 0;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        SetSelectedFromUrl(Navigation.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        => SetSelectedFromUrl(e.Location);

    private void SetSelectedFromUrl(string url)
    {
        if (url.Contains("/upload")) _selectedIndex = 0;
        else if (url.Contains("/jobs")) _selectedIndex = 1;
    }

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/PickleIQ.Web/Components/Layout/MainLayout.razor src/PickleIQ.Web/Components/Layout/NavMenu.razor
git commit -m "feat: replace layout with MudBlazor — AppBar + bottom nav"
```

---

### Task 3: Migrate Home page

**Files:**
- Modify: `src/PickleIQ.Web/Components/Pages/Home.razor`

- [ ] **Step 1: Replace Home.razor**

Replace entire content of `src/PickleIQ.Web/Components/Pages/Home.razor`:

```razor
@page "/"

<PageTitle>PickleIQ — AI Pickleball Analysis</PageTitle>

<MudPaper Elevation="0" Class="pa-8 mb-6 rounded-lg" Style="background: linear-gradient(135deg, #2E7D32 0%, #1B5E20 100%); color: white;">
    <MudText Typo="Typo.h3" Style="font-weight:800; color:white;" Class="mb-2">PickleIQ</MudText>
    <MudText Typo="Typo.h6" Style="color:rgba(255,255,255,0.85); font-weight:400;" Class="mb-6">
        Upload a match video. Get a highlight reel and AI coaching report — no coach required.
    </MudText>
    <MudStack Row="true" Spacing="2">
        <MudButton Href="/upload" Variant="Variant.Filled" Size="Size.Large"
                   Style="background:white; color:#2E7D32; font-weight:700;">
            Upload a Video
        </MudButton>
        <MudButton Href="/jobs" Variant="Variant.Outlined" Size="Size.Large"
                   Style="border-color:white; color:white;">
            My Videos
        </MudButton>
    </MudStack>
</MudPaper>

<MudGrid Spacing="3">
    <MudItem xs="12" sm="4">
        <MudCard Elevation="2" Class="h-100">
            <MudCardContent>
                <MudText Typo="Typo.h4" Class="mb-2">🏓</MudText>
                <MudText Typo="Typo.h6" Class="mb-1">Rally Detection</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">
                    YOLO AI identifies every active rally in your match footage.
                </MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
    <MudItem xs="12" sm="4">
        <MudCard Elevation="2" Class="h-100">
            <MudCardContent>
                <MudText Typo="Typo.h4" Class="mb-2">🎬</MudText>
                <MudText Typo="Typo.h6" Class="mb-1">Highlight Reel</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">
                    Top rallies auto-cut into a 60-second highlight you can share.
                </MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
    <MudItem xs="12" sm="4">
        <MudCard Elevation="2" Class="h-100">
            <MudCardContent>
                <MudText Typo="Typo.h4" Class="mb-2">📊</MudText>
                <MudText Typo="Typo.h6" Class="mb-1">Coaching Report</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">
                    AI-generated feedback on strengths, improvements, and drills.
                </MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
</MudGrid>
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/Components/Pages/Home.razor
git commit -m "feat: migrate Home page to MudBlazor"
```

---

### Task 4: Migrate Upload page

**Files:**
- Modify: `src/PickleIQ.Web/Components/Pages/Upload.razor`

- [ ] **Step 1: Replace Upload.razor**

Replace entire content of `src/PickleIQ.Web/Components/Pages/Upload.razor`:

```razor
@page "/upload"
@using PickleIQ.Core.Interfaces
@inject IVideoStorageService VideoStorageService
@inject NavigationManager Navigation
@rendermode InteractiveServer

<PageTitle>Upload Match Video — PickleIQ</PageTitle>

<MudText Typo="Typo.h4" Class="mb-1">Upload Your Match Video</MudText>
<MudText Typo="Typo.body1" Color="Color.Secondary" Class="mb-6">
    Upload an MP4 of your match and receive an AI-generated highlight reel and coaching report.
</MudText>

<MudPaper MaxWidth="600px" Class="pa-6 mx-auto" Elevation="2">
    @if (!_uploading)
    {
        <MudFileUpload T="IBrowserFile" FilesChanged="OnFileSelected" Accept=".mp4,.MP4,.mov,.MOV">
            <ActivatorContent>
                <MudButton Variant="Variant.Outlined" Color="Color.Primary"
                           StartIcon="@Icons.Material.Filled.CloudUpload" FullWidth="true"
                           Size="Size.Large" Class="mb-3" Style="height:80px;">
                    Click to select video file
                </MudButton>
            </ActivatorContent>
        </MudFileUpload>

        @if (_selectedFile is not null)
        {
            <MudText Class="mb-2">
                Selected: <strong>@_selectedFile.Name</strong> (@FormatBytes(_selectedFile.Size))
            </MudText>

            @if (_selectedFile.Size > MaxFileSizeBytes)
            {
                <MudAlert Severity="Severity.Error" Class="mb-3">
                    File exceeds the 2GB limit. Please select a smaller file.
                </MudAlert>
            }
            else
            {
                <MudButton Variant="Variant.Filled" Color="Color.Primary" FullWidth="true"
                           Size="Size.Large" OnClick="UploadAsync"
                           StartIcon="@Icons.Material.Filled.Analytics">
                    Analyze Video
                </MudButton>
            }
        }

        @if (_errorMessage is not null)
        {
            <MudAlert Severity="Severity.Error" Class="mt-4">@_errorMessage</MudAlert>
        }
    }
    else
    {
        <MudStack AlignItems="AlignItems.Center" Spacing="4" Class="py-4">
            <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
            <MudText>Uploading your video, please wait...</MudText>
        </MudStack>
    }
</MudPaper>

@code {
    private const long MaxFileSizeBytes = 2L * 1024 * 1024 * 1024;

    private IBrowserFile? _selectedFile;
    private bool _uploading;
    private string? _errorMessage;

    private void OnFileSelected(IBrowserFile file)
    {
        _selectedFile = file;
        _errorMessage = null;
    }

    private async Task UploadAsync()
    {
        if (_selectedFile is null) return;

        _uploading = true;
        _errorMessage = null;

        try
        {
            await using var stream = _selectedFile.OpenReadStream(MaxFileSizeBytes);
            var jobId = await VideoStorageService.SaveAsync(stream, _selectedFile.Name, _selectedFile.Size);
            Navigation.NavigateTo($"/results/{jobId}");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Upload failed: {ex.Message}";
            _uploading = false;
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
        _ => $"{bytes / (1024L * 1024 * 1024):F1} GB"
    };
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/Components/Pages/Upload.razor
git commit -m "feat: migrate Upload page to MudBlazor"
```

---

### Task 5: Migrate My Videos (Jobs) page

**Files:**
- Modify: `src/PickleIQ.Web/Components/Pages/Jobs.razor`

- [ ] **Step 1: Replace Jobs.razor**

Replace entire content of `src/PickleIQ.Web/Components/Pages/Jobs.razor`:

```razor
@page "/jobs"
@using Hangfire
@using Microsoft.EntityFrameworkCore
@using PickleIQ.Core.Entities
@using PickleIQ.Infrastructure.Data
@using PickleIQ.Infrastructure.Jobs
@inject AppDbContext Db
@inject IBackgroundJobClient JobClient
@rendermode InteractiveServer

<PageTitle>My Videos — PickleIQ</PageTitle>

<MudStack Row="true" AlignItems="AlignItems.Center" Justify="Justify.SpaceBetween" Class="mb-4">
    <MudText Typo="Typo.h4">My Videos</MudText>
    <MudButton Href="/upload" Variant="Variant.Filled" Color="Color.Primary"
               StartIcon="@Icons.Material.Filled.CloudUpload">
        Upload Video
    </MudButton>
</MudStack>

@if (_jobs is null)
{
    <MudStack AlignItems="AlignItems.Center" Class="mt-8">
        <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
    </MudStack>
}
else if (_jobs.Count == 0)
{
    <MudStack AlignItems="AlignItems.Center" Class="mt-8" Spacing="4">
        <MudText Color="Color.Secondary">No videos uploaded yet.</MudText>
        <MudButton Href="/upload" Variant="Variant.Filled" Color="Color.Primary">Upload your first video</MudButton>
    </MudStack>
}
else
{
    <!-- Desktop table -->
    <MudHidden Breakpoint="Breakpoint.SmAndDown">
        <MudTable Items="_jobs" Hover="true" Elevation="2">
            <HeaderContent>
                <MudTh>File</MudTh>
                <MudTh>Status</MudTh>
                <MudTh>Uploaded</MudTh>
                <MudTh Style="text-align:right">Actions</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>
                    <MudText Typo="Typo.body2" Style="font-weight:600">@context.FileName</MudText>
                </MudTd>
                <MudTd>
                    <MudChip T="string" Color="@ChipColor(context.Status)" Size="Size.Small">
                        @StatusLabel(context.Status)
                    </MudChip>
                </MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" Color="Color.Secondary">
                        @context.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                    </MudText>
                </MudTd>
                <MudTd Style="text-align:right">
                    @ActionButtons(context)
                </MudTd>
            </RowTemplate>
        </MudTable>
    </MudHidden>

    <!-- Mobile cards -->
    <MudHidden Breakpoint="Breakpoint.MdAndUp">
        <MudStack Spacing="3">
            @foreach (var job in _jobs)
            {
                <MudCard Elevation="2">
                    <MudCardContent>
                        <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center" Class="mb-1">
                            <MudText Typo="Typo.body1" Style="font-weight:600">@job.FileName</MudText>
                            <MudChip T="string" Color="@ChipColor(job.Status)" Size="Size.Small">
                                @StatusLabel(job.Status)
                            </MudChip>
                        </MudStack>
                        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-3">
                            @job.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                        </MudText>
                        <MudStack Row="true" Spacing="1" Wrap="Wrap.Wrap">
                            @ActionButtons(job)
                        </MudStack>
                    </MudCardContent>
                </MudCard>
            }
        </MudStack>
    </MudHidden>
}

@code {
    private List<VideoJob>? _jobs;
    private readonly HashSet<Guid> _retrying = [];
    private readonly HashSet<Guid> _deleting = [];

    protected override async Task OnInitializedAsync() => await LoadJobsAsync();

    private async Task LoadJobsAsync()
    {
        _jobs = await Db.VideoJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    private RenderFragment ActionButtons(VideoJob job) => __builder =>
    {
        if (job.Status == VideoJobStatus.ReportComplete)
        {
            <MudButton Href="@($"/results/{job.Id}")" Variant="Variant.Outlined" Color="Color.Success" Size="Size.Small" Class="mr-1">View Results</MudButton>
            @if (!string.IsNullOrEmpty(job.HighlightFilePath))
            {
                <MudButton Href="@($"/download/{job.Id}/highlights")" Variant="Variant.Filled" Color="Color.Success" Size="Size.Small" Class="mr-1">Download</MudButton>
            }
            <MudButton OnClick="@(() => RetryAsync(job.Id))" Variant="Variant.Outlined" Color="Color.Warning" Size="Size.Small" Class="mr-1"
                       Disabled="_retrying.Contains(job.Id)">
                @(_retrying.Contains(job.Id) ? "Retrying…" : "Reprocess")
            </MudButton>
            <MudButton OnClick="@(() => DeleteAsync(job.Id))" Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small"
                       Disabled="_deleting.Contains(job.Id)">Delete</MudButton>
        }
        else if (job.Status == VideoJobStatus.Failed)
        {
            <MudButton OnClick="@(() => RetryAsync(job.Id))" Variant="Variant.Filled" Color="Color.Warning" Size="Size.Small" Class="mr-1"
                       Disabled="_retrying.Contains(job.Id)">
                @(_retrying.Contains(job.Id) ? "Retrying…" : "Retry")
            </MudButton>
            <MudButton Href="@($"/results/{job.Id}")" Variant="Variant.Outlined" Color="Color.Default" Size="Size.Small" Class="mr-1">Details</MudButton>
            <MudButton OnClick="@(() => DeleteAsync(job.Id))" Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small"
                       Disabled="_deleting.Contains(job.Id)">Delete</MudButton>
        }
        else
        {
            <MudButton Href="@($"/results/{job.Id}")" Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small">View Progress</MudButton>
        }
    };

    private async Task RetryAsync(Guid jobId)
    {
        _retrying.Add(jobId);
        var job = await Db.VideoJobs.FindAsync(jobId);
        if (job is not null && (job.Status == VideoJobStatus.Failed || job.Status == VideoJobStatus.ReportComplete))
        {
            Db.RallySegments.RemoveRange(Db.RallySegments.Where(s => s.VideoJobId == jobId));
            var report = await Db.CoachingReports.FirstOrDefaultAsync(r => r.VideoJobId == jobId);
            if (report is not null) Db.CoachingReports.Remove(report);
            job.Status = VideoJobStatus.Queued;
            job.ErrorMessage = null;
            job.HighlightFilePath = null;
            job.CompletedAt = null;
            await Db.SaveChangesAsync();
            JobClient.Enqueue<VideoProcessingJob>(j => j.ProcessAsync(jobId));
        }
        _retrying.Remove(jobId);
        await LoadJobsAsync();
    }

    private async Task DeleteAsync(Guid jobId)
    {
        _deleting.Add(jobId);
        var job = await Db.VideoJobs.FindAsync(jobId);
        if (job is not null)
        {
            Db.RallySegments.RemoveRange(Db.RallySegments.Where(s => s.VideoJobId == jobId));
            var report = await Db.CoachingReports.FirstOrDefaultAsync(r => r.VideoJobId == jobId);
            if (report is not null) Db.CoachingReports.Remove(report);
            Db.VideoJobs.Remove(job);
            await Db.SaveChangesAsync();
            DeleteFileIfExists(job.FilePath);
            DeleteFileIfExists(job.HighlightFilePath);
        }
        _deleting.Remove(jobId);
        await LoadJobsAsync();
    }

    private static void DeleteFileIfExists(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (File.Exists(normalized)) File.Delete(normalized);
    }

    private static string StatusLabel(VideoJobStatus status) => status switch
    {
        VideoJobStatus.Queued => "Queued",
        VideoJobStatus.RallyDetectionInProgress => "Detecting Rallies",
        VideoJobStatus.RallyDetectionComplete => "Rallies Found",
        VideoJobStatus.HighlightInProgress => "Creating Highlights",
        VideoJobStatus.HighlightComplete => "Highlights Ready",
        VideoJobStatus.ReportInProgress => "Generating Report",
        VideoJobStatus.ReportComplete => "Complete",
        VideoJobStatus.Failed => "Failed",
        _ => "Processing"
    };

    private static Color ChipColor(VideoJobStatus status) => status switch
    {
        VideoJobStatus.ReportComplete => Color.Success,
        VideoJobStatus.Failed => Color.Error,
        VideoJobStatus.Queued => Color.Default,
        _ => Color.Primary
    };
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/Components/Pages/Jobs.razor
git commit -m "feat: migrate Jobs page to MudBlazor"
```

---

### Task 6: Migrate Results page

**Files:**
- Modify: `src/PickleIQ.Web/Components/Pages/Results.razor`

- [ ] **Step 1: Replace Results.razor**

Replace entire content of `src/PickleIQ.Web/Components/Pages/Results.razor`:

```razor
@page "/results/{JobId:guid}"
@using Microsoft.EntityFrameworkCore
@using PickleIQ.Core.Entities
@using PickleIQ.Infrastructure.Data
@using Hangfire
@using PickleIQ.Infrastructure.Jobs
@using Markdig
@inject AppDbContext Db
@inject NavigationManager Navigation
@inject IBackgroundJobClient JobClient
@implements IAsyncDisposable
@rendermode InteractiveServer

<PageTitle>Results — PickleIQ</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Match Results</MudText>

@if (_job is null)
{
    <MudStack AlignItems="AlignItems.Center" Class="mt-8" Spacing="3">
        <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
        <MudText Color="Color.Secondary">Loading job status...</MudText>
    </MudStack>
}
else if (_job.Status == VideoJobStatus.Failed)
{
    <MudAlert Severity="Severity.Error" Class="mb-4">
        <MudText Typo="Typo.h6">Processing failed</MudText>
        <MudText>@(_job.ErrorMessage ?? "An unexpected error occurred.")</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">Job ID: @JobId</MudText>
    </MudAlert>
    <MudStack Row="true" Spacing="2">
        <MudButton Variant="Variant.Filled" Color="Color.Warning" OnClick="RetryAsync" Disabled="_retrying">
            @(_retrying ? "Retrying…" : "Retry")
        </MudButton>
        <MudButton Href="/upload" Variant="Variant.Filled" Color="Color.Primary">Try another video</MudButton>
    </MudStack>
}
else if (_job.Status != VideoJobStatus.ReportComplete)
{
    <MudStack AlignItems="AlignItems.Center" Class="mt-6" Spacing="4">
        <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
        <MudText Color="Color.Secondary">@StatusMessage(_job.Status)</MudText>
        <MudProgressLinear Color="Color.Primary" Striped="true" Rounded="true"
                           Value="@ProgressPercent(_job.Status)" Class="my-2" Style="width:100%;max-width:400px;" />
        <MudText Typo="Typo.body2" Color="Color.Secondary">Job ID: @JobId</MudText>
    </MudStack>
}
else
{
    <!-- Rally Statistics -->
    <MudText Typo="Typo.h5" Class="mb-3">Rally Statistics</MudText>
    <MudGrid Spacing="3" Class="mb-4">
        <MudItem xs="12" sm="4">
            <MudPaper Elevation="2" Class="pa-4 text-center">
                <MudText Typo="Typo.h3" Color="Color.Primary">@_segments.Count</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">Rallies Detected</MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="12" sm="4">
            <MudPaper Elevation="2" Class="pa-4 text-center">
                <MudText Typo="Typo.h3" Color="Color.Primary">
                    @(_segments.Count > 0 ? _segments.Average(s => s.DurationSeconds).ToString("F1") : "—")s
                </MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">Avg Rally Length</MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="12" sm="4">
            <MudPaper Elevation="2" Class="pa-4 text-center">
                <MudText Typo="Typo.h3" Color="Color.Primary">
                    @(_segments.Count > 0 ? _segments.Max(s => s.DurationSeconds).ToString("F1") : "—")s
                </MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">Longest Rally</MudText>
            </MudPaper>
        </MudItem>
    </MudGrid>

    @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
    {
        <MudButton Href="@DownloadUrl" Variant="Variant.Filled" Color="Color.Success" Class="mb-4"
                   StartIcon="@Icons.Material.Filled.Download">
            Download Highlight Reel
        </MudButton>
    }

    <!-- File Info -->
    <MudText Typo="Typo.h5" Class="mb-2 mt-4">File Info</MudText>
    <MudSimpleTable Elevation="1" Class="mb-4">
        <thead>
            <tr>
                <th>File</th>
                <th>Path on Disk</th>
                <th>Size</th>
                <th>Date</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td><MudText Typo="Typo.body2" Color="Color.Secondary">Source Video</MudText></td>
                <td><MudText Typo="Typo.body2" Style="font-family:monospace;word-break:break-all;">@_job.FilePath</MudText></td>
                <td><MudText Typo="Typo.body2">@FormatSize(_sourceSize)</MudText></td>
                <td><MudText Typo="Typo.body2">@_sourceDate?.ToLocalTime().ToString("MMM d, yyyy h:mm tt")</MudText></td>
            </tr>
            @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
            {
                <tr>
                    <td><MudText Typo="Typo.body2" Color="Color.Secondary">Highlight Reel</MudText></td>
                    <td><MudText Typo="Typo.body2" Style="font-family:monospace;word-break:break-all;">@_job.HighlightFilePath</MudText></td>
                    <td><MudText Typo="Typo.body2">@FormatSize(_highlightSize)</MudText></td>
                    <td><MudText Typo="Typo.body2">@_highlightDate?.ToLocalTime().ToString("MMM d, yyyy h:mm tt")</MudText></td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>

    <!-- Coaching Report -->
    <MudText Typo="Typo.h5" Class="mb-2 mt-4">Coaching Report</MudText>
    @if (_report is not null)
    {
        <MudPaper Elevation="1" Class="pa-4 mb-4">
            <div class="coaching-report">@((MarkupString)Markdown.ToHtml(_report.HtmlContent, _markdigPipeline))</div>
        </MudPaper>
    }
    else
    {
        <MudText Color="Color.Secondary">Coaching report not available.</MudText>
    }

    <MudStack Row="true" Spacing="2" Class="mt-4" Wrap="Wrap.Wrap">
        <MudButton Href="/upload" Variant="Variant.Outlined" Color="Color.Primary">Analyze another video</MudButton>
        @if (!string.IsNullOrEmpty(_job.HighlightFilePath))
        {
            <MudButton Href="@DownloadUrl" Variant="Variant.Filled" Color="Color.Success"
                       StartIcon="@Icons.Material.Filled.Download">
                Download Highlight Reel
            </MudButton>
        }
        <MudButton Variant="Variant.Outlined" Color="Color.Warning" OnClick="RetryAsync" Disabled="_retrying">
            @(_retrying ? "Reprocessing…" : "Reprocess")
        </MudButton>
    </MudStack>
}

@code {
    [Parameter] public Guid JobId { get; set; }

    private static readonly MarkdownPipeline _markdigPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private VideoJob? _job;
    private List<RallySegment> _segments = [];
    private CoachingReport? _report;
    private System.Threading.Timer? _pollTimer;
    private bool _retrying;

    private long? _sourceSize;
    private DateTime? _sourceDate;
    private long? _highlightSize;
    private DateTime? _highlightDate;

    private string DownloadUrl => $"/download/{JobId}/highlights";

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();

        if (_job?.Status != VideoJobStatus.ReportComplete && _job?.Status != VideoJobStatus.Failed)
        {
            _pollTimer = new System.Threading.Timer(async _ =>
            {
                await RefreshAsync();
                await InvokeAsync(StateHasChanged);
                if (_job?.Status == VideoJobStatus.ReportComplete || _job?.Status == VideoJobStatus.Failed)
                    await _pollTimer!.DisposeAsync();
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
    }

    private async Task RefreshAsync()
    {
        _job = await Db.VideoJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == JobId);

        if (_job?.Status == VideoJobStatus.ReportComplete)
        {
            _segments = await Db.RallySegments.AsNoTracking()
                .Where(s => s.VideoJobId == JobId)
                .ToListAsync();

            _report = await Db.CoachingReports.AsNoTracking()
                .FirstOrDefaultAsync(r => r.VideoJobId == JobId);

            LoadFileInfo(_job);
        }
    }

    private void LoadFileInfo(VideoJob job)
    {
        if (File.Exists(job.FilePath))
        {
            var info = new FileInfo(job.FilePath);
            _sourceSize = info.Length;
            _sourceDate = info.LastWriteTimeUtc;
        }
        if (!string.IsNullOrEmpty(job.HighlightFilePath) && File.Exists(job.HighlightFilePath))
        {
            var info = new FileInfo(job.HighlightFilePath);
            _highlightSize = info.Length;
            _highlightDate = info.LastWriteTimeUtc;
        }
    }

    private static string FormatSize(long? bytes) => bytes switch
    {
        null => "—",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private static string StatusMessage(VideoJobStatus status) => status switch
    {
        VideoJobStatus.Queued => "Queued — waiting to start...",
        VideoJobStatus.RallyDetectionInProgress => "Detecting rallies...",
        VideoJobStatus.RallyDetectionComplete => "Rallies detected — generating highlights...",
        VideoJobStatus.HighlightInProgress => "Creating highlight reel...",
        VideoJobStatus.HighlightComplete => "Highlights ready — generating coaching report...",
        VideoJobStatus.ReportInProgress => "Generating AI coaching report...",
        _ => "Processing..."
    };

    private static int ProgressPercent(VideoJobStatus status) => status switch
    {
        VideoJobStatus.Queued => 5,
        VideoJobStatus.RallyDetectionInProgress => 25,
        VideoJobStatus.RallyDetectionComplete => 50,
        VideoJobStatus.HighlightInProgress => 65,
        VideoJobStatus.HighlightComplete => 80,
        VideoJobStatus.ReportInProgress => 90,
        _ => 10
    };

    private async Task RetryAsync()
    {
        _retrying = true;
        var job = await Db.VideoJobs.FindAsync(JobId);
        if (job is not null && (job.Status == VideoJobStatus.Failed || job.Status == VideoJobStatus.ReportComplete))
        {
            Db.RallySegments.RemoveRange(Db.RallySegments.Where(s => s.VideoJobId == JobId));
            var report = await Db.CoachingReports.FirstOrDefaultAsync(r => r.VideoJobId == JobId);
            if (report is not null) Db.CoachingReports.Remove(report);
            job.Status = VideoJobStatus.Queued;
            job.ErrorMessage = null;
            job.HighlightFilePath = null;
            job.CompletedAt = null;
            await Db.SaveChangesAsync();
            JobClient.Enqueue<VideoProcessingJob>(j => j.ProcessAsync(JobId));
        }
        _retrying = false;
        await RefreshAsync();
        _pollTimer = new System.Threading.Timer(async _ =>
        {
            await RefreshAsync();
            await InvokeAsync(StateHasChanged);
            if (_job?.Status == VideoJobStatus.ReportComplete || _job?.Status == VideoJobStatus.Failed)
                await _pollTimer!.DisposeAsync();
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async ValueTask DisposeAsync()
    {
        if (_pollTimer is not null)
            await _pollTimer.DisposeAsync();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/PickleIQ.Web/Components/Pages/Results.razor
git commit -m "feat: migrate Results page to MudBlazor"
```

---

### Task 7: Remove leftover template pages

**Files:**
- Delete: `src/PickleIQ.Web/Components/Pages/Counter.razor`
- Delete: `src/PickleIQ.Web/Components/Pages/Weather.razor`

- [ ] **Step 1: Delete unused pages**

```bash
rm src/PickleIQ.Web/Components/Pages/Counter.razor
rm src/PickleIQ.Web/Components/Pages/Weather.razor
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PickleIQ.Web/PickleIQ.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: remove unused Counter and Weather template pages"
```
