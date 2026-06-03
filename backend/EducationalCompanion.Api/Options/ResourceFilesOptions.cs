namespace EducationalCompanion.Api.Options;

public class ResourceFilesOptions
{
    public const string SectionName = "ResourceFiles";

    public string StoragePath { get; set; } = "storage/resource-files";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxExtractedTextLength { get; set; } = 50_000;

    public int MaxSummaryLength { get; set; } = 2_000;

    /// <summary>Minimum extracted characters to treat PDF/DOCX as successful (below → OCR-needed failure).</summary>
    public int MinMeaningfulExtractedCharacters { get; set; } = 40;
}
