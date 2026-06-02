using FFMpegCore;
using MudBlazor.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;
using PickleIQ.Infrastructure.Jobs;
using PickleIQ.Infrastructure.AI;
using PickleIQ.Infrastructure.Services;
using Serilog;
using PickleIQ.Web.Components;

// Bootstrap logger so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/pickleiq-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    // Resolve FFmpeg: config key → WinGet auto-detect → inject into process PATH
    var ffmpegFolder = builder.Configuration["FFmpeg:BinaryFolder"];
    if (string.IsNullOrEmpty(ffmpegFolder))
    {
        var wingetBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetBase))
        {
            ffmpegFolder = Directory.EnumerateDirectories(wingetBase, "Gyan.FFmpeg*")
                .SelectMany(d => Directory.EnumerateDirectories(d, "ffmpeg*"))
                .Select(d => Path.Combine(d, "bin"))
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "ffmpeg.exe")));
        }
    }

    if (!string.IsNullOrEmpty(ffmpegFolder))
    {
        // Set GlobalFFOptions for FFMpegCore and also inject into PATH so child processes find it
        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = ffmpegFolder });
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!currentPath.Contains(ffmpegFolder, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", ffmpegFolder + Path.PathSeparator + currentPath);
        Log.Information("FFmpeg resolved at: {Folder}", ffmpegFolder);
    }
    else
    {
        Log.Warning("FFmpeg not found via config or WinGet — falling back to system PATH");
    }

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents(options =>
            options.MaxBufferedUnacknowledgedRenderBatches = 20);

    builder.Services.AddServerSideBlazor(options =>
        options.MaxBufferedUnacknowledgedRenderBatches = 20);

    const long fiveGb = 5L * 1024 * 1024 * 1024;
    builder.Services.AddSignalR(options =>
        options.MaximumReceiveMessageSize = fiveGb);

    builder.Services.AddMudServices();

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
    builder.Services.AddScoped<ICoachingFrameSampler, CoachingFrameSampler>();
    builder.Services.AddScoped<ICoachingEngine, OllamaVisionCoachingEngine>();
    builder.Services.AddScoped<VideoProcessingJob>();
    builder.Services.AddSingleton<ICoachingStreamService, CoachingStreamService>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.MapHangfireDashboard("/hangfire");

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
