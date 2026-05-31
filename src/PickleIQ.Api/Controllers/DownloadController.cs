using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleIQ.Infrastructure.Data;

namespace PickleIQ.Api.Controllers;

[ApiController]
[Route("api/download")]
public class DownloadController(AppDbContext db) : ControllerBase
{
    [HttpGet("{jobId:guid}/highlights")]
    public async Task<IActionResult> DownloadHighlights(Guid jobId)
    {
        var job = await db.VideoJobs.FirstOrDefaultAsync(j => j.Id == jobId);

        if (job is null)
            return NotFound("Job not found.");

        if (string.IsNullOrEmpty(job.HighlightFilePath) || !System.IO.File.Exists(job.HighlightFilePath))
            return NotFound("Highlight file not available yet.");

        var stream = System.IO.File.OpenRead(job.HighlightFilePath);
        return File(stream, "video/mp4", $"highlights-{jobId}.mp4");
    }
}
