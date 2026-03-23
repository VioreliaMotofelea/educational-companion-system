using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.LearningResources;

public record UpdateLearningResourceRequest(
    [param: Required, MinLength(1), MaxLength(200)] string Title,
    [param: MaxLength(2000)] string? Description,
    [param: Required, MinLength(1), MaxLength(100)] string Topic,
    [param: Range(1, 5)] int Difficulty,
    [param: Range(1, 9999)] int EstimatedDurationMinutes,
    [param: Required, MaxLength(50)] string ContentType
);