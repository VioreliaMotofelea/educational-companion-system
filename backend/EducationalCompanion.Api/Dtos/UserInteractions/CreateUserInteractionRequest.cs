using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.UserInteractions;

public record CreateUserInteractionRequest(
    [param: Required, MinLength(1), MaxLength(450)] string UserId,
    [param: Required] Guid LearningResourceId,
    [param: Required, MaxLength(50)] string InteractionType,
    [param: Range(1, 5)] int? Rating,
    [param: Range(0, 9999)] int? TimeSpentMinutes
);
