using PickleIQ.Core.Entities;

namespace PickleIQ.Core.Interfaces;

public interface IRallyDetectionService
{
    Task<IList<(double StartSeconds, double EndSeconds)>> DetectRalliesAsync(string videoPath, VideoMode mode = VideoMode.Match, CancellationToken cancellationToken = default);
}
