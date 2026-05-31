using System.Text;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using LargeDocumentProcessing.Application.Documents.Models;

namespace LargeDocumentProcessing.Infrastructure.Documents;

public class PlaceholderPageArtifactGenerator(IObjectStorageService objectStorage) : IPageArtifactGenerator
{
    private const int PageCount = 3;
    private const int Width = 1200;
    private const int Height = 1600;

    public async Task<IReadOnlyList<PageArtifactResult>> GeneratePageArtifactsAsync(
        Guid documentId,
        string originalStorageKey,
        CancellationToken cancellationToken = default)
    {
        _ = originalStorageKey;

        var results = new List<PageArtifactResult>(PageCount);

        for (var pageNumber = 1; pageNumber <= PageCount; pageNumber++)
        {
            var storageKey = $"pages/{documentId}/page-{pageNumber}.svg";
            var svg = BuildPlaceholderSvg(documentId, pageNumber);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
            await objectStorage.SaveAsync(stream, storageKey, "image/svg+xml", cancellationToken);

            results.Add(new PageArtifactResult
            {
                PageNumber = pageNumber,
                Width = Width,
                Height = Height,
                ArtifactStorageKey = storageKey
            });
        }

        return results;
    }

    private static string BuildPlaceholderSvg(Guid documentId, int pageNumber)
    {
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{Width}" height="{Height}" viewBox="0 0 {Width} {Height}">
              <rect width="100%" height="100%" fill="#f4f6f8"/>
              <text x="60" y="120" font-family="Segoe UI, Arial, sans-serif" font-size="42" fill="#1f2937">Placeholder page artifact</text>
              <text x="60" y="200" font-family="Segoe UI, Arial, sans-serif" font-size="28" fill="#374151">Document ID: {documentId}</text>
              <text x="60" y="260" font-family="Segoe UI, Arial, sans-serif" font-size="28" fill="#374151">Page number: {pageNumber}</text>
              <text x="60" y="340" font-family="Segoe UI, Arial, sans-serif" font-size="22" fill="#6b7280">Replace this generator with PDFium / MuPDF / Ghostscript in production.</text>
            </svg>
            """;
    }
}
