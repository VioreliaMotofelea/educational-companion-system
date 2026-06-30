using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Access;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public sealed class ResourceAccessEvaluatorTests
{
    private const string UserA = "demo-alex";
    private const string UserB = "demo-bianca";

    [Fact]
    public void Global_resource_is_accessible_to_every_user()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.Global
        };

        Assert.True(ResourceAccessEvaluator.IsAccessible(resource, UserB, EmptyKeys(), EmptyKeys()));
    }

    [Fact]
    public void CourseOnly_accessible_with_matching_membership()
    {
        var resource = CourseResource("databases-demo-course");
        var courseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "databases-demo-course" };

        Assert.True(ResourceAccessEvaluator.IsAccessible(resource, UserA, courseKeys, EmptyKeys()));
    }

    [Fact]
    public void CourseOnly_not_accessible_without_membership()
    {
        var resource = CourseResource("databases-demo-course");
        Assert.False(ResourceAccessEvaluator.IsAccessible(resource, UserB, EmptyKeys(), EmptyKeys()));
    }

    [Fact]
    public void CourseOnly_without_scope_mapping_is_not_accessible()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "Databases",
            Difficulty = 2,
            EstimatedDurationMinutes = 30,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.CourseOnly,
            AccessScopes = []
        };
        var courseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "databases-demo-course" };

        Assert.False(ResourceAccessEvaluator.IsAccessible(resource, UserA, courseKeys, EmptyKeys()));
    }

    [Fact]
    public void GroupOnly_accessible_with_matching_membership()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.GroupOnly,
            AccessScopes =
            [
                new ResourceAccessScope { ScopeType = ResourceScopeType.Group, ScopeKey = "group-a" }
            ]
        };
        var groupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "group-a" };

        Assert.True(ResourceAccessEvaluator.IsAccessible(resource, UserA, EmptyKeys(), groupKeys));
    }

    [Fact]
    public void GroupOnly_not_accessible_without_membership()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.GroupOnly,
            AccessScopes =
            [
                new ResourceAccessScope { ScopeType = ResourceScopeType.Group, ScopeKey = "group-a" }
            ]
        };

        Assert.False(ResourceAccessEvaluator.IsAccessible(resource, UserB, EmptyKeys(), EmptyKeys()));
    }

    [Fact]
    public void PrivateToUser_accessible_only_to_owner()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.PrivateToUser,
            OwnerUserId = UserA
        };

        Assert.True(ResourceAccessEvaluator.IsAccessible(resource, UserA, EmptyKeys(), EmptyKeys()));
        Assert.False(ResourceAccessEvaluator.IsAccessible(resource, UserB, EmptyKeys(), EmptyKeys()));
    }

    [Fact]
    public void PrivateToUser_without_owner_is_not_accessible()
    {
        var resource = new LearningResource
        {
            Title = "t",
            Topic = "T",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.PrivateToUser,
            OwnerUserId = null
        };

        Assert.False(ResourceAccessEvaluator.IsAccessible(resource, UserA, EmptyKeys(), EmptyKeys()));
    }

    private static LearningResource CourseResource(string scopeKey) =>
        new()
        {
            Title = "t",
            Topic = "Databases",
            Difficulty = 2,
            EstimatedDurationMinutes = 30,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.CourseOnly,
            AccessScopes =
            [
                new ResourceAccessScope { ScopeType = ResourceScopeType.Course, ScopeKey = scopeKey }
            ]
        };

    private static HashSet<string> EmptyKeys() =>
        new(StringComparer.OrdinalIgnoreCase);
}
