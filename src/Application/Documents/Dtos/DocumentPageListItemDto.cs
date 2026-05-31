using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Application.Documents.Dtos;

public class DocumentPageListItemDto
{
    public int PageNumber { get; init; }

    public DocumentPageStatus Status { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
