using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Infrastructure.Access;

public static class ResourceAccessEvaluator
{
    public static bool IsAccessible(
        LearningResource resource,
        string userId,
        IReadOnlySet<string> userCourseScopeKeys,
        IReadOnlySet<string> userGroupScopeKeys)
    {
        return resource.Visibility switch
        {
            ResourceVisibility.Global => true,
            ResourceVisibility.PrivateToUser => IsOwnedByUser(resource, userId),
            ResourceVisibility.CourseOnly => HasMatchingScope(
                resource,
                ResourceScopeType.Course,
                userCourseScopeKeys),
            ResourceVisibility.GroupOnly => HasMatchingScope(
                resource,
                ResourceScopeType.Group,
                userGroupScopeKeys),
            _ => false
        };
    }

    private static bool IsOwnedByUser(LearningResource resource, string userId) =>
        !string.IsNullOrWhiteSpace(resource.OwnerUserId)
        && string.Equals(resource.OwnerUserId.Trim(), userId, StringComparison.Ordinal);

    private static bool HasMatchingScope(
        LearningResource resource,
        ResourceScopeType scopeType,
        IReadOnlySet<string> userScopeKeys)
    {
        var resourceScopes = resource.AccessScopes
            .Where(s => s.ScopeType == scopeType)
            .Select(s => s.ScopeKey)
            .ToList();

        if (resourceScopes.Count == 0)
            return false;

        if (userScopeKeys.Count == 0)
            return false;

        return resourceScopes.Any(key =>
            userScopeKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
