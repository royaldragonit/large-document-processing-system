using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LargeDocumentProcessing.Application.Documents.Dtos;
using LargeDocumentProcessing.Domain.Enums;

namespace LargeDocumentProcessing.Application.FunctionalTests.Documents;

public class DocumentApiHttpTests : TestBase
{
    private static readonly string SamplePdfPath = Path.Combine(AppContext.BaseDirectory, "samples", "minimal.pdf");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task ShouldAcceptPdfUpload()
    {
        using var content = await CreatePdfMultipartContentAsync();

        var response = await FunctionalTestSetup.HttpClient.PostAsync("/api/documents/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var result = await response.Content.ReadFromJsonAsync<DocumentUploadResultDto>(JsonOptions);
        result.ShouldNotBeNull();
        result!.DocumentId.ShouldNotBe(Guid.Empty);
        result.Status.ShouldBeOneOf(
            DocumentStatus.Uploaded,
            DocumentStatus.Processing,
            DocumentStatus.Ready);
    }

    [Test]
    public async Task ShouldEventuallyReturnReadyDocument()
    {
        var documentId = await UploadSamplePdfAsync();

        var document = await PollUntilReadyAsync(documentId, TimeSpan.FromSeconds(5));

        document.Status.ShouldBe(DocumentStatus.Ready);
        document.TotalPages.ShouldBe(3);
        document.RetentionUntil.ShouldBe(document.CreatedAt.AddYears(7), TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ShouldReturnThreeReadyPages()
    {
        var documentId = await UploadSamplePdfAsync();
        await PollUntilReadyAsync(documentId, TimeSpan.FromSeconds(5));

        var response = await FunctionalTestSetup.HttpClient.GetAsync($"/api/documents/{documentId}/pages");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var pages = await response.Content.ReadFromJsonAsync<List<DocumentPageListItemDto>>(JsonOptions);
        pages.ShouldNotBeNull();
        pages!.Count.ShouldBe(3);
        pages.Select(p => p.PageNumber).ShouldBe([1, 2, 3]);
        pages.ShouldAllBe(p => p.Status == DocumentPageStatus.Ready);
    }

    [Test]
    public async Task ShouldReturnArtifactUrlForPageOne()
    {
        var documentId = await UploadSamplePdfAsync();
        await PollUntilReadyAsync(documentId, TimeSpan.FromSeconds(5));

        var response = await FunctionalTestSetup.HttpClient.GetAsync($"/api/documents/{documentId}/pages/1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<DocumentPageDto>(JsonOptions);
        page.ShouldNotBeNull();
        page!.Status.ShouldBe(DocumentPageStatus.Ready);
        page.ArtifactUrl.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ShouldReturnSvgArtifactContent()
    {
        var documentId = await UploadSamplePdfAsync();
        await PollUntilReadyAsync(documentId, TimeSpan.FromSeconds(5));

        var pageResponse = await FunctionalTestSetup.HttpClient.GetAsync($"/api/documents/{documentId}/pages/1");
        var page = (await pageResponse.Content.ReadFromJsonAsync<DocumentPageDto>(JsonOptions))!;

        var artifactResponse = await FunctionalTestSetup.HttpClient.GetAsync(page.ArtifactUrl!);

        artifactResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await artifactResponse.Content.ReadAsStringAsync();
        var contentType = artifactResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;

        (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase) ||
         body.Contains("Placeholder page artifact", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldRejectNonPdfUpload()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("plain text"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "notes.txt");

        var response = await FunctionalTestSetup.HttpClient.PostAsync("/api/documents/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> UploadSamplePdfAsync()
    {
        using var content = await CreatePdfMultipartContentAsync();

        var response = await FunctionalTestSetup.HttpClient.PostAsync("/api/documents/upload", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var result = (await response.Content.ReadFromJsonAsync<DocumentUploadResultDto>(JsonOptions))!;
        return result.DocumentId;
    }

    private static async Task<MultipartFormDataContent> CreatePdfMultipartContentAsync()
    {
        var pdfBytes = await File.ReadAllBytesAsync(SamplePdfPath);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "minimal.pdf");
        return content;
    }

    private static async Task<DocumentDto> PollUntilReadyAsync(Guid documentId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var response = await FunctionalTestSetup.HttpClient.GetAsync($"/api/documents/{documentId}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var document = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions);
            if (document?.Status == DocumentStatus.Ready)
            {
                return document;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Document {documentId} did not reach Ready within {timeout.TotalSeconds} seconds.");
    }
}
