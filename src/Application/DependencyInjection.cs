using LargeDocumentProcessing.Application.Documents;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IDocumentService, DocumentService>();
    }
}
