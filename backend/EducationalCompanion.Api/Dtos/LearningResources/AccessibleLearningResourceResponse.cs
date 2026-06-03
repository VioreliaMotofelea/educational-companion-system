namespace EducationalCompanion.Api.Dtos.LearningResources;

public record AccessibleLearningResourceResponse(
    Guid Id,
    string Title,
    string? Description,
    string Topic,
    int Difficulty,
    int EstimatedDurationMinutes,
    string ContentType,
    string? SourceName,
    string? Url,
    string AccessType,
    string? AccessInstructions,
    string Visibility,
    string? ExtractedTextSummary,
    bool HasSupplementaryFile
);
