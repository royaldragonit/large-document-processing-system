using LargeDocumentProcessing.Application.Documents.Interfaces;
using Microsoft.Extensions.Options;

namespace LargeDocumentProcessing.Infrastructure.Documents;

public class LocalFileObjectStorageService(IOptions<DocumentStorageOptions> options) : IObjectStorageService
{
    private readonly DocumentStorageOptions _options = options.Value;

    public async Task SaveAsync(Stream stream, string storageKey, string contentType, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<string> GetReadUrlAsync(string storageKey, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        var basePath = _options.PublicBasePath.TrimEnd('/');
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var url = $"{basePath}/{normalizedKey}";
        return Task.FromResult(url);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        return Task.FromResult(File.Exists(fullPath));
    }

    private string GetFullPath(string storageKey)
    {
        var root = Path.GetFullPath(_options.RootPath);
        var relative = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return fullPath;
    }
}
