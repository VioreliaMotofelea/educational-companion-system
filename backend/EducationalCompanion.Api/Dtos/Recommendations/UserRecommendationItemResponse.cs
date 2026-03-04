using EducationalCompanion.Api.Dtos.LearningResources;

namespace EducationalCompanion.Api.Dtos.Recommendations;

// EDM layer: a single recommendation item (content) for the user.
public record UserRecommendationItemResponse(
    Guid RecommendationId,
    LearningResourceResponse Resource,
    double Score,
    string AlgorithmUsed,
    string Explanation,
    DateTime CreatedAtUtc
);
