using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Repositories;

public sealed class ResourceAccessRepositoryTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAccessibleResourcesForUserAsync_Global_visible_to_all_users()
    {
        await using var db = CreateDb();
        var globalId = Guid.NewGuid();
        db.LearningResources.Add(new LearningResource
        {
            Id = globalId,
            Title = "Global article",
            Topic = "DB",
            Difficulty = 1,
            EstimatedDurationMinutes = 10,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.Global
        });
        await db.SaveChangesAsync();

        var repo = new ResourceAccessRepository(db);
        var forAlex = await repo.GetAccessibleResourcesForUserAsync("demo-alex");
        var forBianca = await repo.GetAccessibleResourcesForUserAsync("demo-bianca");

        Assert.Contains(forAlex, r => r.Id == globalId);
        Assert.Contains(forBianca, r => r.Id == globalId);
    }

    [Fact]
    public async Task GetAccessibleResourcesForUserAsync_CourseOnly_requires_membership()
    {
        await using var db = CreateDb();
        const string scopeKey = "databases-demo-course";
        var courseOnlyId = Guid.NewGuid();

        db.LearningResources.Add(new LearningResource
        {
            Id = courseOnlyId,
            Title = "Course worksheet",
            Topic = "DB",
            Difficulty = 2,
            EstimatedDurationMinutes = 20,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.CourseOnly,
            AccessScopes = new List<ResourceAccessScope>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ScopeType = ResourceScopeType.Course,
                    ScopeKey = scopeKey,
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        });
        db.UserAccessScopeMemberships.Add(new UserAccessScopeMembership
        {
            Id = Guid.NewGuid(),
            UserId = "demo-alex",
            ScopeType = ResourceScopeType.Course,
            ScopeKey = scopeKey,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ResourceAccessRepository(db);
        var forAlex = await repo.GetAccessibleResourcesForUserAsync("demo-alex");
        var forBianca = await repo.GetAccessibleResourcesForUserAsync("demo-bianca");

        Assert.Contains(forAlex, r => r.Id == courseOnlyId);
        Assert.DoesNotContain(forBianca, r => r.Id == courseOnlyId);
    }

    [Fact]
    public async Task GetAccessibleResourcesForUserAsync_CourseOnly_without_scope_rows_is_not_accessible()
    {
        await using var db = CreateDb();
        var courseOnlyId = Guid.NewGuid();
        db.LearningResources.Add(new LearningResource
        {
            Id = courseOnlyId,
            Title = "Orphan course resource",
            Topic = "DB",
            Difficulty = 2,
            EstimatedDurationMinutes = 20,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.CourseOnly,
            AccessScopes = new List<ResourceAccessScope>()
        });
        db.UserAccessScopeMemberships.Add(new UserAccessScopeMembership
        {
            Id = Guid.NewGuid(),
            UserId = "demo-alex",
            ScopeType = ResourceScopeType.Course,
            ScopeKey = "databases-demo-course",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ResourceAccessRepository(db);
        var forAlex = await repo.GetAccessibleResourcesForUserAsync("demo-alex");

        Assert.DoesNotContain(forAlex, r => r.Id == courseOnlyId);
    }

    [Fact]
    public async Task GetAccessibleResourcesForUserAsync_PrivateToUser_requires_owner()
    {
        await using var db = CreateDb();
        var privateId = Guid.NewGuid();
        db.LearningResources.Add(new LearningResource
        {
            Id = privateId,
            Title = "Private notes",
            Topic = "DB",
            Difficulty = 1,
            EstimatedDurationMinutes = 5,
            ContentType = ResourceContentType.Article,
            Visibility = ResourceVisibility.PrivateToUser,
            OwnerUserId = "demo-alex"
        });
        await db.SaveChangesAsync();

        var repo = new ResourceAccessRepository(db);
        var forAlex = await repo.GetAccessibleResourcesForUserAsync("demo-alex");
        var forBianca = await repo.GetAccessibleResourcesForUserAsync("demo-bianca");

        Assert.Contains(forAlex, r => r.Id == privateId);
        Assert.DoesNotContain(forBianca, r => r.Id == privateId);
    }
}
