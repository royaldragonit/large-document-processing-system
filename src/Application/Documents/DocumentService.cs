using LargeDocumentProcessing.Application.Common.Interfaces;
using LargeDocumentProcessing.Application.Documents.Dtos;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using LargeDocumentProcessing.Domain.Entities;
using LargeDocumentProcessing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LargeDocumentProcessing.Application.Documents;

public class DocumentService(
    IApplicationDbContext context,
    IObjectStorageService objectStorage,
    IDocumentProcessingQueue processingQueue) : IDocumentService
{
    private static readonly TimeSpan ArtifactUrlLifetime = TimeSpan.FromHours(1);
    private const int RetentionYears = 7;

    public async Task<DocumentUploadResultDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ValidatePdfUpload(fileName, contentType);

        var documentId = Guid.NewGuid();
        var safeFileName = GetSafeFileName(fileName);
        var storageKey = $"originals/{documentId}/{safeFileName}";
        var now = DateTimeOffset.UtcNow;

        await objectStorage.SaveAsync(fileStream, storageKey, contentType, cancellationToken);

        var document = new Document
        {
            Id = documentId,
            OwnerUserId = ownerUserId,
            FileName = safeFileName,
            OriginalContentType = contentType,
            OriginalFileSizeBytes = fileSizeBytes,
            OriginalStorageKey = storageKey,
            Status = DocumentStatus.Uploaded,
            CreatedAt = now,
            UpdatedAt = now,
            RetentionUntil = now.AddYears(RetentionYears)
        };

        context.Documents.Add(document);
        await context.SaveChangesAsync(cancellationToken);

        await processingQueue.EnqueueAsync(documentId, cancellationToken);

        return new DocumentUploadResultDto
        {
            DocumentId = documentId,
            Status = document.Status
        };
    }

    public async Task<DocumentDto?> GetByIdAsync(
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var document = await context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OwnerUserId == ownerUserId, cancellationToken);

        return document is null ? null : MapDocument(document);
    }

    public async Task<IReadOnlyList<DocumentPageListItemDto>?> GetPagesAsync(
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var documentExists = await context.Documents
            .AsNoTracking()
            .AnyAsync(d => d.Id == documentId && d.OwnerUserId == ownerUserId, cancellationToken);

        if (!documentExists)
        {
            return null;
        }

        return await context.DocumentPages
            .AsNoTracking()
            .Where(p => p.DocumentId == documentId)
            .OrderBy(p => p.PageNumber)
            .Select(p => new DocumentPageListItemDto
            {
                PageNumber = p.PageNumber,
                Status = p.Status,
                Width = p.Width,
                Height = p.Height
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentPageDto?> GetPageAsync(
        Guid documentId,
        int pageNumber,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var document = await context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OwnerUserId == ownerUserId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var page = await context.DocumentPages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.DocumentId == documentId && p.PageNumber == pageNumber,
                cancellationToken);

        if (page is null)
        {
            return new DocumentPageDto
            {
                DocumentId = documentId,
                PageNumber = pageNumber,
                Status = DocumentPageStatus.Pending
            };
        }

        string? artifactUrl = null;
        if (page.Status == DocumentPageStatus.Ready && !string.IsNullOrEmpty(page.ArtifactStorageKey))
        {
            artifactUrl = await objectStorage.GetReadUrlAsync(
                page.ArtifactStorageKey,
                ArtifactUrlLifetime,
                cancellationToken);
        }

        return new DocumentPageDto
        {
            DocumentId = documentId,
            PageNumber = page.PageNumber,
            Status = page.Status,
            ArtifactUrl = artifactUrl,
            Width = page.Width,
            Height = page.Height,
            FailureReason = page.FailureReason
        };
    }

    private static void ValidatePdfUpload(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        var isPdfExtension = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        var isPdfContentType = string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        if (!isPdfExtension && !isPdfContentType)
        {
            throw new ArgumentException("Only PDF files are supported.", nameof(fileName));
        }
    }

    private static string GetSafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "document.pdf";
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return name;
    }

    private static DocumentDto MapDocument(Document document) => new()
    {
        Id = document.Id,
        OwnerUserId = document.OwnerUserId,
        FileName = document.FileName,
        OriginalContentType = document.OriginalContentType,
        OriginalFileSizeBytes = document.OriginalFileSizeBytes,
        Status = document.Status,
        TotalPages = document.TotalPages,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
        RetentionUntil = document.RetentionUntil,
        FailureReason = document.FailureReason
    };
}
