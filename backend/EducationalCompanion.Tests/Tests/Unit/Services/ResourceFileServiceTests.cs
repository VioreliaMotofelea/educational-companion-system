using System.Text;
using EducationalCompanion.Api.Dtos.ResourceFiles;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using EducationalCompanion.Tests.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class ResourceFileServiceTests
{
    private static readonly Guid ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string UserId = "demo-alex";
    private const string BlockedUserId = "demo-bianca";

    [Fact]
    public async Task UploadAsync_RejectsUnsupportedExtension()
    {
        var service = CreateService(accessible: true);
        var file = CreateFormFile("malware.exe", "MZ", "application/octet-stream");

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(ResourceId, UserId, file, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_ThrowsForbiddenWhenUserLacksAccess()
    {
        var service = CreateService(accessible: false);
        var file = CreateFormFile("notes.txt", "hello", "text/plain");

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            service.UploadAsync(ResourceId, BlockedUserId, file, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_Txt_ExtractsAndPersistsMetadata()
    {
        var fileRepo = new InMemoryResourceFileRepository();
        var extractedRepo = new InMemoryResourceExtractedTextRepository();
        var storage = new InMemoryResourceFileStorage();
        var service = CreateService(
            accessible: true,
            fileRepo: fileRepo,
            extractedRepo: extractedRepo,
            storage: storage);

        var content = "Course notes about BCNF and normalization for databases.";
        var file = CreateFormFile("notes.txt", content, "text/plain");

        var response = await service.UploadAsync(ResourceId, UserId, file, CancellationToken.None);

        Assert.Equal("Completed", response.ProcessingStatus);
        Assert.Equal(1, fileRepo.Entities.Count);
        Assert.Equal(1, extractedRepo.Entities.Count);
        Assert.Contains("BCNF", extractedRepo.Entities[0].Summary!, StringComparison.Ordinal);
        Assert.True(storage.StoredKeys.Count > 0);
        Assert.DoesNotContain('/', fileRepo.Entities[0].StorageKey);
        Assert.DoesNotContain("..", fileRepo.Entities[0].StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsOnlyWhenAccessible()
    {
        var fileRepo = new InMemoryResourceFileRepository();
        fileRepo.Entities.Add(new ResourceFile
        {
            Id = Guid.NewGuid(),
            LearningResourceId = ResourceId,
            OriginalFileName = "notes.txt",
            StorageKey = $"{ResourceId:N}/abc.txt",
            MimeType = "text/plain",
            SizeBytes = 10,
            ProcessingStatus = ResourceFileProcessingStatus.Completed
        });

        var accessibleService = CreateService(accessible: true, fileRepo: fileRepo);
        var list = await accessibleService.ListFilesAsync(ResourceId, UserId, CancellationToken.None);
        Assert.Single(list);

        var blockedService = CreateService(accessible: false, fileRepo: fileRepo);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            blockedService.ListFilesAsync(ResourceId, BlockedUserId, CancellationToken.None));
    }

    private static ResourceFileService CreateService(
        bool accessible,
        InMemoryResourceFileRepository? fileRepo = null,
        InMemoryResourceExtractedTextRepository? extractedRepo = null,
        InMemoryResourceFileStorage? storage = null)
    {
        var resource = new LearningResource
        {
            Id = ResourceId,
            Title = "DB Notes",
            Topic = "Databases",
            Difficulty = 2,
            EstimatedDurationMinutes = 20,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.CourseOnly
        };
        var learningRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource> { [ResourceId] = resource });
        var accessRepo = accessible
            ? new FakeResourceAccessRepository(learningRepo, new[] { ResourceId })
            : new FakeResourceAccessRepository(learningRepo, Array.Empty<Guid>());

        return new ResourceFileService(
            learningRepo,
            accessRepo,
            fileRepo ?? new InMemoryResourceFileRepository(),
            extractedRepo ?? new InMemoryResourceExtractedTextRepository(),
            storage ?? new InMemoryResourceFileStorage(),
            new ResourceTextExtractionService(Options.Create(new ResourceFilesOptions())),
            Options.Create(new ResourceFilesOptions()),
            NullLogger<ResourceFileService>.Instance);
    }

    private static IFormFile CreateFormFile(string fileName, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class InMemoryResourceFileRepository : IResourceFileRepository
    {
        public List<ResourceFile> Entities { get; } = new();

        public Task<ResourceFile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Entities.FirstOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<ResourceFile>> GetByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ResourceFile>>(Entities.Where(e => e.LearningResourceId == learningResourceId).ToList());

        public Task<IReadOnlySet<Guid>> GetResourceIdsWithFilesAsync(IEnumerable<Guid> learningResourceIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(Entities.Select(e => e.LearningResourceId).ToHashSet());

        public async Task AddAsync(ResourceFile entity, CancellationToken ct = default)
        {
            Entities.Add(entity);
            await Task.CompletedTask;
        }

        public void Remove(ResourceFile entity) => Entities.Remove(entity);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryResourceExtractedTextRepository : IResourceExtractedTextRepository
    {
        public List<ResourceExtractedText> Entities { get; } = new();

        public Task<ResourceExtractedText?> GetLatestByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default) =>
            Task.FromResult(Entities.Where(e => e.LearningResourceId == learningResourceId).OrderByDescending(e => e.CreatedAtUtc).FirstOrDefault());

        public Task<IReadOnlyDictionary<Guid, string>> GetLatestSummariesByResourceIdsAsync(
            IEnumerable<Guid> learningResourceIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public async Task AddAsync(ResourceExtractedText entity, CancellationToken ct = default)
        {
            Entities.Add(entity);
            await Task.CompletedTask;
        }

        public Task RemoveByResourceFileIdAsync(Guid resourceFileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLearningResourceRepository : ILearningResourceRepository
    {
        private readonly Dictionary<Guid, LearningResource> _resources;

        public FakeLearningResourceRepository(Dictionary<Guid, LearningResource> resources) => _resources = resources;

        public Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _resources.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }

        public Task<IReadOnlyList<LearningResource>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(_resources.Values.ToList());

        public Task<IReadOnlyList<LearningResource>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(_resources.Values.ToList());

        public IQueryable<LearningResource> Query() => _resources.Values.AsQueryable();
        public Task AddAsync(LearningResource entity, CancellationToken ct = default) { _resources[entity.Id] = entity; return Task.CompletedTask; }
        public void Update(LearningResource entity) => _resources[entity.Id] = entity;
        public void Remove(LearningResource entity) => _resources.Remove(entity.Id);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class InMemoryResourceFileStorage : IResourceFileStorageService
    {
        public HashSet<string> StoredKeys { get; } = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public async Task<string> SaveAsync(Stream content, string storageKey, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            _files[storageKey] = ms.ToArray();
            StoredKeys.Add(storageKey);
            return storageKey;
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
        {
            if (!_files.TryGetValue(storageKey, out var bytes))
                throw new NotFoundException("StoredResourceFile", storageKey);
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            _files.Remove(storageKey);
            StoredKeys.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
