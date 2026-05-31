using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string OriginalContentType { get; set; } = string.Empty;

    public long OriginalFileSizeBytes { get; set; }

    public string OriginalStorageKey { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; }

    public int? TotalPages { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset RetentionUntil { get; set; }

    public string? FailureReason { get; set; }

    public ICollection<DocumentPage> Pages { get; set; } = new List<DocumentPage>();
}
