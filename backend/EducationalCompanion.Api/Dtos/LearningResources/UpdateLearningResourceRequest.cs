using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.LearningResources;

public record UpdateLearningResourceRequest(
    [property: Required, MinLength(1), MaxLength(200)] string Title,
    [property: MaxLength(2000)] string? Description,
    [property: Required, MinLength(1), MaxLength(100)] string Topic,
    [property: Range(1, 5)] int Difficulty,
    [property: Range(1, 9999)] int EstimatedDurationMinutes,
    [property: Required, MaxLength(50)] string ContentType
);