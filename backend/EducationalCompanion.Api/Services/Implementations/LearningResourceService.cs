using EducationalCompanion.Api.Dtos.LearningResources;
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

    public LearningResourceService(ILearningResourceRepository repo)
    {
        _repo = repo;
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

        var entity = new LearningResource
        {
            Title = request.Title,
            Description = request.Description,
            Topic = request.Topic,
            Difficulty = request.Difficulty,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            ContentType = contentType
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

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.Topic = request.Topic;
        existing.Difficulty = request.Difficulty;
        existing.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        existing.ContentType = contentType;

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

    private static LearningResourceResponse Map(LearningResource e) =>
        new(e.Id, e.Title, e.Description, e.Topic, e.Difficulty, e.EstimatedDurationMinutes, e.ContentType.ToString());
}
