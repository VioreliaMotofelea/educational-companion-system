using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Users;

public record UpdateUserPreferencesRequest(
    [property: Range(1, 5)] int? PreferredDifficulty,
    [property: MaxLength(200)] string? PreferredContentTypesCsv,
    [property: MaxLength(500)] string? PreferredTopicsCsv
);
