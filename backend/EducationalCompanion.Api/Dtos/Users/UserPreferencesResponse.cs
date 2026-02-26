namespace EducationalCompanion.Api.Dtos.Users;

public record UserPreferencesResponse(
    int? PreferredDifficulty,
    string? PreferredContentTypesCsv,
    string? PreferredTopicsCsv
);
