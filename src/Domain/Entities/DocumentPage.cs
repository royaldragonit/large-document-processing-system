using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Domain.Entities;

public class DocumentPage
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int PageNumber { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? ArtifactStorageKey { get; set; }

    public string? ThumbnailStorageKey { get; set; }

    public DocumentPageStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? FailureReason { get; set; }

    public Document Document { get; set; } = null!;
}
