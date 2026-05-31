namespace PickleIQ.Core.Entities;

public class RallySegment
{
    public Guid Id { get; set; }
    public Guid VideoJobId { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => EndSeconds - StartSeconds;

    public VideoJob VideoJob { get; set; } = null!;
}
