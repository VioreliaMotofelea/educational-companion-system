using EducationalCompanion.Infrastructure.Access;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class ResourceAccessRepository : IResourceAccessRepository
{
    private readonly ApplicationDbContext _db;

    public ResourceAccessRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LearningResource>> GetAccessibleResourcesForUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var (courseKeys, groupKeys) = await LoadUserScopeKeysAsync(userId, ct);

        var resources = await _db.LearningResources
            .AsNoTracking()
            .Include(r => r.AccessScopes)
            .ToListAsync(ct);

        return resources
            .Where(r => ResourceAccessEvaluator.IsAccessible(r, userId, courseKeys, groupKeys))
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();
    }

    public async Task<bool> IsResourceAccessibleToUserAsync(
        string userId,
        Guid learningResourceId,
        CancellationToken ct = default)
    {
        var (courseKeys, groupKeys) = await LoadUserScopeKeysAsync(userId, ct);

        var resource = await _db.LearningResources
            .AsNoTracking()
            .Include(r => r.AccessScopes)
            .FirstOrDefaultAsync(r => r.Id == learningResourceId, ct);

        if (resource is null)
            return false;

        return ResourceAccessEvaluator.IsAccessible(resource, userId, courseKeys, groupKeys);
    }

    private async Task<(HashSet<string> CourseKeys, HashSet<string> GroupKeys)> LoadUserScopeKeysAsync(
        string userId,
        CancellationToken ct)
    {
        var memberships = await _db.UserAccessScopeMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        var courseKeys = memberships
            .Where(m => m.ScopeType == ResourceScopeType.Course)
            .Select(m => m.ScopeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groupKeys = memberships
            .Where(m => m.ScopeType == ResourceScopeType.Group)
            .Select(m => m.ScopeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (courseKeys, groupKeys);
    }
}
