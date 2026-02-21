using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.UserInteractions;

public record UpdateUserInteractionRequest(
    [property: MaxLength(50)] string? InteractionType,
    [property: Range(1, 5)] int? Rating,
    [property: Range(0, 9999)] int? TimeSpentMinutes
);
