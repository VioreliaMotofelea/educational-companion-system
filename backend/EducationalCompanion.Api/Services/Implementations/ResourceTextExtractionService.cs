using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EducationalCompanion.Api.Ingestion;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace EducationalCompanion.Api.Services.Implementations;

public class ResourceTextExtractionService : IResourceTextExtractionService
{
    private readonly ResourceFilesOptions _options;

    public ResourceTextExtractionService(IOptions<ResourceFilesOptions> options)
    {
        _options = options.Value;
    }

    public ResourceTextExtractionResult Extract(Stream fileStream, string extension)
    {
        var raw = extension switch
        {
            ".txt" => ResourceTextNormalizer.ReadUtf8Text(fileStream),
            ".md" or ".markdown" => ResourceTextNormalizer.ReadUtf8Text(fileStream),
            ".docx" => ExtractDocx(fileStream),
            ".pdf" => ExtractPdf(fileStream),
            _ => throw new ValidationException("File type is not supported for text extraction.")
        };

        var method = extension switch
        {
            ".txt" => ResourceTextExtractionMethod.PlainText,
            ".md" or ".markdown" => ResourceTextExtractionMethod.MarkdownText,
            ".docx" => ResourceTextExtractionMethod.DocxText,
            ".pdf" => ResourceTextExtractionMethod.PdfText,
            _ => ResourceTextExtractionMethod.Unsupported
        };

        var normalized = ResourceTextNormalizer.Normalize(raw);
        var truncated = ResourceTextNormalizer.Truncate(normalized, _options.MaxExtractedTextLength);
        var requiresOcr = method == ResourceTextExtractionMethod.PdfText
            && truncated.Length < _options.MinMeaningfulExtractedCharacters;

        return new ResourceTextExtractionResult(truncated, method, requiresOcr);
    }

    private static string ExtractDocx(Stream fileStream)
    {
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        var paragraphs = body.Descendants<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string ExtractPdf(Stream fileStream)
    {
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        try
        {
            using var document = PdfDocument.Open(fileStream);
            var pages = document.GetPages();
            var builder = new System.Text.StringBuilder();
            foreach (var page in pages)
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (builder.Length > 0)
                        builder.Append(' ');
                    builder.Append(text);
                }
            }

            return builder.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
