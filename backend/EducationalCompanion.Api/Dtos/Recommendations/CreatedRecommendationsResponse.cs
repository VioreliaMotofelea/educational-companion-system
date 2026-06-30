namespace EducationalCompanion.Api.Dtos.Recommendations;

public record CreatedRecommendationsResponse(
    string UserId,
    int CreatedCount,
    bool ReplacedExisting
);
