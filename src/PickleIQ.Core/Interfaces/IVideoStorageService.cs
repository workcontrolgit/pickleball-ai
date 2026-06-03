using PickleIQ.Core.Entities;

namespace PickleIQ.Core.Interfaces;

public interface IVideoStorageService
{
    Task<Guid> SaveAsync(Stream fileStream, string fileName, long fileSize, VideoMode mode = VideoMode.Match, CancellationToken cancellationToken = default);
}
