using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IResourceExtractedTextRepository
{
    Task<ResourceExtractedText?> GetLatestByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, string>> GetLatestSummariesByResourceIdsAsync(
        IEnumerable<Guid> learningResourceIds,
        CancellationToken ct = default);
    Task AddAsync(ResourceExtractedText entity, CancellationToken ct = default);
    Task RemoveByResourceFileIdAsync(Guid resourceFileId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
