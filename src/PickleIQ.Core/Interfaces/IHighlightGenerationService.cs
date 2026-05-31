namespace PickleIQ.Core.Interfaces;

public interface IHighlightGenerationService
{
    Task<string> GenerateAsync(Guid jobId, string videoPath, CancellationToken cancellationToken = default);
}
