using LargeDocumentProcessing.Application.Common.Interfaces;
using LargeDocumentProcessing.Infrastructure.Data;
using LargeDocumentProcessing.Application.Documents.Interfaces;
using LargeDocumentProcessing.Infrastructure.Documents;
using LargeDocumentProcessing.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        builder.Services.Configure<DocumentStorageOptions>(
            builder.Configuration.GetSection(DocumentStorageOptions.SectionName));

        builder.Services.AddSingleton<InMemoryDocumentProcessingQueue>();
        builder.Services.AddSingleton<IDocumentProcessingQueue>(sp => sp.GetRequiredService<InMemoryDocumentProcessingQueue>());

        builder.Services.AddScoped<IObjectStorageService, LocalFileObjectStorageService>();
        builder.Services.AddScoped<IPageArtifactGenerator, PlaceholderPageArtifactGenerator>();
        builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        builder.Services.AddHostedService<DocumentProcessingWorker>();
    }
}
