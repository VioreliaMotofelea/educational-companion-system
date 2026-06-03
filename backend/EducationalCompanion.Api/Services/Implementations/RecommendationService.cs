using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EducationalCompanion.Api.Services.Implementations;

public class RecommendationService : IRecommendationService
{
    private const double MinScore = 0.0;
    private const double MaxScore = 1.0;
    private const int MaxAlgorithmUsedLength = 50;
    private const int MaxExplanationLength = 1000;
    private const double AutoTaskMinScore = 0.65;
    private const int AutoTaskFallbackCount = 2;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserRecommendationLocks = new();

    private readonly IUserProfileRepository _userProfileRepo;
    private readonly ILearningResourceRepository _learningResourceRepo;
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly IStudyTaskService _studyTaskService;
    private readonly IResourceAccessRepository _accessRepo;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IUserProfileRepository userProfileRepo,
        ILearningResourceRepository learningResourceRepo,
        IRecommendationRepository recommendationRepo,
        IStudyTaskService studyTaskService,
        IResourceAccessRepository accessRepo,
        ILogger<RecommendationService> logger)
    {
        _userProfileRepo = userProfileRepo;
        _learningResourceRepo = learningResourceRepo;
        _recommendationRepo = recommendationRepo;
        _studyTaskService = studyTaskService;
        _accessRepo = accessRepo;
        _logger = logger;
    }

    public async Task<CreatedRecommendationsResponse> CreateBatchForUserAsync(
        string userId,
        CreateRecommendationsBatchRequest request,
        CancellationToken ct = default)
    {
        var userLock = UserRecommendationLocks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);
        try
        {
            if (request.Recommendations is null)
                throw new ValidationException("Recommendations array is required.");

            await EnsureUserExistsAsync(userId, ct);

            if (request.Recommendations.Count == 0)
            {
                if (!request.ReplaceExisting)
                    throw new ValidationException("At least one recommendation is required.");

                await _recommendationRepo.DeleteByUserIdAsync(userId, ct);
                await _recommendationRepo.SaveChangesAsync(ct);
                return new CreatedRecommendationsResponse(userId, 0, ReplacedExisting: true);
            }

            var normalizedItems = new List<NormalizedRecommendationItem>(request.Recommendations.Count);
            foreach (var item in request.Recommendations)
                normalizedItems.Add(ValidateAndNormalizeItem(item));

            foreach (var resourceId in normalizedItems.Select(i => i.LearningResourceId).Distinct())
                await EnsureLearningResourceExistsAsync(resourceId, ct);

            var accessibleIds = (await _accessRepo.GetAccessibleResourcesForUserAsync(userId, ct))
                .Select(r => r.Id)
                .ToHashSet();

            var accessibleRecommendations = normalizedItems
                .Where(item => accessibleIds.Contains(item.LearningResourceId))
                .ToList();

            LogDiscardedInaccessibleItems(userId, request, normalizedItems, accessibleRecommendations);

            if (accessibleRecommendations.Count == 0)
            {
                if (!request.ReplaceExisting)
                    throw new ValidationException("No accessible resources in recommendation batch.");

                await _recommendationRepo.DeleteByUserIdAsync(userId, ct);
                await _recommendationRepo.SaveChangesAsync(ct);
                return new CreatedRecommendationsResponse(userId, 0, ReplacedExisting: true);
            }

            var replaced = false;
            if (request.ReplaceExisting)
            {
                await _recommendationRepo.DeleteByUserIdAsync(userId, ct);
                replaced = true;
            }

            var dedupedRecommendations = accessibleRecommendations
                .OrderByDescending(item => item.Score)
                .DistinctBy(item => item.LearningResourceId)
                .ToList();

            var entities = dedupedRecommendations
                .Select(item => new Recommendation
                {
                    UserId = userId,
                    LearningResourceId = item.LearningResourceId,
                    Score = item.Score,
                    AlgorithmUsed = item.AlgorithmUsed,
                    Explanation = item.Explanation
                })
                .ToList();

            foreach (var entity in entities)
                await _recommendationRepo.AddAsync(entity, ct);

            await _recommendationRepo.SaveChangesAsync(ct);
            var taskCandidateIds = entities
                .Where(e => e.Score >= AutoTaskMinScore)
                .OrderByDescending(e => e.Score)
                .Select(e => e.LearningResourceId)
                .ToList();

            if (taskCandidateIds.Count == 0)
            {
                taskCandidateIds = entities
                    .OrderByDescending(e => e.Score)
                    .Take(AutoTaskFallbackCount)
                    .Select(e => e.LearningResourceId)
                    .ToList();
            }

            await _studyTaskService.EnsurePendingTasksForRecommendationsAsync(
                userId,
                taskCandidateIds,
                ct);

            return new CreatedRecommendationsResponse(userId, entities.Count, ReplacedExisting: replaced);
        }
        finally
        {
            userLock.Release();
        }
    }

    private void LogDiscardedInaccessibleItems(
        string userId,
        CreateRecommendationsBatchRequest request,
        IReadOnlyList<NormalizedRecommendationItem> normalizedItems,
        List<NormalizedRecommendationItem> accessibleRecommendations)
    {
        var accessibleIdSet = accessibleRecommendations
            .Select(item => item.LearningResourceId)
            .ToHashSet();

        var discardedResourceIds = normalizedItems
            .Where(item => !accessibleIdSet.Contains(item.LearningResourceId))
            .Select(item => item.LearningResourceId)
            .ToList();

        if (discardedResourceIds.Count == 0)
            return;

        _logger.LogInformation(
            "Recommendation batch for user {UserId}: discarded {DiscardedCount} inaccessible item(s); "
            + "persisting {PersistedCount} of {RequestedCount}. ReplaceExisting={ReplaceExisting}.",
            userId,
            discardedResourceIds.Count,
            accessibleRecommendations.Count,
            normalizedItems.Count,
            request.ReplaceExisting);

        _logger.LogDebug(
            "Discarded learning resource ids for user {UserId}: {DiscardedResourceIds}",
            userId,
            discardedResourceIds);
    }

    private async Task EnsureUserExistsAsync(string userId, CancellationToken ct)
    {
        var profile = await _userProfileRepo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);
    }

    private async Task EnsureLearningResourceExistsAsync(Guid learningResourceId, CancellationToken ct)
    {
        var resource = await _learningResourceRepo.GetByIdAsync(learningResourceId, ct);
        if (resource is null)
            throw new LearningResourceNotFoundException(learningResourceId);
    }

    private static NormalizedRecommendationItem ValidateAndNormalizeItem(CreateRecommendationItemRequest item)
    {
        if (item is null)
            throw new ValidationException("Recommendation item cannot be null.");

        if (item.Score < MinScore || item.Score > MaxScore)
            throw new ValidationException($"Score must be between {MinScore} and {MaxScore}.");

        if (string.IsNullOrWhiteSpace(item.AlgorithmUsed))
            throw new ValidationException("AlgorithmUsed is required.");

        var algorithmUsed = item.AlgorithmUsed.Trim();
        if (algorithmUsed.Length > MaxAlgorithmUsedLength)
            throw new ValidationException($"AlgorithmUsed must be at most {MaxAlgorithmUsedLength} characters.");

        if (string.IsNullOrWhiteSpace(item.Explanation))
            throw new ValidationException("Explanation is required.");

        var explanation = item.Explanation.Trim();
        if (explanation.Length > MaxExplanationLength)
            throw new ValidationException($"Explanation must be at most {MaxExplanationLength} characters.");

        return new NormalizedRecommendationItem(
            item.LearningResourceId,
            item.Score,
            algorithmUsed,
            explanation);
    }

    private sealed record NormalizedRecommendationItem(
        Guid LearningResourceId,
        double Score,
        string AlgorithmUsed,
        string Explanation);
}
