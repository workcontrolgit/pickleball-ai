using System.Threading.Channels;

namespace PickleIQ.Core.Interfaces;

public interface ICoachingStreamService
{
    void CreateStream(Guid jobId);
    void WriteChunk(Guid jobId, string chunk);
    void CompleteStream(Guid jobId);
    ChannelReader<string>? TryGetReader(Guid jobId);
}
