using LargeDocumentProcessing.Application.Documents.Dtos;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using LargeDocumentProcessing.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LargeDocumentProcessing.Application.FunctionalTests.Documents;

public class DocumentProcessingTests : TestBase
{
    private const string OwnerUserId = "demo-user";
    private static readonly string SamplePdfPath = Path.Combine(AppContext.BaseDirectory, "samples", "minimal.pdf");

    [Test]
    public async Task ShouldRejectNonPdfUpload()
    {
        await using var stream = new MemoryStream("not a pdf"u8.ToArray());

        await Should.ThrowAsync<ArgumentException>(() =>
            TestApp.ExecuteAsync(async sp =>
            {
                var service = sp.GetRequiredService<IDocumentService>();
                await service.UploadAsync(stream, "notes.txt", "text/plain", stream.Length, OwnerUserId);
            }));
    }

    [Test]
    public async Task ShouldUploadProcessAndRetrievePageArtifact()
    {
        await using var stream = File.OpenRead(SamplePdfPath);
        var fileSize = stream.Length;

        var documentId = await TestApp.ExecuteAsync(async sp =>
        {
            var service = sp.GetRequiredService<IDocumentService>();
            var result = await service.UploadAsync(stream, "minimal.pdf", "application/pdf", fileSize, OwnerUserId);
            return result.DocumentId;
        });

        var document = await WaitForDocumentReadyAsync(documentId);
        document.Status.ShouldBe(DocumentStatus.Ready);
        document.TotalPages.ShouldNotBeNull();
        document.TotalPages!.Value.ShouldBeGreaterThan(0);

        var pages = await TestApp.ExecuteAsync(sp =>
            sp.GetRequiredService<IDocumentService>().GetPagesAsync(documentId, OwnerUserId));
        pages.ShouldNotBeNull();
        pages!.Count.ShouldBe(document.TotalPages!.Value);

        var page = await TestApp.ExecuteAsync(sp =>
            sp.GetRequiredService<IDocumentService>().GetPageAsync(documentId, 1, OwnerUserId));
        page.ShouldNotBeNull();
        page!.Status.ShouldBe(DocumentPageStatus.Ready);
        page.ArtifactUrl.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ShouldReturnNullForDocumentOwnedByAnotherUser()
    {
        await using var stream = File.OpenRead(SamplePdfPath);

        var documentId = await TestApp.ExecuteAsync(async sp =>
        {
            var service = sp.GetRequiredService<IDocumentService>();
            var result = await service.UploadAsync(stream, "minimal.pdf", "application/pdf", stream.Length, OwnerUserId);
            return result.DocumentId;
        });

        var document = await TestApp.ExecuteAsync(sp =>
            sp.GetRequiredService<IDocumentService>().GetByIdAsync(documentId, "other-user"));

        document.ShouldBeNull();
    }

    private static async Task<DocumentDto> WaitForDocumentReadyAsync(Guid documentId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var document = await TestApp.ExecuteAsync(sp =>
                sp.GetRequiredService<IDocumentService>().GetByIdAsync(documentId, OwnerUserId));

            if (document?.Status == DocumentStatus.Ready)
            {
                return document;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Document {documentId} did not reach Ready status in time.");
    }
}
