using EducationalCompanion.Api.Ingestion;
using EducationalCompanion.Domain.Exceptions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Ingestion;

public class ResourceFileUploadValidatorTests
{
    [Theory]
    [InlineData("notes.exe")]
    [InlineData("script.sh")]
    [InlineData("archive.zip")]
    public void ValidateAndGetExtension_RejectsUnsupportedExtension(string fileName)
    {
        Assert.Throws<ValidationException>(() =>
            ResourceFileUploadValidator.ValidateAndGetExtension(fileName, "application/octet-stream", 100, 1024));
    }

    [Fact]
    public void ValidateAndGetExtension_RejectsOversizedFile()
    {
        Assert.Throws<ValidationException>(() =>
            ResourceFileUploadValidator.ValidateAndGetExtension("notes.txt", "text/plain", 2000, 1000));
    }

    [Theory]
    [InlineData("notes.txt", ".txt")]
    [InlineData("readme.md", ".md")]
    [InlineData("slides.pdf", ".pdf")]
    [InlineData("handout.docx", ".docx")]
    public void ValidateAndGetExtension_AcceptsAllowedTypes(string fileName, string expectedExtension)
    {
        var ext = ResourceFileUploadValidator.ValidateAndGetExtension(fileName, null, 50, 10_000);
        Assert.Equal(expectedExtension, ext);
    }

    [Fact]
    public void SanitizeDisplayFileName_StripsPathSegments()
    {
        var name = ResourceFileUploadValidator.SanitizeDisplayFileName("../../secret/notes.txt");
        Assert.Equal("notes.txt", name);
    }
}
