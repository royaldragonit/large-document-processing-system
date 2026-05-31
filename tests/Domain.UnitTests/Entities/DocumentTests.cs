using LargeDocumentProcessing.Domain.Entities;
using LargeDocumentProcessing.Domain.Enums;
using NUnit.Framework;
using Shouldly;

namespace LargeDocumentProcessing.Domain.UnitTests.Entities;

public class DocumentTests
{
    [Test]
    public void NewDocumentShouldStartUploadedWithRetention()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "demo-user",
            FileName = "plan.pdf",
            OriginalContentType = "application/pdf",
            OriginalFileSizeBytes = 1024,
            OriginalStorageKey = "originals/test/plan.pdf",
            Status = DocumentStatus.Uploaded,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            RetentionUntil = createdAt.AddYears(7)
        };

        document.Status.ShouldBe(DocumentStatus.Uploaded);
        document.TotalPages.ShouldBeNull();
        document.RetentionUntil.ShouldBe(createdAt.AddYears(7));
    }
}
