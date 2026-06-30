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
using EducationalCompanion.Tests.Tests.Unit.Fakes;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class LearningResourceServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ThrowsWhenNotFound()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

        await Assert.ThrowsAsync<LearningResourceNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ParsesContentTypeAndValidatesRanges()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

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
        Assert.Equal(ResourceAccessType.NoDirectAccess.ToString(), result.AccessType);
        Assert.Equal(ResourceVisibility.Global.ToString(), result.Visibility);

        Assert.Equal(1, repo.AddCalls);
        Assert.Equal(1, repo.SaveCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_ThrowsWhenEstimatedDurationInvalid(int minutes)
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

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
        var service = CreateService(repo);

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
    public async Task CreateAsync_WithUrl_DefaultsAccessTypeToExternalUrl()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: "Desc",
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: 60,
            ContentType: "Article",
            SourceName: "MIT OCW",
            Url: "https://ocw.mit.edu/");

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("MIT OCW", result.SourceName);
        Assert.Equal("https://ocw.mit.edu/", result.Url);
        Assert.Equal(ResourceAccessType.ExternalUrl.ToString(), result.AccessType);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenUrlIsUnsafe()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

        var request = new CreateLearningResourceRequest(
            Title: "Title",
            Description: null,
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: 60,
            ContentType: "Article",
            Url: "file:///etc/passwd");

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenContentTypeInvalid()
    {
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>());
        var service = CreateService(repo);

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
        var service = CreateService(repo);

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
        var service = CreateService(repo);

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
        var service = CreateService(repo);

        await service.DeleteAsync(existingId, CancellationToken.None);

        Assert.Equal(1, repo.RemoveCalls);
        Assert.Equal(1, repo.SaveCalls);
        Assert.False(repo.Resources.ContainsKey(existingId));
    }

    [Fact]
    public async Task GetAccessibleForUserAsync_IncludesExtractedTextSummaryAndHasSupplementaryFile()
    {
        var resourceId = Guid.NewGuid();
        var resource = new LearningResource
        {
            Id = resourceId,
            Title = "Course notes",
            Topic = "Databases",
            Difficulty = 2,
            EstimatedDurationMinutes = 30,
            ContentType = ResourceContentType.Article,
            Description = "Intro"
        };
        var repo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource> { [resourceId] = resource });
        var extractedRepo = new FakeResourceExtractedTextRepository(
            new Dictionary<Guid, string> { [resourceId] = "Normalized summary for ranking." });
        var fileRepo = new FakeResourceFileRepository(new HashSet<Guid> { resourceId });
        var service = CreateService(repo, extractedRepo, fileRepo);

        var result = await service.GetAccessibleForUserAsync("user-1", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Normalized summary for ranking.", result[0].ExtractedTextSummary);
        Assert.True(result[0].HasSupplementaryFile);
    }

    private static LearningResourceService CreateService(
        FakeLearningResourceRepository repo,
        FakeResourceExtractedTextRepository? extractedRepo = null,
        FakeResourceFileRepository? fileRepo = null)
    {
        var userRepo = new FakeUserProfileRepository(profile: new UserProfile { UserId = "user-1" });
        var accessRepo = new PermissiveResourceAccessRepository(repo);
        return new LearningResourceService(
            repo,
            accessRepo,
            userRepo,
            extractedRepo ?? new FakeResourceExtractedTextRepository(),
            fileRepo ?? new FakeResourceFileRepository());
    }

    private sealed class FakeResourceExtractedTextRepository : IResourceExtractedTextRepository
    {
        private readonly IReadOnlyDictionary<Guid, string> _summaries;

        public FakeResourceExtractedTextRepository(IReadOnlyDictionary<Guid, string>? summaries = null) =>
            _summaries = summaries ?? new Dictionary<Guid, string>();

        public Task<ResourceExtractedText?> GetLatestByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default)
        {
            if (!_summaries.TryGetValue(learningResourceId, out var summary))
                return Task.FromResult<ResourceExtractedText?>(null);
            return Task.FromResult<ResourceExtractedText?>(new ResourceExtractedText
            {
                LearningResourceId = learningResourceId,
                ResourceFileId = Guid.NewGuid(),
                Summary = summary,
                ExtractedText = summary,
                CharacterCount = summary.Length,
                ExtractionMethod = ResourceTextExtractionMethod.PlainText
            });
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetLatestSummariesByResourceIdsAsync(
            IEnumerable<Guid> learningResourceIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(_summaries);

        public Task AddAsync(ResourceExtractedText entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveByResourceFileIdAsync(Guid resourceFileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeResourceFileRepository : IResourceFileRepository
    {
        private readonly IReadOnlySet<Guid> _withFiles;

        public FakeResourceFileRepository(IReadOnlySet<Guid>? withFiles = null) =>
            _withFiles = withFiles ?? new HashSet<Guid>();

        public Task<ResourceFile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ResourceFile?>(null);

        public Task<IReadOnlyList<ResourceFile>> GetByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ResourceFile>>(Array.Empty<ResourceFile>());

        public Task<IReadOnlySet<Guid>> GetResourceIdsWithFilesAsync(IEnumerable<Guid> learningResourceIds, CancellationToken ct = default) =>
            Task.FromResult(_withFiles);

        public Task AddAsync(ResourceFile entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(ResourceFile entity) { }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUserProfileRepository : IUserProfileRepository
    {
        private readonly UserProfile? _profile;
        public FakeUserProfileRepository(UserProfile? profile) => _profile = profile;
        public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_profile);
        public Task<UserProfile?> GetByUserIdWithPreferencesAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_profile);
        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<UserProfile?>(null);
        public Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserProfile>>(Array.Empty<UserProfile>());
        public Task AddAsync(UserProfile entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(UserProfile entity) { }
        public void Remove(UserProfile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public IQueryable<UserProfile> Query() => Array.Empty<UserProfile>().AsQueryable();
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

