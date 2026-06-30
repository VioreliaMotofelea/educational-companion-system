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
using EducationalCompanion.Tests.Tests.Unit.Fakes;
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

        var service = CreateService(userRepo, recRepo, edmRepo);

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

        var service = CreateService(userRepo, recRepo, edmRepo);
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

        var service = CreateService(userRepo, recRepo, edmRepo, [learningResource]);
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

        var service = CreateService(userRepo, recRepo, edmRepo);
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

    [Fact]
    public async Task GetRecommendationsAsync_ExcludesInaccessibleResources()
    {
        var globalId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var courseOnlyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var catalog = new Dictionary<Guid, LearningResource>
        {
            [globalId] = new LearningResource
            {
                Id = globalId,
                Title = "Global",
                Topic = "DB",
                Difficulty = 1,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.Global
            },
            [courseOnlyId] = new LearningResource
            {
                Id = courseOnlyId,
                Title = "Course only",
                Topic = "DB",
                Difficulty = 2,
                EstimatedDurationMinutes = 15,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.CourseOnly
            }
        };

        var userProfile = new UserProfile { UserId = "demo-bianca", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);
        var edmRepo = new FakeUserEdmReadRepository(kpis: null, topicMastery: new List<TopicMasteryData>());
        var recRepo = new FakeRecommendationRepository(new List<Recommendation>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "demo-bianca",
                LearningResourceId = globalId,
                Score = 0.9,
                AlgorithmUsed = "Hybrid",
                Explanation = "ok",
                LearningResource = catalog[globalId]
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "demo-bianca",
                LearningResourceId = courseOnlyId,
                Score = 0.8,
                AlgorithmUsed = "Hybrid",
                Explanation = "hidden",
                LearningResource = catalog[courseOnlyId]
            }
        });

        var learningRepo = new FakeLearningResourceRepository(catalog);
        var accessRepo = new FakeResourceAccessRepository(learningRepo, new[] { globalId });
        var service = new UserEdmService(
            userRepo,
            recRepo,
            edmRepo,
            accessRepo,
            new FakeUserInteractionRepository());

        var result = await service.GetRecommendationsAsync("demo-bianca", limit: null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(globalId, result[0].Resource.Id);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ExcludesCompletedResources()
    {
        var resourceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var otherId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var catalog = new Dictionary<Guid, LearningResource>
        {
            [resourceId] = new LearningResource
            {
                Id = resourceId,
                Title = "Done",
                Topic = "DB",
                Difficulty = 1,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.Global
            },
            [otherId] = new LearningResource
            {
                Id = otherId,
                Title = "Next",
                Topic = "DB",
                Difficulty = 2,
                EstimatedDurationMinutes = 15,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.Global
            }
        };

        var userProfile = new UserProfile { UserId = "user-1", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);
        var edmRepo = new FakeUserEdmReadRepository(kpis: null, topicMastery: new List<TopicMasteryData>());
        var recRepo = new FakeRecommendationRepository(new List<Recommendation>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = resourceId,
                Score = 0.95,
                AlgorithmUsed = "Hybrid",
                Explanation = "done item",
                LearningResource = catalog[resourceId]
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = otherId,
                Score = 0.8,
                AlgorithmUsed = "Hybrid",
                Explanation = "next item",
                LearningResource = catalog[otherId]
            }
        });

        var interactionRepo = new FakeUserInteractionRepository(new Dictionary<Guid, UserInteraction>
        {
            [Guid.NewGuid()] = new UserInteraction
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = resourceId,
                InteractionType = InteractionType.Completed,
                CreatedAtUtc = DateTime.UtcNow
            }
        });

        var service = CreateService(userRepo, recRepo, edmRepo, catalog.Values, interactionRepo);
        var result = await service.GetRecommendationsAsync("user-1", limit: null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(otherId, result[0].Resource.Id);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ExcludesSkippedResources()
    {
        var resourceId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var otherId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var catalog = new Dictionary<Guid, LearningResource>
        {
            [resourceId] = new LearningResource
            {
                Id = resourceId,
                Title = "Skipped",
                Topic = "DB",
                Difficulty = 1,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.Global
            },
            [otherId] = new LearningResource
            {
                Id = otherId,
                Title = "Next",
                Topic = "DB",
                Difficulty = 2,
                EstimatedDurationMinutes = 15,
                ContentType = ResourceContentType.Article,
                Visibility = ResourceVisibility.Global
            }
        };

        var userProfile = new UserProfile { UserId = "user-1", Level = 1, Xp = 0, DailyAvailableMinutes = 60 };
        var userRepo = new FakeUserProfileRepository(profile: userProfile);
        var edmRepo = new FakeUserEdmReadRepository(kpis: null, topicMastery: new List<TopicMasteryData>());
        var recRepo = new FakeRecommendationRepository(new List<Recommendation>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = resourceId,
                Score = 0.95,
                AlgorithmUsed = "Hybrid",
                Explanation = "skipped item",
                LearningResource = catalog[resourceId]
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = otherId,
                Score = 0.8,
                AlgorithmUsed = "Hybrid",
                Explanation = "next item",
                LearningResource = catalog[otherId]
            }
        });

        var interactionRepo = new FakeUserInteractionRepository(new Dictionary<Guid, UserInteraction>
        {
            [Guid.NewGuid()] = new UserInteraction
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                LearningResourceId = resourceId,
                InteractionType = InteractionType.Skipped,
                CreatedAtUtc = DateTime.UtcNow
            }
        });

        var service = CreateService(userRepo, recRepo, edmRepo, catalog.Values, interactionRepo);
        var result = await service.GetRecommendationsAsync("user-1", limit: null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(otherId, result[0].Resource.Id);
    }

    private static UserEdmService CreateService(
        FakeUserProfileRepository userRepo,
        FakeRecommendationRepository recRepo,
        FakeUserEdmReadRepository edmRepo,
        IEnumerable<LearningResource>? catalog = null,
        FakeUserInteractionRepository? interactionRepo = null)
    {
        var resources = catalog?.ToDictionary(r => r.Id) ?? new Dictionary<Guid, LearningResource>();
        var learningRepo = new FakeLearningResourceRepository(resources);
        var accessRepo = new PermissiveResourceAccessRepository(learningRepo);
        return new UserEdmService(
            userRepo,
            recRepo,
            edmRepo,
            accessRepo,
            interactionRepo ?? new FakeUserInteractionRepository());
    }

    private sealed class FakeUserInteractionRepository : IUserInteractionRepository
    {
        private readonly Dictionary<Guid, UserInteraction> _stored;

        public FakeUserInteractionRepository(Dictionary<Guid, UserInteraction>? seed = null) =>
            _stored = seed ?? new Dictionary<Guid, UserInteraction>();

        public Task<IReadOnlyList<UserInteraction>> GetByUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserInteraction>>(_stored.Values.Where(x => x.UserId == userId).ToList());

        public Task<IReadOnlyList<UserInteraction>> SearchAsync(
            string? userId,
            Guid? learningResourceId,
            string? interactionType,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserInteraction>>(Array.Empty<UserInteraction>());

        public Task<bool> ExistsAsync(string userId, Guid learningResourceId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<UserInteraction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<UserInteraction?>(null);

        public Task<IReadOnlyList<UserInteraction>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserInteraction>>(_stored.Values.ToList());

        public IQueryable<UserInteraction> Query() => _stored.Values.AsQueryable();

        public Task AddAsync(UserInteraction entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(UserInteraction entity) { }
        public void Remove(UserInteraction entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeLearningResourceRepository : ILearningResourceRepository
    {
        private readonly Dictionary<Guid, LearningResource> _resources;
        public FakeLearningResourceRepository(Dictionary<Guid, LearningResource> resources) => _resources = resources;
        public Task<IReadOnlyList<LearningResource>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(_resources.Values.ToList());
        public Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _resources.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }
        public Task<IReadOnlyList<LearningResource>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(_resources.Values.ToList());
        public IQueryable<LearningResource> Query() => _resources.Values.AsQueryable();
        public Task AddAsync(LearningResource entity, CancellationToken ct = default) { _resources[entity.Id] = entity; return Task.CompletedTask; }
        public void Update(LearningResource entity) => _resources[entity.Id] = entity;
        public void Remove(LearningResource entity) => _resources.Remove(entity.Id);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
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

