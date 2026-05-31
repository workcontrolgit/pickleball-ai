using FFMpegCore;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;
using PickleIQ.Infrastructure.Jobs;
using PickleIQ.Infrastructure.AI;
using PickleIQ.Infrastructure.Services;
using PickleIQ.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Allow overriding the FFmpeg binary folder via config (e.g. appsettings.Development.json).
// If not set, FFMpegCore looks for ffmpeg on the system PATH.
var ffmpegFolder = builder.Configuration["FFmpeg:BinaryFolder"];
if (!string.IsNullOrEmpty(ffmpegFolder))
    GlobalFFOptions.Configure(opts => opts.BinaryFolder = ffmpegFolder);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

builder.Services.AddScoped<IVideoStorageService, VideoStorageService>();
builder.Services.AddScoped<IRallyDetectionService, RallyDetectionService>();
builder.Services.AddScoped<IHighlightGenerationService, HighlightGenerationService>();
builder.Services.AddScoped<ICoachingEngine, OllamaCoachingEngine>();
builder.Services.AddScoped<VideoProcessingJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/download/{jobId:guid}/highlights", async (Guid jobId, AppDbContext db) =>
{
    var job = await db.VideoJobs.FirstOrDefaultAsync(j => j.Id == jobId);
    if (job is null || string.IsNullOrEmpty(job.HighlightFilePath) || !File.Exists(job.HighlightFilePath))
        return Results.NotFound("Highlight file not available.");
    var stream = File.OpenRead(job.HighlightFilePath);
    return Results.File(stream, "video/mp4", $"highlights-{jobId}.mp4");
});

app.Run();
