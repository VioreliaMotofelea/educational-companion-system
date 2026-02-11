namespace EducationalCompanion.Api.Dtos.LearningResources;

public record LearningResourceResponse(
    Guid Id,
    string Title,
    string? Description,
    string Topic,
    int Difficulty,
    int EstimatedDurationMinutes,
    string ContentType
);