using LargeDocumentProcessing.Application.Common.Interfaces;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using LargeDocumentProcessing.Domain.Entities;
using LargeDocumentProcessing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LargeDocumentProcessing.Infrastructure.Documents;

public class DocumentProcessingService(
    IApplicationDbContext context,
    IPageArtifactGenerator pageArtifactGenerator,
    ILogger<DocumentProcessingService> logger) : IDocumentProcessingService
{
    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} was not found for processing.", documentId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        try
        {
            document.Status = DocumentStatus.Processing;
            document.UpdatedAt = now;
            document.FailureReason = null;
            await context.SaveChangesAsync(cancellationToken);

            var artifacts = await pageArtifactGenerator.GeneratePageArtifactsAsync(
                document.Id,
                document.OriginalStorageKey,
                cancellationToken);

            var existingPages = await context.DocumentPages
                .Where(p => p.DocumentId == documentId)
                .ToListAsync(cancellationToken);

            context.DocumentPages.RemoveRange(existingPages);

            foreach (var artifact in artifacts)
            {
                context.DocumentPages.Add(new DocumentPage
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    PageNumber = artifact.PageNumber,
                    Width = artifact.Width,
                    Height = artifact.Height,
                    ArtifactStorageKey = artifact.ArtifactStorageKey,
                    ThumbnailStorageKey = artifact.ThumbnailStorageKey,
                    Status = DocumentPageStatus.Ready,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            document.TotalPages = artifacts.Count;
            document.Status = DocumentStatus.Ready;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.FailureReason = null;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Processed document {DocumentId} with {PageCount} pages.",
                documentId,
                artifacts.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {DocumentId}.", documentId);

            await context.Documents
                .Where(d => d.Id == documentId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(d => d.Status, DocumentStatus.Failed)
                        .SetProperty(d => d.FailureReason, ex.Message)
                        .SetProperty(d => d.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken);
        }
    }
}
