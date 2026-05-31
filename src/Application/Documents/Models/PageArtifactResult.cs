namespace LargeDocumentProcessing.Application.Documents.Models;

public class PageArtifactResult
{
    public int PageNumber { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string ArtifactStorageKey { get; init; } = string.Empty;

    public string? ThumbnailStorageKey { get; init; }
}
