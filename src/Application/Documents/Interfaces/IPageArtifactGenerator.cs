using LargeDocumentProcessing.Application.Documents.Models;

namespace LargeDocumentProcessing.Application.Documents.Interfaces;

public interface IPageArtifactGenerator
{
    Task<IReadOnlyList<PageArtifactResult>> GeneratePageArtifactsAsync(
        Guid documentId,
        string originalStorageKey,
        CancellationToken cancellationToken = default);
}
