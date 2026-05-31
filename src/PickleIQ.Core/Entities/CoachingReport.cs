namespace PickleIQ.Core.Entities;

public class CoachingReport
{
    public Guid Id { get; set; }
    public Guid VideoJobId { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public VideoJob VideoJob { get; set; } = null!;
}
