namespace ClassificadorExtractorDocumentos.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public required string BasePath { get; set; }
}
