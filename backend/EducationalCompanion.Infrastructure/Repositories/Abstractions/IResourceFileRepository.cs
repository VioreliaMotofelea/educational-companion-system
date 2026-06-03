using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IResourceFileRepository
{
    Task<ResourceFile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ResourceFile>> GetByLearningResourceIdAsync(Guid learningResourceId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetResourceIdsWithFilesAsync(IEnumerable<Guid> learningResourceIds, CancellationToken ct = default);
    Task AddAsync(ResourceFile entity, CancellationToken ct = default);
    void Remove(ResourceFile entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
