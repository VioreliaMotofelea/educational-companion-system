using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Api.Services.Abstractions;

public record ResourceTextExtractionResult(
    string NormalizedText,
    ResourceTextExtractionMethod Method,
    bool RequiresOcrFallback);

public interface IResourceTextExtractionService
{
    ResourceTextExtractionResult Extract(Stream fileStream, string extension);
}
