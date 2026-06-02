namespace PickleIQ.Core.Interfaces;

public record MatchSummary(
    int RallyCount,
    double AverageRallySeconds,
    double LongestRallySeconds,
    double TotalMatchSeconds);

public interface ICoachingEngine
{
    Task<string> GenerateReportHtmlAsync(
        MatchSummary summary,
        IReadOnlyList<byte[]>? coachingFrames = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default);
}
