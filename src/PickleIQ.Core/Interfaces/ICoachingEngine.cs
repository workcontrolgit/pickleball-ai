namespace PickleIQ.Core.Interfaces;

public record MatchSummary(
    int RallyCount,
    double AverageRallySeconds,
    double LongestRallySeconds,
    double TotalMatchSeconds,
    IReadOnlyList<byte[]> CoachingFrames);

public interface ICoachingEngine
{
    Task<string> GenerateReportHtmlAsync(MatchSummary summary, CancellationToken cancellationToken = default);
}
