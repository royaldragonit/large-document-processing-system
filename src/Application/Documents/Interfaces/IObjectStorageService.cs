namespace LargeDocumentProcessing.Application.Documents.Interfaces;

public interface IObjectStorageService
{
    Task SaveAsync(Stream stream, string storageKey, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(string storageKey, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}
