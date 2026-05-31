using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Application.Documents.Dtos;

public class DocumentUploadResultDto
{
    public Guid DocumentId { get; init; }

    public DocumentStatus Status { get; init; }
}
