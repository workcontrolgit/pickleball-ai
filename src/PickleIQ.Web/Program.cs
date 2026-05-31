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

app.Run();
