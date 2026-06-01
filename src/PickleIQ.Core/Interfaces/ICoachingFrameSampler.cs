namespace PickleIQ.Core.Interfaces;

public interface ICoachingFrameSampler
{
    Task<IReadOnlyList<byte[]>> SampleAsync(
        string videoPath,
        IReadOnlyList<(double StartSeconds, double EndSeconds)> rallies,
        CancellationToken cancellationToken = default);
}
