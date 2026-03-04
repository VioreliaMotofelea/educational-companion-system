namespace EducationalCompanion.Api.Dtos.Recommendations;

// Batch of recommendations for a user. AI service can replace all or append.
public record CreateRecommendationsBatchRequest(
    IReadOnlyList<CreateRecommendationItemRequest> Recommendations,
    bool ReplaceExisting = true
);
