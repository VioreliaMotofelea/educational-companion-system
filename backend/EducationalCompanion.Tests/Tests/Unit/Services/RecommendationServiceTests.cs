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
using EducationalCompanion.Tests.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(Recommendations: null!, ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_EmptyRecommendationsWithReplaceExisting_ClearsExisting()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(new List<CreateRecommendationItemRequest>(), ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);
        Assert.Equal(0, result.CreatedCount);
        Assert.True(result.ReplacedExisting);
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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

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

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var tooLongExplanation = new string('b', 1001); // Max is 1000
        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Algo", tooLongExplanation),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
    }

    [Fact]
    public async Task CreateBatchForUserAsync_PersistsOnlyAccessibleRecommendations()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "Global", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "Restricted", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, new[] { R1 });

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "accessible"),
                new CreateRecommendationItemRequest(R2, 0.8, "Hybrid", "inaccessible"),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, result.CreatedCount);
        Assert.True(result.ReplacedExisting);
        Assert.Single(recRepo.Stored);
        Assert.Equal(R1, recRepo.Stored[0].LearningResourceId);
        Assert.DoesNotContain(recRepo.Stored, r => r.LearningResourceId == R2);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_AppendMode_DiscardsInaccessibleAndPersistsAccessible()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "Resource A", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "Resource B", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, new[] { R1 });

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "accessible A"),
                new CreateRecommendationItemRequest(R2, 0.8, "Hybrid", "inaccessible B"),
            },
            ReplaceExisting: false);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.False(result.ReplacedExisting);
        Assert.Equal(1, result.CreatedCount);
        Assert.Contains(recRepo.Stored, r => r.LearningResourceId == R1);
        Assert.DoesNotContain(recRepo.Stored, r => r.LearningResourceId == R2);
        Assert.Equal(2, recRepo.Stored.Count);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ReplaceExistingWithOnlyInaccessible_ClearsRecommendations()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R2] = new LearningResource { Title = "Restricted", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, Array.Empty<Guid>());
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R2, 0.7, "Hybrid", "inaccessible"),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(0, result.CreatedCount);
        Assert.True(result.ReplacedExisting);
        Assert.Equal(1, recRepo.DeleteCalls);
        Assert.Empty(recRepo.Stored);
        Assert.DoesNotContain(recRepo.Stored, r => r.LearningResourceId == R2);
        Assert.Equal(0, studyTasks.EnsurePendingTasksCallCount);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_AppendMode_AllInaccessible_ThrowsAndDoesNotMutateStore()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R2] = new LearningResource { Title = "Restricted", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, Array.Empty<Guid>());
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R2, 0.7, "Hybrid", "inaccessible"),
            },
            ReplaceExisting: false);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));

        Assert.Equal(0, recRepo.DeleteCalls);
        Assert.Equal(0, recRepo.SaveCalls);
        Assert.Single(recRepo.Stored);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), recRepo.Stored[0].LearningResourceId);
        Assert.Equal(0, studyTasks.EnsurePendingTasksCallCount);
        Assert.DoesNotContain(recRepo.Stored, r => r.LearningResourceId == R2);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ReplaceMode_PartialBatch_PersistsOnlyAccessibleSubset()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "Resource A", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "Resource B", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository(seedUserId: UserId);
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, new[] { R1 });
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "accessible"),
                new CreateRecommendationItemRequest(R2, 0.8, "Hybrid", "inaccessible"),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, result.CreatedCount);
        Assert.True(result.ReplacedExisting);
        Assert.Equal(1, recRepo.DeleteCalls);
        Assert.Single(recRepo.Stored);
        Assert.Equal(R1, recRepo.Stored[0].LearningResourceId);
        Assert.DoesNotContain(recRepo.Stored, r => r.LearningResourceId == R2);
        Assert.Equal(1, studyTasks.EnsurePendingTasksCallCount);
        Assert.Single(studyTasks.LastRecommendationResourceIds);
        Assert.Equal(R1, studyTasks.LastRecommendationResourceIds[0]);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_DeduplicatesByResourceId_KeepsHighestScore()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.4, "Hybrid", "lower"),
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "higher"),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, result.CreatedCount);
        var stored = Assert.Single(recRepo.Stored);
        Assert.Equal(0.9, stored.Score);
        Assert.Equal("higher", stored.Explanation);
        Assert.Equal(1, studyTasks.EnsurePendingTasksCallCount);
        Assert.Single(studyTasks.LastRecommendationResourceIds);
        Assert.Equal(R1, studyTasks.LastRecommendationResourceIds[0]);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenRecommendationItemIsNull()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest?> { null }!,
            ReplaceExisting: true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(recRepo.Stored);
        Assert.Equal(0, recRepo.SaveCalls);
        Assert.Equal(0, studyTasks.EnsurePendingTasksCallCount);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_AcceptsAlgorithmUsed_WhenOnlyWhitespacePaddingExceedsLimitBeforeTrim()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var inner = new string('a', MaxAlgorithmUsedLength);
        var padded = $"  {inner}  ";

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, padded, "Explanation"),
            },
            ReplaceExisting: true);

        var result = await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(inner, Assert.Single(recRepo.Stored).AlgorithmUsed);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_TrimsExplanation_BeforeValidationAndStorage()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, "Hybrid", "  Good match  "),
            },
            ReplaceExisting: true);

        await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal("Good match", Assert.Single(recRepo.Stored).Explanation);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ThrowsWhenTrimmedAlgorithmUsedTooLong()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var padded = $"  {new string('a', MaxAlgorithmUsedLength + 1)}  ";

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.5, padded, "Explanation"),
            },
            ReplaceExisting: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBatchForUserAsync(UserId, request));
        Assert.Empty(recRepo.Stored);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_ChecksResourceExistenceOncePerDistinctId()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new CountingLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "t", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            new PermissiveResourceAccessRepository(resourceRepo),
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.4, "Hybrid", "first"),
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "second"),
            },
            ReplaceExisting: true);

        await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, resourceRepo.GetByIdCallCount);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_StudyTasksReceiveOnlyPersistedAccessibleResourceIds()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "Resource A", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "Resource B", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, new[] { R1 });
        var studyTasks = new RecordingStudyTaskService();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            studyTasks,
            accessRepo,
            NullLogger<RecommendationService>.Instance);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "accessible"),
                new CreateRecommendationItemRequest(R2, 0.85, "Hybrid", "inaccessible"),
            },
            ReplaceExisting: true);

        await service.CreateBatchForUserAsync(UserId, request);

        Assert.Equal(1, studyTasks.EnsurePendingTasksCallCount);
        Assert.Equal(UserId, studyTasks.LastRecommendationUserId);
        Assert.Single(studyTasks.LastRecommendationResourceIds);
        Assert.Equal(R1, studyTasks.LastRecommendationResourceIds[0]);
        Assert.DoesNotContain(R2, studyTasks.LastRecommendationResourceIds);
    }

    [Fact]
    public async Task CreateBatchForUserAsync_LogsDiscardedCountWithoutResourceTitles()
    {
        var userRepo = new FakeUserProfileRepository(hasUser: true);
        var resourceRepo = new FakeLearningResourceRepository(new Dictionary<Guid, LearningResource>
        {
            [R1] = new LearningResource { Title = "Visible Title A", Topic = "T", Difficulty = 1, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
            [R2] = new LearningResource { Title = "Secret Title B", Topic = "T", Difficulty = 2, EstimatedDurationMinutes = 10, ContentType = ResourceContentType.Article },
        });
        var recRepo = new FakeRecommendationRepository();
        var accessRepo = new FakeResourceAccessRepository(resourceRepo, new[] { R1 });
        var logger = new CaptureLogger<RecommendationService>();

        var service = new RecommendationService(
            userRepo,
            resourceRepo,
            recRepo,
            new NoOpStudyTaskService(),
            accessRepo,
            logger);

        var request = new CreateRecommendationsBatchRequest(
            new List<CreateRecommendationItemRequest>
            {
                new CreateRecommendationItemRequest(R1, 0.9, "Hybrid", "ok"),
                new CreateRecommendationItemRequest(R2, 0.8, "Hybrid", "blocked"),
            },
            ReplaceExisting: true);

        await service.CreateBatchForUserAsync(UserId, request);

        var infoLogs = logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        var debugLogs = logger.Entries.Where(e => e.Level == LogLevel.Debug).ToList();
        Assert.NotEmpty(infoLogs);
        Assert.Contains(infoLogs, e => e.Message.Contains("discarded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(infoLogs, e => e.Message.Contains("1", StringComparison.Ordinal));
        Assert.All(infoLogs, e => Assert.DoesNotContain(R2.ToString(), e.Message));
        Assert.All(infoLogs, e => Assert.DoesNotContain("Secret Title B", e.Message));
        Assert.All(infoLogs, e => Assert.DoesNotContain("Visible Title A", e.Message));
        Assert.Contains(debugLogs, e => e.Message.Contains(R2.ToString(), StringComparison.Ordinal));
    }

    private const int MaxAlgorithmUsedLength = 50;

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

    private sealed class CountingLearningResourceRepository : FakeLearningResourceRepository
    {
        public int GetByIdCallCount { get; private set; }

        public CountingLearningResourceRepository(Dictionary<Guid, LearningResource> resourcesById)
            : base(resourcesById)
        {
        }

        public override Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            GetByIdCallCount++;
            return base.GetByIdAsync(id, ct);
        }
    }

    private class FakeLearningResourceRepository : ILearningResourceRepository
    {
        private readonly Dictionary<Guid, LearningResource> _resourcesById;
        public FakeLearningResourceRepository(Dictionary<Guid, LearningResource> resourcesById)
        {
            _resourcesById = new Dictionary<Guid, LearningResource>();
            foreach (var (id, resource) in resourcesById)
            {
                resource.Id = id;
                _resourcesById[id] = resource;
            }
        }

        public Task<IReadOnlyList<LearningResource>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningResource>>(Array.Empty<LearningResource>());

        public virtual Task<LearningResource?> GetByIdAsync(Guid id, CancellationToken ct = default)
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

