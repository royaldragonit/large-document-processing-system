namespace LargeDocumentProcessing.Infrastructure.Documents;

public class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string RootPath { get; set; } = "storage";

    public string PublicBasePath { get; set; } = "/storage";
}
