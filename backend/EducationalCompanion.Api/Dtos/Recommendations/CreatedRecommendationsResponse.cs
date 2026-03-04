namespace EducationalCompanion.Api.Dtos.Recommendations;

// Response after writing recommendations (batch).
public record CreatedRecommendationsResponse(
    string UserId,
    int CreatedCount,
    bool ReplacedExisting
);
