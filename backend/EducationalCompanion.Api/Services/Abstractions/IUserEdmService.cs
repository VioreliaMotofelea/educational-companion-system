using EducationalCompanion.Api.Dtos.Analytics;
using EducationalCompanion.Api.Dtos.Mastery;
using EducationalCompanion.Api.Dtos.Recommendations;

namespace EducationalCompanion.Api.Services.Abstractions;

// Educational Data Mining layer: analytics, recommendations, and mastery for adaptive learning.
public interface IUserEdmService
{
    Task<UserAnalyticsResponse> GetAnalyticsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserRecommendationItemResponse>> GetRecommendationsAsync(string userId, int? limit, CancellationToken ct = default);
    Task<UserMasteryResponse> GetMasteryAsync(string userId, CancellationToken ct = default);
}
