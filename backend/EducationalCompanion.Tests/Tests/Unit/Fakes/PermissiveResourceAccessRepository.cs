using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Tests.Tests.Unit.Fakes;

/// <summary>
/// Treats every catalog resource as accessible (for legacy unit tests that predate scope filtering).
/// </summary>
public sealed class PermissiveResourceAccessRepository : IResourceAccessRepository
{
    private readonly ILearningResourceRepository _learningResourceRepository;

    public PermissiveResourceAccessRepository(ILearningResourceRepository learningResourceRepository)
    {
        _learningResourceRepository = learningResourceRepository;
    }

    public Task<IReadOnlyList<LearningResource>> GetAccessibleResourcesForUserAsync(
        string userId,
        CancellationToken ct = default) =>
        _learningResourceRepository.GetAllAsync(ct);

    public async Task<bool> IsResourceAccessibleToUserAsync(
        string userId,
        Guid learningResourceId,
        CancellationToken ct = default) =>
        await _learningResourceRepository.GetByIdAsync(learningResourceId, ct) is not null;
}
