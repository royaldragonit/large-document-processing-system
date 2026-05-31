using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Application.Documents.Dtos;

public class DocumentDto
{
    public Guid Id { get; init; }

    public string OwnerUserId { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string OriginalContentType { get; init; } = string.Empty;

    public long OriginalFileSizeBytes { get; init; }

    public DocumentStatus Status { get; init; }

    public int? TotalPages { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset RetentionUntil { get; init; }

    public string? FailureReason { get; init; }
}
