using System.Threading.Channels;
using PickleIQ.Core.Entities;

namespace PickleIQ.Core.Interfaces;

public interface IJobStatusService
{
    void Subscribe(Guid jobId);
    void PushStatus(Guid jobId, VideoJobStatus status);
    void Unsubscribe(Guid jobId);
    ChannelReader<VideoJobStatus>? TryGetReader(Guid jobId);
}
