namespace EducationalCompanion.Api.Dtos.Recommendations;

public record GenerateRecommendationsResponse(
    string UserId,
    int GeneratedCount
);

