namespace PickleIQ.Core.Interfaces;

public interface IRallyDetectionService
{
    Task<IList<(double StartSeconds, double EndSeconds)>> DetectRalliesAsync(string videoPath, CancellationToken cancellationToken = default);
}
