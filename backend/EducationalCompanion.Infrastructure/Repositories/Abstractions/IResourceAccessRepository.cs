using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IResourceAccessRepository
{
    Task<IReadOnlyList<LearningResource>> GetAccessibleResourcesForUserAsync(string userId, CancellationToken ct = default);
    Task<bool> IsResourceAccessibleToUserAsync(string userId, Guid learningResourceId, CancellationToken ct = default);
}
