using System.Collections.Concurrent;
using System.Threading.Channels;
using PickleIQ.Core.Entities;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.Services;

public class JobStatusService : IJobStatusService
{
    private readonly ConcurrentDictionary<Guid, Channel<VideoJobStatus>> _channels = new();

    public void Subscribe(Guid jobId)
    {
        _channels.TryAdd(jobId, Channel.CreateUnbounded<VideoJobStatus>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false
        }));
    }

    public void PushStatus(Guid jobId, VideoJobStatus status)
    {
        if (_channels.TryGetValue(jobId, out var channel))
            _ = channel.Writer.TryWrite(status);
    }

    public void Unsubscribe(Guid jobId)
    {
        if (_channels.TryRemove(jobId, out var channel))
            channel.Writer.TryComplete();
    }

    public ChannelReader<VideoJobStatus>? TryGetReader(Guid jobId) =>
        _channels.TryGetValue(jobId, out var channel) ? channel.Reader : null;
}
