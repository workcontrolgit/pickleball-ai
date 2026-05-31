using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using PickleIQ.Core.Interfaces;
using PickleIQ.Infrastructure.Data;
using PickleIQ.Infrastructure.Jobs;
using PickleIQ.Infrastructure.AI;
using PickleIQ.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

// No AddHangfireServer() here — the Web project is the sole Hangfire worker.
// Api only needs the client for enqueuing and the dashboard.

builder.Services.AddScoped<IRallyDetectionService, RallyDetectionService>();
builder.Services.AddScoped<IHighlightGenerationService, HighlightGenerationService>();
builder.Services.AddScoped<ICoachingEngine, OllamaCoachingEngine>();
builder.Services.AddScoped<VideoProcessingJob>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseHangfireDashboard("/hangfire");
app.MapControllers();

app.Run();
