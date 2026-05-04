using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using EducationalCompanion.Tests.Tests.Unit.Fakes;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class UserInteractionServiceTests
{
    [Fact]
    public async Task CreateAsync_ThrowsWhenLearningResourceMissing()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: Guid.NewGuid(),
            InteractionType: "Viewed",
            Rating: null,
            TimeSpentMinutes: 10);

        await Assert.ThrowsAsync<LearningResourceNotFoundException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenInteractionTypeInvalid()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningId = Guid.NewGuid();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [learningId] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: learningId,
            InteractionType: "NotAType",
            Rating: null,
            TimeSpentMinutes: 10);

        await Assert.ThrowsAsync<InvalidInteractionTypeException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_RatedWithoutRating_ThrowsValidationException()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningId = Guid.NewGuid();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [learningId] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: learningId,
            InteractionType: "Rated",
            Rating: null,
            TimeSpentMinutes: 10);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task CreateAsync_RatingOutOfRange_ThrowsInvalidRatingException(int rating)
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningId = Guid.NewGuid();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [learningId] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: learningId,
            InteractionType: "Rated",
            Rating: rating,
            TimeSpentMinutes: 10);

        await Assert.ThrowsAsync<InvalidRatingException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_NegativeTimeSpent_ThrowsValidationException()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningId = Guid.NewGuid();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [learningId] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: learningId,
            InteractionType: "Viewed",
            Rating: null,
            TimeSpentMinutes: -1);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_Valid_RatesAndSaves()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningId = Guid.NewGuid();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [learningId] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article }
        });
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        var request = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: learningId,
            InteractionType: "Rated",
            Rating: 5,
            TimeSpentMinutes: 25);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("user-1", result.UserId);
        Assert.Equal(learningId, result.LearningResourceId);
        Assert.Equal(InteractionType.Rated.ToString(), result.InteractionType);
        Assert.Equal(5, result.Rating);
        Assert.Equal(25, result.TimeSpentMinutes);
        Assert.Equal(1, interactionRepo.AddCalls);
        Assert.Equal(1, interactionRepo.SaveCalls);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        var interactionRepo = new FakeUserInteractionRepository();
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        await Assert.ThrowsAsync<UserInteractionNotFoundException>(() =>
            service.UpdateAsync(Guid.NewGuid(), new UpdateUserInteractionRequest(
                InteractionType: "Viewed",
                Rating: null,
                TimeSpentMinutes: null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_WhenProvided()
    {
        var existingId = Guid.NewGuid();
        var existing = new UserInteraction
        {
            Id = existingId,
            UserId = "user-1",
            LearningResourceId = Guid.NewGuid(),
            InteractionType = InteractionType.Viewed,
            Rating = null,
            TimeSpentMinutes = 0
        };

        var interactionRepo = new FakeUserInteractionRepository(new Dictionary<Guid, UserInteraction> { [existingId] = existing });
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new UserInteractionService(interactionRepo, learningRepo, new NoOpStudyTaskService());

        await service.UpdateAsync(existingId, new UpdateUserInteractionRequest(
            InteractionType: "Completed",
            Rating: 4,
            TimeSpentMinutes: 12), CancellationToken.None);

        Assert.Equal(1, interactionRepo.UpdateCalls);
        Assert.Equal(1, interactionRepo.SaveCalls);

        var stored = interactionRepo.Stored[existingId];
        Assert.Equal(InteractionType.Completed, stored.InteractionType);
        Assert.Equal(4, stored.Rating);
        Assert.Equal(12, stored.TimeSpentMinutes);
    }

    private sealed class FakeUserInteractionRepository : IUserInteractionRepository
    {
        public Dictionary<Guid, UserInteraction> Stored { get; }
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public FakeUserInteractionRepository(Dictionary<Guid, UserInteraction>? seed = null)
        {
            Stored = seed ?? new Dictionary<Guid, UserInteraction>();
        }

        public Task<IReadOnlyList<UserInteraction>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserInteraction>>(Stored.Values.ToList());

        public Task<UserInteraction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Stored.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }

        public IQueryable<UserInteraction> Query() => Stored.Values.AsQueryable();

        public Task AddAsync(UserInteraction entity, CancellationToken ct = default)
        {
            AddCalls++;
            Stored[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Update(UserInteraction entity)
        {
            UpdateCalls++;
            Stored[entity.Id] = entity;
        }

        public void Remove(UserInteraction entity)
        {
            RemoveCalls++;
            Stored.Remove(entity.Id);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<UserInteraction>> GetByUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserInteraction>>(Stored.Values.Where(x => x.UserId == userId).ToList());

        public Task<IReadOnlyList<UserInteraction>> SearchAsync(string? userId, Guid? learningResourceId, string? interactionType, CancellationToken ct = default)
        {
            var query = Stored.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(x => x.UserId == userId);
            if (learningResourceId.HasValue) query = query.Where(x => x.LearningResourceId == learningResourceId.Value);
            if (!string.IsNullOrWhiteSpace(interactionType) && Enum.TryParse<InteractionType>(interactionType, true, out var parsed))
                query = query.Where(x => x.InteractionType == parsed);

            return Task.FromResult<IReadOnlyList<UserInteraction>>(query.OrderByDescending(x => x.CreatedAtUtc).ToList());
        }

        public Task<bool> ExistsAsync(string userId, Guid learningResourceId, CancellationToken ct = default) =>
            Task.FromResult(Stored.Values.Any(x => x.UserId == userId && x.LearningResourceId == learningResourceId));
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

        public Task AddAsync(LearningResource entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(LearningResource entity) { }
        public void Remove(LearningResource entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

