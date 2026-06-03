namespace EducationalCompanion.Api.Dtos.ResourceFiles;

public record ResourceExtractedTextResponse(
    Guid LearningResourceId,
    Guid ResourceFileId,
    string? Summary,
    string ExtractionMethod,
    int CharacterCount,
    DateTime CreatedAtUtc
);
