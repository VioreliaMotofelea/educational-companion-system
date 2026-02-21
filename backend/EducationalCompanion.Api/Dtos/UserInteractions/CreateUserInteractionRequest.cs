using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.UserInteractions;

public record CreateUserInteractionRequest(
    [property: Required, MinLength(1), MaxLength(450)] string UserId,
    [property: Required] Guid LearningResourceId,
    [property: Required, MaxLength(50)] string InteractionType,
    [property: Range(1, 5)] int? Rating,
    [property: Range(0, 9999)] int? TimeSpentMinutes
);
