using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class LearningResourceService : ILearningResourceService
{
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

    public async Task<LearningResourceResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyList<LearningResourceResponse>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct)
    {
        var items = await _repo.SearchAsync(topic, difficulty, contentType, ct);
        return items.Select(Map).ToList();
    }

    public async Task<LearningResourceResponse> CreateAsync(CreateLearningResourceRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<ResourceContentType>(request.ContentType, true, out var ctEnum))
            throw new ArgumentException("Invalid ContentType. Use Article, Video, or Quiz.");

        var entity = new LearningResource
        {
            Title = request.Title,
            Description = request.Description,
            Topic = request.Topic,
            Difficulty = request.Difficulty,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            ContentType = ctEnum
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateLearningResourceRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null) return false;

        if (!Enum.TryParse<ResourceContentType>(request.ContentType, true, out var ctEnum))
            throw new ArgumentException("Invalid ContentType. Use Article, Video, or Quiz.");

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.Topic = request.Topic;
        existing.Difficulty = request.Difficulty;
        existing.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        existing.ContentType = ctEnum;

        _repo.Update(existing);
        await _repo.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null) return false;

        _repo.Remove(existing);
        await _repo.SaveChangesAsync(ct);
        return true;
    }

    private static LearningResourceResponse Map(LearningResource e) =>
        new(e.Id, e.Title, e.Description, e.Topic, e.Difficulty, e.EstimatedDurationMinutes, e.ContentType.ToString());
}
