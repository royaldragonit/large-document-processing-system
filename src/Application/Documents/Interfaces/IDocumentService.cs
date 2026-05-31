using LargeDocumentProcessing.Application.Documents.Dtos;

namespace LargeDocumentProcessing.Application.Documents.Interfaces;

public interface IDocumentService
{
    Task<DocumentUploadResultDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentDto?> GetByIdAsync(Guid documentId, string ownerUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentPageListItemDto>?> GetPagesAsync(
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentPageDto?> GetPageAsync(
        Guid documentId,
        int pageNumber,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
