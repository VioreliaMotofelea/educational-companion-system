using System.Text;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Ingestion;

public class ResourceTextExtractionServiceTests
{
    private readonly ResourceTextExtractionService _service = new(
        Options.Create(new ResourceFilesOptions
        {
            MaxExtractedTextLength = 50_000,
            MinMeaningfulExtractedCharacters = 40
        }));

    [Fact]
    public void Extract_Txt_ReadsUtf8Content()
    {
        var bytes = Encoding.UTF8.GetBytes("  Hello   world  from  notes.  ");
        using var stream = new MemoryStream(bytes);

        var result = _service.Extract(stream, ".txt");

        Assert.Equal(ResourceTextExtractionMethod.PlainText, result.Method);
        Assert.Equal("Hello world from notes.", result.NormalizedText);
        Assert.False(result.RequiresOcrFallback);
    }

    [Fact]
    public void Extract_Markdown_UsesMarkdownMethod()
    {
        var bytes = Encoding.UTF8.GetBytes("# Title\n\nParagraph about indexes.");
        using var stream = new MemoryStream(bytes);

        var result = _service.Extract(stream, ".md");

        Assert.Equal(ResourceTextExtractionMethod.MarkdownText, result.Method);
        Assert.Contains("indexes", result.NormalizedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_Docx_ReadsParagraphText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ecs-docx-{Guid.NewGuid():N}.docx");
        try
        {
            CreateMinimalDocx(path, "Database normalization explains redundancy removal.");
            using var stream = File.OpenRead(path);
            var result = _service.Extract(stream, ".docx");
            Assert.Equal(ResourceTextExtractionMethod.DocxText, result.Method);
            Assert.Contains("normalization", result.NormalizedText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Extract_PdfWithNoText_MarksOcrFallback()
    {
        var emptyPdf = MinimalPdfBytes();
        using var stream = new MemoryStream(emptyPdf);
        var result = _service.Extract(stream, ".pdf");
        Assert.Equal(ResourceTextExtractionMethod.PdfText, result.Method);
        Assert.True(result.RequiresOcrFallback);
    }

    private static void CreateMinimalDocx(string path, string paragraphText)
    {
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            path,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
            new DocumentFormat.OpenXml.Wordprocessing.Body(
                new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.Text(paragraphText)))));
        mainPart.Document.Save();
    }

    private static byte[] MinimalPdfBytes()
    {
        // Minimal PDF with no text objects (triggers low-text / OCR-needed path).
        var pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n";
        return Encoding.ASCII.GetBytes(pdf);
    }
}
