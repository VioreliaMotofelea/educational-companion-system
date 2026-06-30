using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Tasks;

public record UpdateStudyTaskRequest(
    Guid? LearningResourceId,
    [Required, MinLength(3)] string Title,
    string? Notes,
    DateTime DeadlineUtc,
    [Range(1, 600)] int EstimatedMinutes,
    [Range(1, 5)] int Priority
);

