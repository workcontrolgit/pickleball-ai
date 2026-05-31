namespace PickleIQ.Core.Entities;

public enum VideoJobStatus
{
    Queued,
    RallyDetectionInProgress,
    RallyDetectionComplete,
    HighlightInProgress,
    HighlightComplete,
    ReportInProgress,
    ReportComplete,
    Failed
}

public class VideoJob
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? HighlightFilePath { get; set; }
    public VideoJobStatus Status { get; set; } = VideoJobStatus.Queued;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<RallySegment> RallySegments { get; set; } = new List<RallySegment>();
    public CoachingReport? CoachingReport { get; set; }
}
