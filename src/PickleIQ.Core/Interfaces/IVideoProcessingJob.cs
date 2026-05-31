namespace PickleIQ.Core.Interfaces;

public interface IVideoProcessingJob
{
    Task ProcessAsync(Guid jobId);
}
