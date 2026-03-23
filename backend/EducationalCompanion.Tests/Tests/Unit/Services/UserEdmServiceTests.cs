using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Api.Dtos.Analytics;
using EducationalCompanion.Api.Dtos.Mastery;
using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Edm;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class UserEdmServiceTests
{
    [Fact]
    public async Task GetAnalyticsAsync_ThrowsWhenUserMissing()
    {
        var userRepo = new FakeUserProfileRepository(profile: null);
        var edmRepo = new FakeUserEdmReadRepository(kpis: new UserAnalyticsKpisData(
            0, 0, 0, null, 0, 0, 1, 0, 0, 0, 0), topicMastery: new List<TopicMasteryData>());
        var recRepo = new FakeRecommendationRepository();

        var service = new UserEdmService(userRepo, recRepo, edmRepo);

        await Assert.ThrowsAsync<UserProfileNotFoundException>(() => service.GetAnalyticsAsync("missing-user", CancellationToken.None));
    }

    [Fact]
    public async Task GetAnalyticsAsync_BuildsNoActivitySummary_WhenAllKpisZero()
    {
        var userProfile = new UserProfile { UserId = "user-1", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);
        var edmRepo = new FakeUserEdmReadRepository(
            kpis: new UserAnalyticsKpisData(
                TotalResourcesViewed: 0,
                TotalResourcesCompleted: 0,
                CompletionRatePercent: 0,
                AverageRatingGiven: null,
                TotalTimeSpentMinutes: 0,
                TotalXpEarned: 0,
                CurrentLevel: 1,
                TasksCompleted: 0,
                TasksPending: 0,
                TasksOverdue: 0,
                GamificationEventsCount: 0),
            topicMastery: new List<TopicMasteryData>());
        var recRepo = new FakeRecommendationRepository();

        var service = new UserEdmService(userRepo, recRepo, edmRepo);
        var result = await service.GetAnalyticsAsync("user-1", CancellationToken.None);

        Assert.Equal("user-1", result.UserId);
        Assert.Equal("No activity yet. Start by viewing and completing resources.", result.Summary.SummaryText);
        Assert.Equal(0, result.Kpis.TotalResourcesViewed);
        Assert.Equal(1, result.Kpis.CurrentLevel);
        Assert.NotEqual(default, result.Summary.ComputedAtUtc);
    }

    [Fact]
    public async Task GetRecommendationsAsync_FiltersOutNullLearningResource()
    {
        var userProfile = new UserProfile { UserId = "user-1", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);

        var learningResource = new LearningResource
        {
            Id = Guid.NewGuid(),
            Title = "t",
            Description = "d",
            Topic = "Python",
            Difficulty = 2,
            EstimatedDurationMinutes = 30,
            ContentType = ResourceContentType.Article,
        };

        var withResource = new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            LearningResourceId = learningResource.Id,
            Score = 0.9,
            AlgorithmUsed = "Algo",
            Explanation = "Exp",
            LearningResource = learningResource,
            CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var withoutResource = new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            LearningResourceId = Guid.NewGuid(),
            Score = 0.1,
            AlgorithmUsed = "Algo2",
            Explanation = "Exp2",
            LearningResource = null
        };

        var recRepo = new FakeRecommendationRepository(new List<Recommendation> { withResource, withoutResource });
        var edmRepo = new FakeUserEdmReadRepository(kpis: null, topicMastery: new List<TopicMasteryData>());

        var service = new UserEdmService(userRepo, recRepo, edmRepo);
        var result = await service.GetRecommendationsAsync("user-1", limit: null, CancellationToken.None);

        Assert.Single(result);
        var item = result.Single();
        Assert.Equal(withResource.Id, item.RecommendationId);
        Assert.Equal(withResource.Score, item.Score);
        Assert.Equal("Algo", item.AlgorithmUsed);
        Assert.Equal("Exp", item.Explanation);
        Assert.Equal(learningResource.Id, item.Resource.Id);
        Assert.Equal("Python", item.Resource.Topic);
    }

    [Fact]
    public async Task GetMasteryAsync_DerivesMasteryLevels_AndSuggestedDifficulty()
    {
        var userProfile = new UserProfile { UserId = "user-1", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);

        var topicData = new List<TopicMasteryData>
        {
            new TopicMasteryData("Python", ResourcesCompleted: 0, AverageRating: null, AverageDifficultyCompleted: 1.0),
            new TopicMasteryData("AI", ResourcesCompleted: 5, AverageRating: 4.2, AverageDifficultyCompleted: 4.0),
            new TopicMasteryData("Web", ResourcesCompleted: 3, AverageRating: null, AverageDifficultyCompleted: 2.0),
            new TopicMasteryData("C#", ResourcesCompleted: 2, AverageRating: 4.0, AverageDifficultyCompleted: 3.0)
        };

        var edmRepo = new FakeUserEdmReadRepository(kpis: null, topicMastery: topicData);
        var recRepo = new FakeRecommendationRepository();

        var service = new UserEdmService(userRepo, recRepo, edmRepo);
        var result = await service.GetMasteryAsync("user-1", CancellationToken.None);

        Assert.Equal("user-1", result.UserId);
        Assert.Equal(4, result.TopicMastery.Count);

        var pythonItem = result.TopicMastery.Single(x => x.Topic == "Python");
        Assert.Equal("None", pythonItem.MasteryLevel);

        var aiItem = result.TopicMastery.Single(x => x.Topic == "AI");
        Assert.Equal("Advanced", aiItem.MasteryLevel);

        var webItem = result.TopicMastery.Single(x => x.Topic == "Web");
        Assert.Equal("Intermediate", webItem.MasteryLevel);

        var csharpItem = result.TopicMastery.Single(x => x.Topic == "C#");
        Assert.Equal("Beginner", csharpItem.MasteryLevel);

        Assert.Equal(4, result.SuggestedDifficulty);
        Assert.Equal(
            "Based on 4 topic(s); average completed difficulty 2.5, rating 4.1. Suggested next: 4.",
            result.SuggestedDifficultyReason);
        Assert.NotEqual(default, result.ComputedAtUtc);
    }

    private sealed class FakeUserProfileRepository : IUserProfileRepository
    {
        private readonly UserProfile? _profile;
        public FakeUserProfileRepository(UserProfile? profile) => _profile = profile;

        public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_profile != null && _profile.UserId == userId ? _profile : null);

        public Task<UserProfile?> GetByUserIdWithPreferencesAsync(string userId, CancellationToken ct = default) =>
            GetByUserIdAsync(userId, ct);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<UserProfile?>(null);
        public Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserProfile>>(Array.Empty<UserProfile>());
        public IQueryable<UserProfile> Query() => Array.Empty<UserProfile>().AsQueryable();
        public Task AddAsync(UserProfile entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(UserProfile entity) { }
        public void Remove(UserProfile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeRecommendationRepository : IRecommendationRepository
    {
        private readonly List<Recommendation> _items;
        public FakeRecommendationRepository(List<Recommendation>? items = null) => _items = items ?? new List<Recommendation>();

        public Task<IReadOnlyList<Recommendation>> GetByUserIdWithResourceAsync(string userId, int? limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recommendation>>(_items.Where(x => x.UserId == userId).ToList());

        public Task<int> DeleteByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recommendation?>(null);
        public Task<IReadOnlyList<Recommendation>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recommendation>>(_items.ToList());
        public IQueryable<Recommendation> Query() => _items.AsQueryable();
        public Task AddAsync(Recommendation entity, CancellationToken ct = default) { _items.Add(entity); return Task.CompletedTask; }
        public void Update(Recommendation entity) { }
        public void Remove(Recommendation entity) { _items.Remove(entity); }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeUserEdmReadRepository : IUserEdmReadRepository
    {
        private readonly UserAnalyticsKpisData? _kpis;
        private readonly IReadOnlyList<TopicMasteryData> _topicMastery;

        public FakeUserEdmReadRepository(UserAnalyticsKpisData? kpis, List<TopicMasteryData> topicMastery)
        {
            _kpis = kpis;
            _topicMastery = topicMastery;
        }

        public Task<UserAnalyticsKpisData?> GetUserAnalyticsKpisAsync(string userId, CancellationToken ct = default) => Task.FromResult(_kpis);

        public Task<IReadOnlyList<TopicMasteryData>> GetTopicMasteryDataAsync(string userId, CancellationToken ct = default) => Task.FromResult(_topicMastery);
    }
}

