using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class RecommendationService : IRecommendationService
{
    private const double MinScore = 0.0;
    private const double MaxScore = 1.0;
    private const int MaxAlgorithmUsedLength = 50;
    private const int MaxExplanationLength = 1000;
    private const double AutoTaskMinScore = 0.65;
    private const int AutoTaskFallbackCount = 2;

    private readonly IUserProfileRepository _userProfileRepo;
    private readonly ILearningResourceRepository _learningResourceRepo;
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly IStudyTaskService _studyTaskService;

    public RecommendationService(
        IUserProfileRepository userProfileRepo,
        ILearningResourceRepository learningResourceRepo,
        IRecommendationRepository recommendationRepo,
        IStudyTaskService studyTaskService)
    {
        _userProfileRepo = userProfileRepo;
        _learningResourceRepo = learningResourceRepo;
        _recommendationRepo = recommendationRepo;
        _studyTaskService = studyTaskService;
    }

    public async Task<CreatedRecommendationsResponse> CreateBatchForUserAsync(
        string userId,
        CreateRecommendationsBatchRequest request,
        CancellationToken ct = default)
    {
        if (request.Recommendations is null || request.Recommendations.Count == 0)
            throw new ValidationException("At least one recommendation is required.");

        await EnsureUserExistsAsync(userId, ct);

        foreach (var item in request.Recommendations)
        {
            ValidateItem(item);
            await EnsureLearningResourceExistsAsync(item.LearningResourceId, ct);
        }

        var replaced = false;
        if (request.ReplaceExisting)
        {
            await _recommendationRepo.DeleteByUserIdAsync(userId, ct);
            replaced = true;
        }

        var entities = request.Recommendations
            .Select(item => new Recommendation
            {
                UserId = userId,
                LearningResourceId = item.LearningResourceId,
                Score = item.Score,
                AlgorithmUsed = item.AlgorithmUsed.Trim(),
                Explanation = item.Explanation.Trim()
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

        return new CreatedRecommendationsResponse(userId, entities.Count, replaced);
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

    private static void ValidateItem(CreateRecommendationItemRequest item)
    {
        if (item.Score < MinScore || item.Score > MaxScore)
            throw new ValidationException($"Score must be between {MinScore} and {MaxScore}.");

        if (string.IsNullOrWhiteSpace(item.AlgorithmUsed))
            throw new ValidationException("AlgorithmUsed is required.");
        if (item.AlgorithmUsed.Length > MaxAlgorithmUsedLength)
            throw new ValidationException($"AlgorithmUsed must be at most {MaxAlgorithmUsedLength} characters.");

        if (string.IsNullOrWhiteSpace(item.Explanation))
            throw new ValidationException("Explanation is required.");
        if (item.Explanation.Length > MaxExplanationLength)
            throw new ValidationException($"Explanation must be at most {MaxExplanationLength} characters.");
    }
}
