namespace EducationalCompanion.Api.Dtos.Tasks;

public record StudyTaskResponse(
    Guid Id,
    string UserId,
    Guid? LearningResourceId,
    string? LearningResourceTitle,
    string Title,
    string? Notes,
    DateTime DeadlineUtc,
    int EstimatedMinutes,
    int Priority,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

