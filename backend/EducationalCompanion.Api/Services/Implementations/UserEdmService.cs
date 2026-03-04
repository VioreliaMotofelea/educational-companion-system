using EducationalCompanion.Api.Dtos.Analytics;
using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Dtos.Mastery;
using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Edm;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class UserEdmService : IUserEdmService
{
    private const int MinDifficulty = 1;
    private const int MaxDifficulty = 5;

    private readonly IUserProfileRepository _userProfileRepo;
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly IUserEdmReadRepository _edmReadRepo;

    public UserEdmService(
        IUserProfileRepository userProfileRepo,
        IRecommendationRepository recommendationRepo,
        IUserEdmReadRepository edmReadRepo)
    {
        _userProfileRepo = userProfileRepo;
        _recommendationRepo = recommendationRepo;
        _edmReadRepo = edmReadRepo;
    }

    public async Task<UserAnalyticsResponse> GetAnalyticsAsync(string userId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var kpisData = await _edmReadRepo.GetUserAnalyticsKpisAsync(userId, ct);
        if (kpisData is null)
            throw new UserProfileNotFoundException(userId);

        var summaryText = BuildAnalyticsSummary(kpisData);
        var summary = new UserAnalyticsSummary(summaryText, DateTime.UtcNow);
        var kpis = new UserAnalyticsKpis(
            kpisData.TotalResourcesViewed,
            kpisData.TotalResourcesCompleted,
            kpisData.CompletionRatePercent,
            kpisData.AverageRatingGiven,
            kpisData.TotalTimeSpentMinutes,
            kpisData.TotalXpEarned,
            kpisData.CurrentLevel,
            kpisData.TasksCompleted,
            kpisData.TasksPending,
            kpisData.TasksOverdue,
            kpisData.GamificationEventsCount
        );

        return new UserAnalyticsResponse(userId, summary, kpis);
    }

    public async Task<IReadOnlyList<UserRecommendationItemResponse>> GetRecommendationsAsync(string userId, int? limit, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var recommendations = await _recommendationRepo.GetByUserIdWithResourceAsync(userId, limit, ct);
        return recommendations
            .Where(r => r.LearningResource != null)
            .Select(r => MapToRecommendationItem(r))
            .ToList();
    }

    public async Task<UserMasteryResponse> GetMasteryAsync(string userId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var topicData = await _edmReadRepo.GetTopicMasteryDataAsync(userId, ct);
        var topicMastery = topicData
            .Select(d => new TopicMasteryItem(
                d.Topic,
                d.ResourcesCompleted,
                d.AverageRating,
                d.AverageDifficultyCompleted,
                DeriveMasteryLevel(d)
            ))
            .ToList();

        var (suggestedDifficulty, reason) = DeriveSuggestedDifficulty(topicData);
        return new UserMasteryResponse(
            userId,
            topicMastery,
            suggestedDifficulty,
            reason,
            DateTime.UtcNow
        );
    }

    private async Task EnsureUserExistsAsync(string userId, CancellationToken ct)
    {
        var profile = await _userProfileRepo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);
    }

    private static string BuildAnalyticsSummary(UserAnalyticsKpisData kpis)
    {
        var parts = new List<string>();
        if (kpis.TotalResourcesViewed > 0)
            parts.Add($"{kpis.TotalResourcesCompleted} of {kpis.TotalResourcesViewed} viewed resources completed ({kpis.CompletionRatePercent}% completion rate).");
        if (kpis.TotalTimeSpentMinutes > 0)
            parts.Add($"Total study time: {kpis.TotalTimeSpentMinutes} minutes.");
        if (kpis.TotalXpEarned > 0)
            parts.Add($"Level {kpis.CurrentLevel}, {kpis.TotalXpEarned} XP; {kpis.GamificationEventsCount} gamification events.");
        if (kpis.TasksCompleted + kpis.TasksPending + kpis.TasksOverdue > 0)
            parts.Add($"Tasks: {kpis.TasksCompleted} completed, {kpis.TasksPending} pending, {kpis.TasksOverdue} overdue.");
        return parts.Count > 0 ? string.Join(" ", parts) : "No activity yet. Start by viewing and completing resources.";
    }

    private static UserRecommendationItemResponse MapToRecommendationItem(Recommendation r)
    {
        var res = r.LearningResource!;
        return new UserRecommendationItemResponse(
            r.Id,
            new LearningResourceResponse(
                res.Id,
                res.Title,
                res.Description,
                res.Topic,
                res.Difficulty,
                res.EstimatedDurationMinutes,
                res.ContentType.ToString()
            ),
            r.Score,
            r.AlgorithmUsed,
            r.Explanation,
            r.CreatedAtUtc
        );
    }

    private static string DeriveMasteryLevel(TopicMasteryData d)
    {
        if (d.ResourcesCompleted == 0) return "None";
        if (d.ResourcesCompleted >= 5 && (d.AverageRating ?? 0) >= 4.0) return "Advanced";
        if (d.ResourcesCompleted >= 3) return "Intermediate";
        return "Beginner";
    }

    private static (int SuggestedDifficulty, string? Reason) DeriveSuggestedDifficulty(IReadOnlyList<TopicMasteryData> topicData)
    {
        if (topicData.Count == 0)
            return (MinDifficulty, "No completed topics yet; start with difficulty 1.");

        var avgDifficulty = topicData.Average(d => d.AverageDifficultyCompleted);
        var avgRating = topicData.Where(d => d.AverageRating.HasValue).Select(d => d.AverageRating!.Value).DefaultIfEmpty(0).Average();
        var suggested = avgRating >= 4.0
            ? (int)Math.Min(MaxDifficulty, Math.Ceiling(avgDifficulty) + 1)
            : (int)Math.Round(avgDifficulty);
        suggested = Math.Clamp(suggested, MinDifficulty, MaxDifficulty);
        var reason = $"Based on {topicData.Count} topic(s); average completed difficulty {avgDifficulty:F1}, rating {avgRating:F1}. Suggested next: {suggested}.";
        return (suggested, reason);
    }
}
