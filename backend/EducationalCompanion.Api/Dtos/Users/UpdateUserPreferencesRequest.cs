using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Users;

public record UpdateUserPreferencesRequest(
    [param: Range(1, 5)] int? PreferredDifficulty,
    [param: MaxLength(200)] string? PreferredContentTypesCsv,
    [param: MaxLength(500)] string? PreferredTopicsCsv
);
