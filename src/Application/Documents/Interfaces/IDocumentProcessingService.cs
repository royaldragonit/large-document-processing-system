namespace LargeDocumentProcessing.Application.Documents.Interfaces;

public interface IDocumentProcessingService
{
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
