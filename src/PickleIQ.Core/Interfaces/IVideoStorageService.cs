namespace PickleIQ.Core.Interfaces;

public interface IVideoStorageService
{
    Task<Guid> SaveAsync(Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default);
}
