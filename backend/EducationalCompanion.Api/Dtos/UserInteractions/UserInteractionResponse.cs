namespace EducationalCompanion.Api.Dtos.UserInteractions;

public record UserInteractionResponse(
    Guid Id,
    string UserId,
    Guid LearningResourceId,
    string InteractionType,
    int? Rating,
    int? TimeSpentMinutes,
    DateTime CreatedAtUtc
);
