using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class LearningResourceServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ThrowsWhenNotFound()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        await Assert.ThrowsAsync<LearningResourceNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ParsesContentTypeAndValidatesRanges()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: "Desc",
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: 60,
            ContentType: "Article");

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Title", result.Title);
        Assert.Equal("Desc", result.Description);
        Assert.Equal("Python", result.Topic);
        Assert.Equal(3, result.Difficulty);
        Assert.Equal(60, result.EstimatedDurationMinutes);
        Assert.Equal(ResourceContentType.Article.ToString(), result.ContentType);

        Assert.Equal(1, repo.AddCalls);
        Assert.Equal(1, repo.SaveCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_ThrowsWhenEstimatedDurationInvalid(int minutes)
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: "Desc",
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: minutes,
            ContentType: "Article");

        await Assert.ThrowsAsync<InvalidEstimatedDurationException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task CreateAsync_ThrowsWhenDifficultyInvalid(int difficulty)
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: "Desc",
            Topic: "Python",
            Difficulty: difficulty,
            EstimatedDurationMinutes: 60,
            ContentType: "Article");

        await Assert.ThrowsAsync<InvalidDifficultyException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenContentTypeInvalid()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: "Desc",
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: 60,
            ContentType: "NotAType");

        await Assert.ThrowsAsync<InvalidContentTypeException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndCallsRepo()
    {
        var existingId = Guid.NewGuid();
        var existing = new LearningResource
        {
            Id = existingId,
            Title = "Old",
            Description = "OldDesc",
            Topic = "OldTopic",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Video
        };

        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource> { [existingId] = existing });
        var service = new LearningResourceService(repo);

        var request = new UpdateLearningResourceRequest(
            Title: "NewTitle",
            Description: "NewDesc",
            Topic: "NewTopic",
            Difficulty: 4,
            EstimatedDurationMinutes: 90,
            ContentType: "Quiz");

        await service.UpdateAsync(existingId, request, CancellationToken.None);

        Assert.Equal(1, repo.UpdateCalls);
        Assert.Equal(1, repo.SaveCalls);

        var stored = repo.Resources[existingId];
        Assert.Equal("NewTitle", stored.Title);
        Assert.Equal("NewDesc", stored.Description);
        Assert.Equal("NewTopic", stored.Topic);
        Assert.Equal(4, stored.Difficulty);
        Assert.Equal(90, stored.EstimatedDurationMinutes);
        Assert.Equal(ResourceContentType.Quiz, stored.ContentType);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenNotFound()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = new LearningResourceService(repo);

        await Assert.ThrowsAsync<LearningResourceNotFoundException>(() => service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntityAndSaves()
    {
        var existingId = Guid.NewGuid();
        var existing = new LearningResource
        {
            Id = existingId,
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article
        };

        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource> { [existingId] = existing });
        var service = new LearningResourceService(repo);

        await service.DeleteAsync(existingId, CancellationToken.None);

        Assert.Equal(1, repo.RemoveCalls);
        Assert.Equal(1, repo.SaveCalls);
        Assert.False(repo.Resources.ContainsKey(existingId));
    }

    private sealed class FakeLearningResourceRepository : ILearningResourceRepository
    {
        public Dictionary<Guid, LearningResource> Resources { get; }
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public FakeLearningResourceRepository(Dictionary<Guid, LearningResource> resources) => Resources = resources;

        public Task<IReadOnlyList<LearningResource>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(Array.Empty<LearningResource>());

        public Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Resources.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }

        public Task<IReadOnlyList<LearningResource>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(Resources.Values.ToList());

        public IQueryable<LearningResource> Query() => Resources.Values.AsQueryable();

        public Task AddAsync(LearningResource entity, CancellationToken ct = default)
        {
            AddCalls++;
            Resources[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Update(LearningResource entity)
        {
            UpdateCalls++;
            Resources[entity.Id] = entity;
        }

        public void Remove(LearningResource entity)
        {
            RemoveCalls++;
            Resources.Remove(entity.Id);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(0);
        }
    }
}

