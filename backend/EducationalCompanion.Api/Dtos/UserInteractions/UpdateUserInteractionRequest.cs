using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.UserInteractions;

public record UpdateUserInteractionRequest(
    [param: MaxLength(50)] string? InteractionType,
    [param: Range(1, 5)] int? Rating,
    [param: Range(0, 9999)] int? TimeSpentMinutes
);
