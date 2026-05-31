using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Application.Documents.Dtos;

public class DocumentPageDto
{
    public Guid DocumentId { get; init; }

    public int PageNumber { get; init; }

    public DocumentPageStatus Status { get; init; }

    public string? ArtifactUrl { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string? FailureReason { get; init; }
}
