using EducationalCompanion.Api.Dtos.Recommendations;

namespace EducationalCompanion.Api.Services.Abstractions;

// Write side for recommendations (AI service pushes batch for a user).
// Read side is handled by IUserEdmService.GetRecommendationsAsync.
public interface IRecommendationService
{
    // Creates or replaces recommendations for a user. Validates user and all learning resource ids.
    Task<CreatedRecommendationsResponse> CreateBatchForUserAsync(
        string userId,
        CreateRecommendationsBatchRequest request,
        CancellationToken ct = default);
}
