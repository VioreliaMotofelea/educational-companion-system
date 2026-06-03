using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Services;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class LearningResourceService : ILearningResourceService
{
    private const int MinDifficulty = 1;
    private const int MaxDifficulty = 5;

    private readonly ILearningResourceRepository _repo;
    private readonly IResourceAccessRepository _accessRepo;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly IResourceExtractedTextRepository _extractedTextRepo;
    private readonly IResourceFileRepository _resourceFileRepo;

    public LearningResourceService(
        ILearningResourceRepository repo,
        IResourceAccessRepository accessRepo,
        IUserProfileRepository userProfileRepo,
        IResourceExtractedTextRepository extractedTextRepo,
        IResourceFileRepository resourceFileRepo)
    {
        _repo = repo;
        _accessRepo = accessRepo;
        _userProfileRepo = userProfileRepo;
        _extractedTextRepo = extractedTextRepo;
        _resourceFileRepo = resourceFileRepo;
    }

    public async Task<IReadOnlyList<LearningResourceResponse>> GetAllAsync(CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<LearningResourceResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        if (item is null)
            throw new LearningResourceNotFoundException(id);
        return Map(item);
    }

    public async Task<IReadOnlyList<LearningResourceResponse>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct)
    {
        var items = await _repo.SearchAsync(topic, difficulty, contentType, ct);
        return items.Select(Map).ToList();
    }

    public async Task<LearningResourceResponse> CreateAsync(CreateLearningResourceRequest request, CancellationToken ct)
    {
        var contentType = ParseContentType(request.ContentType);
        ValidateDifficulty(request.Difficulty);
        ValidateEstimatedDuration(request.EstimatedDurationMinutes);
        var access = LearningResourceAccessNormalizer.Normalize(
            request.SourceName,
            request.Url,
            request.AccessType,
            request.AccessInstructions,
            request.Visibility);

        var entity = new LearningResource
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Topic = request.Topic.Trim(),
            Difficulty = request.Difficulty,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            ContentType = contentType,
            SourceName = access.SourceName,
            Url = access.Url,
            AccessType = access.AccessType,
            AccessInstructions = access.AccessInstructions,
            Visibility = access.Visibility
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task UpdateAsync(Guid id, UpdateLearningResourceRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null)
            throw new LearningResourceNotFoundException(id);

        var contentType = ParseContentType(request.ContentType);
        ValidateDifficulty(request.Difficulty);
        ValidateEstimatedDuration(request.EstimatedDurationMinutes);
        var access = LearningResourceAccessNormalizer.Normalize(
            request.SourceName,
            request.Url,
            request.AccessType,
            request.AccessInstructions,
            request.Visibility);

        existing.Title = request.Title.Trim();
        existing.Description = request.Description;
        existing.Topic = request.Topic.Trim();
        existing.Difficulty = request.Difficulty;
        existing.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        existing.ContentType = contentType;
        existing.SourceName = access.SourceName;
        existing.Url = access.Url;
        existing.AccessType = access.AccessType;
        existing.AccessInstructions = access.AccessInstructions;
        existing.Visibility = access.Visibility;

        _repo.Update(existing);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null)
            throw new LearningResourceNotFoundException(id);

        _repo.Remove(existing);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccessibleLearningResourceResponse>> GetAccessibleForUserAsync(string userId, CancellationToken ct)
    {
        await EnsureUserExistsAsync(userId, ct);
        var items = await _accessRepo.GetAccessibleResourcesForUserAsync(userId, ct);
        var ids = items.Select(i => i.Id).ToList();
        var summaries = await _extractedTextRepo.GetLatestSummariesByResourceIdsAsync(ids, ct);
        var withFiles = await _resourceFileRepo.GetResourceIdsWithFilesAsync(ids, ct);

        return items
            .Select(e => MapAccessible(e, summaries, withFiles))
            .ToList();
    }

    private async Task EnsureUserExistsAsync(string userId, CancellationToken ct)
    {
        var profile = await _userProfileRepo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);
    }

    private static ResourceContentType ParseContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !Enum.TryParse<ResourceContentType>(contentType, true, out var parsed))
            throw new InvalidContentTypeException(contentType ?? "(null)");
        return parsed;
    }

    private static void ValidateDifficulty(int difficulty)
    {
        if (difficulty < MinDifficulty || difficulty > MaxDifficulty)
            throw new InvalidDifficultyException(difficulty);
    }

    private static void ValidateEstimatedDuration(int minutes)
    {
        if (minutes <= 0)
            throw new InvalidEstimatedDurationException(minutes);
    }

    public static LearningResourceResponse Map(LearningResource e) =>
        new(
            e.Id,
            e.Title,
            e.Description,
            e.Topic,
            e.Difficulty,
            e.EstimatedDurationMinutes,
            e.ContentType.ToString(),
            e.SourceName,
            e.Url,
            e.AccessType.ToString(),
            e.AccessInstructions,
            e.Visibility.ToString()
        );

    private static AccessibleLearningResourceResponse MapAccessible(
        LearningResource e,
        IReadOnlyDictionary<Guid, string> summaries,
        IReadOnlySet<Guid> withFiles)
    {
        summaries.TryGetValue(e.Id, out var summary);
        return new AccessibleLearningResourceResponse(
            e.Id,
            e.Title,
            e.Description,
            e.Topic,
            e.Difficulty,
            e.EstimatedDurationMinutes,
            e.ContentType.ToString(),
            e.SourceName,
            e.Url,
            e.AccessType.ToString(),
            e.AccessInstructions,
            e.Visibility.ToString(),
            summary,
            withFiles.Contains(e.Id));
    }
}
