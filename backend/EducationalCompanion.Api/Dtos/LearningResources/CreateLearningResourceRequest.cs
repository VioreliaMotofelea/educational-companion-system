namespace EducationalCompanion.Api.Dtos.LearningResources;

public record CreateLearningResourceRequest(
    string Title,
    string? Description,
    string Topic,
    int Difficulty,
    int EstimatedDurationMinutes,
    string ContentType
);