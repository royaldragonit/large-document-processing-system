using LargeDocumentProcessing.Application.Common.Interfaces;
using LargeDocumentProcessing.Infrastructure.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LargeDocumentProcessing.Application.FunctionalTests.Infrastructure;

public sealed class WebApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "ldp-http-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder
            .UseSetting("ConnectionStrings:LargeDocumentProcessingDb", connectionString)
            .UseSetting($"{DocumentStorageOptions.SectionName}:RootPath", _storageRoot)
            .UseSetting($"{DocumentStorageOptions.SectionName}:PublicBasePath", "/storage");

        builder.ConfigureTestServices(services =>
        {
            services
                .RemoveAll<IUser>()
                .AddTransient(provider =>
                {
                    var mock = new Mock<IUser>();
                    mock.SetupGet(x => x.Roles).Returns(TestApp.GetRoles());
                    mock.SetupGet(x => x.Id).Returns(TestApp.GetUserId());
                    return mock.Object;
                });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TryDeleteStorageRoot();
        }

        base.Dispose(disposing);
    }

    private void TryDeleteStorageRoot()
    {
        if (!Directory.Exists(_storageRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; another test worker may still hold file handles briefly.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}
