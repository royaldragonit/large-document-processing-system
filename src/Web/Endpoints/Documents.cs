using LargeDocumentProcessing.Application.Documents.Dtos;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LargeDocumentProcessing.Web.Endpoints;

public class Documents : IEndpointGroup
{
    private const string DemoOwnerUserId = "demo-user";

    public static string? RoutePrefix => "/api/documents";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(Upload, "upload").DisableAntiforgery();
        groupBuilder.MapGet(GetDocument, "{documentId:guid}");
        groupBuilder.MapGet(GetPages, "{documentId:guid}/pages");
        groupBuilder.MapGet(GetPage, "{documentId:guid}/pages/{pageNumber:int}");
    }

    [EndpointSummary("Upload a construction plan PDF")]
    [EndpointDescription("Stores the original PDF, sets 7-year retention metadata, and enqueues asynchronous page artifact generation. Prototype artifacts are placeholder SVGs via IPageArtifactGenerator.")]
    public static async Task<Results<Accepted<DocumentUploadResultDto>, BadRequest<string>>> Upload(
        IDocumentService documentService,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("A non-empty PDF file is required.");
        }

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await documentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                DemoOwnerUserId,
                cancellationToken);

            return TypedResults.Accepted($"/api/documents/{result.DocumentId}", result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    [EndpointSummary("Get document metadata")]
    [EndpointDescription("Returns upload status, retention date, and total pages when processing completes.")]
    public static async Task<Results<Ok<DocumentDto>, NotFound>> GetDocument(
        IDocumentService documentService,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await documentService.GetByIdAsync(documentId, DemoOwnerUserId, cancellationToken);
        return document is null ? TypedResults.NotFound() : TypedResults.Ok(document);
    }

    [EndpointSummary("List document pages")]
    [EndpointDescription("Returns page numbers and processing status. Does not access the original PDF.")]
    public static async Task<Results<Ok<IReadOnlyList<DocumentPageListItemDto>>, NotFound>> GetPages(
        IDocumentService documentService,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var pages = await documentService.GetPagesAsync(documentId, DemoOwnerUserId, cancellationToken);
        return pages is null ? TypedResults.NotFound() : TypedResults.Ok(pages);
    }

    [EndpointSummary("Get a single page artifact")]
    [EndpointDescription("Returns page status and artifactUrl when ready. Serves a pre-generated artifact URL only; never parses the original PDF on this path.")]
    public static async Task<Results<Ok<DocumentPageDto>, NotFound>> GetPage(
        IDocumentService documentService,
        Guid documentId,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var page = await documentService.GetPageAsync(documentId, pageNumber, DemoOwnerUserId, cancellationToken);
        return page is null ? TypedResults.NotFound() : TypedResults.Ok(page);
    }
}
