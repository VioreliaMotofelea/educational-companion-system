using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class RecommendationServiceTests
{
    private static readonly Guid R1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid R2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string UserId = "user-1";

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenRecommendationsNull()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(Recommendations: null!, ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenEmptyRecommendations()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(new List<CreateRecommendationItemRequest>(), ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenUserMissing()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: false);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Algo", "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<UserProfileNotFoundException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenLearningResourceMissing()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Algo", "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<LearningResourceNotFoundException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ValidatesScoreRange()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 1.5, "Algo", "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public async Task CreateBatchForUserAsync_AcceptsScoreBoundaries(double score)
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, score, " Algo ", "  Explanation  "),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.True(result.ReplacedExisting);
        Assert.Equal(1, result.CreatedCount);
        var stored = Assert.Single(recRepo.Stored);
        Assert.Equal(score, stored.Score);
        Assert.Equal("Algo", stored.AlgorithmUsed);
        Assert.Equal("Explanation", stored.Explanation);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_TrimsAlgorithmUsedAndExplanation_AndReplacesExisting()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "t2", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.2, "  Algo  ", "  hi  "),
                new CreateRecommendationItemRequest(R2, 0.9, "Hybrid", "  Explanation with spaces  "),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.True(result.ReplacedExisting);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(1, recRepo.DeleteCalls); // called once
        Assert.Equal(1, recRepo.SaveCalls); // saved once
        Assert.Equal(2, recRepo.Stored.Count);

        Assert.Contains(recRepo.Stored, r => r.LearningResourceId == R1 && r.AlgorithmUsed == "Algo" && r.Explanation == "hi");
        Assert.Contains(recRepo.Stored, r => r.LearningResourceId == R2 && r.Explanation == "Explanation with spaces");
    }

    [Fact]
    public async Task CreateBatchForUserAsync_DoesNotReplaceExistingWhenFlagFalse()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.1, "Algo", "Explanation"),
            },
            ReplaceExisting: false);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.False(result.ReplacedExisting);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, recRepo.DeleteCalls); // no delete
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBatchForUserAsync_ThrowsWhenAlgorithmUsedMissing(string? algorithmUsed)
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, algorithmUsed ?? null!, "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenAlgorithmUsedTooLong()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var tooLongAlgo = new string('a', 51); // Max is 50
        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, tooLongAlgo, "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBatchForUserAsync_ThrowsWhenExplanationMissing(string? explanation)
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Algo", explanation ?? null!),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenExplanationTooLong()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(userRepo, resourceRepo, recRepo);

        var tooLongExplanation = new string('b', 1001); // Max is 1000
        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Algo", tooLongExplanation),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    private sealed class FakeUserProfileRepository : IUserProfileRepository
    {
        private readonly bool _hasUser;
        public FakeUserProfileRepository(bool hasUser) => _hasUser = hasUser;

        public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_hasUser ? new UserProfile { UserId = userId } : null);

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

    private sealed class FakeLearningResourceRepository : ILearningResourceRepository
    {
        private readonly Dictionary<Guid, LearningResource> _resourcesById;
        public FakeLearningResourceRepository(Dictionary<Guid, LearningResource> resourcesById) => _resourcesById = resourcesById;

        public Task<IReadOnlyList<LearningResource>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(Array.Empty<LearningResource>());

        public Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _resourcesById.TryGetValue(id, out var res);
            return Task.FromResult(res);
        }

        public Task<IReadOnlyList<LearningResource>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(_resourcesById.Values.ToList());

        public IQueryable<LearningResource> Query() => _resourcesById.Values.AsQueryable();

        public Task AddAsync(LearningResource entity, CancellationToken ct = default)
        {
            _resourcesById[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Update(LearningResource entity) { }
        public void Remove(LearningResource entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeRecommendationRepository : IRecommendationRepository
    {
        private readonly string? _seedUserId;
        public int DeleteCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public List<Recommendation> Stored { get; } = new();

        public FakeRecommendationRepository(string? seedUserId = null)
        {
            _seedUserId = seedUserId;
            if (!string.IsNullOrWhiteSpace(seedUserId))
            {
                Stored.Add(new Recommendation
                {
                    UserId = seedUserId,
                    LearningResourceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Score = 0.5,
                    AlgorithmUsed = "Seed",
                    Explanation = "Seed"
                });
            }
        }

        public Task<IReadOnlyList<Recommendation>> GetByUserIdWithResourceAsync(string userId, int? limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recommendation>>(Stored.Where(x => x.UserId == userId).ToList());

        public Task<int> DeleteByUserIdAsync(string userId, CancellationToken ct = default)
        {
            DeleteCalls++;
            var removed = Stored.RemoveAll(r => r.UserId == userId);
            return Task.FromResult(removed);
        }

        public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recommendation?>(null);
        public Task<IReadOnlyList<Recommendation>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recommendation>>(Stored.ToList());
        public IQueryable<Recommendation> Query() => Stored.AsQueryable();

        public Task AddAsync(Recommendation entity, CancellationToken ct = default)
        {
            Stored.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(Recommendation entity) { }
        public void Remove(Recommendation entity) { }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(0);
        }
    }
}

