using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Tests.Tests.Unit.Fakes;

/// <summary>
/// Returns only resources whose ids are in the configured accessible set (for access-filtering unit tests).
/// </summary>
public sealed class FakeResourceAccessRepository : IResourceAccessRepository
{
    private readonly ILearningResourceRepository _learningResourceRepository;
    private readonly HashSet<Guid> _accessibleIds;

    public FakeResourceAccessRepository(
        ILearningResourceRepository learningResourceRepository,
        IEnumerable<Guid> accessibleIds)
    {
        _learningResourceRepository = learningResourceRepository;
        _accessibleIds = accessibleIds.ToHashSet();
    }

    public async Task<IReadOnlyList<LearningResource>> GetAccessibleResourcesForUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var all = await _learningResourceRepository.GetAllAsync(ct);
        return all.Where(r => _accessibleIds.Contains(r.Id)).ToList();
    }

    public Task<bool> IsResourceAccessibleToUserAsync(
        string userId,
        Guid learningResourceId,
        CancellationToken ct = default) =>
        Task.FromResult(_accessibleIds.Contains(learningResourceId));
}
