using System.Collections.Concurrent;
using System.Threading.Channels;
using PickleIQ.Core.Interfaces;

namespace PickleIQ.Infrastructure.Services;

public class CoachingStreamService : ICoachingStreamService
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _channels = new();

    public void CreateStream(Guid jobId)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        _channels.TryAdd(jobId, channel);
    }

    public void WriteChunk(Guid jobId, string chunk)
    {
        if (_channels.TryGetValue(jobId, out var channel))
            _ = channel.Writer.TryWrite(chunk);
    }

    public void CompleteStream(Guid jobId)
    {
        if (_channels.TryRemove(jobId, out var channel))
            channel.Writer.TryComplete();
    }

    public ChannelReader<string>? TryGetReader(Guid jobId) =>
        _channels.TryGetValue(jobId, out var channel) ? channel.Reader : null;
}
