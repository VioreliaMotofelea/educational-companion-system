namespace EducationalCompanion.Api.Dtos.Recommendations;

// Single recommendation item for batch create (from AI service).
public record CreateRecommendationItemRequest(
    Guid LearningResourceId,
    double Score,
    string AlgorithmUsed,
    string Explanation
);
