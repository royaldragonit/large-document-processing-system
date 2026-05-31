using LargeDocumentProcessing.Application.Documents.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LargeDocumentProcessing.Infrastructure.Documents;

public class DocumentProcessingWorker(
    IDocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var documentId = await queue.DequeueAsync(stoppingToken);

                await using var scope = scopeFactory.CreateAsyncScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();

                await processingService.ProcessDocumentAsync(documentId, stoppingToken);

                logger.LogInformation("Finished processing document {DocumentId}.", documentId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in document processing worker.");
            }
        }

        logger.LogInformation("Document processing worker stopped.");
    }
}
