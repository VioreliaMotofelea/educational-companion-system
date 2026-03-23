using System;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

public class RecommendationRepositoryIntegrationTests
{
    [Fact]
    public async Task GetByUserIdWithResourceAsync_IncludesAndOrdersAndRespectsLimit()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var resA = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "A",
                Description = "d",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };

            var resB = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "B",
                Description = "d",
                Topic = "Python",
                Difficulty = 3,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };

            var resC = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "C",
                Description = "d",
                Topic = "Python",
                Difficulty = 4,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };

            context.LearningResources.AddRange(resA, resB, resC);
            await context.SaveChangesAsync();

            var userId = "user-1";
            var rec1 = new Recommendation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LearningResourceId = resA.Id,
                LearningResource = resA,
                Score = 0.9,
                AlgorithmUsed = "Algo",
                Explanation = "Exp",
                CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            var rec2 = new Recommendation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LearningResourceId = resB.Id,
                LearningResource = resB,
                Score = 0.9,
                AlgorithmUsed = "Algo",
                Explanation = "Exp",
                CreatedAtUtc = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc) // same score, newer => should come first
            };

            var rec3 = new Recommendation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LearningResourceId = resC.Id,
                LearningResource = resC,
                Score = 0.8,
                AlgorithmUsed = "Algo",
                Explanation = "Exp",
                CreatedAtUtc = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            };

            context.Recommendations.AddRange(rec1, rec2, rec3);
            await context.SaveChangesAsync();

            IRecommendationRepository repo = new RecommendationRepository(context);

            var ordered = await repo.GetByUserIdWithResourceAsync(userId, limit: null, CancellationToken.None);
            Assert.Equal(new[] { rec2.Id, rec1.Id, rec3.Id }, ordered.Select(r => r.Id).ToArray());

            Assert.All(ordered, r => Assert.NotNull(r.LearningResource));
            Assert.Equal("B", ordered[0].LearningResource!.Title);

            var limited = await repo.GetByUserIdWithResourceAsync(userId, limit: 2, CancellationToken.None);
            Assert.Equal(2, limited.Count);
            Assert.Equal(new[] { rec2.Id, rec1.Id }, limited.Select(r => r.Id).ToArray());
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task DeleteByUserIdAsync_DeletesOnlyTargetUser()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var res = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "R",
                Description = "d",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };
            context.LearningResources.Add(res);
            await context.SaveChangesAsync();

            context.Recommendations.AddRange(
                new Recommendation
                {
                    Id = Guid.NewGuid(),
                    UserId = "user-1",
                    LearningResourceId = res.Id,
                    Score = 0.1,
                    AlgorithmUsed = "Algo",
                    Explanation = "Exp",
                    LearningResource = res
                },
                new Recommendation
                {
                    Id = Guid.NewGuid(),
                    UserId = "user-2",
                    LearningResourceId = res.Id,
                    Score = 0.2,
                    AlgorithmUsed = "Algo",
                    Explanation = "Exp",
                    LearningResource = res
                });

            await context.SaveChangesAsync();

            var repo = new RecommendationRepository(context);

            var deleted = await repo.DeleteByUserIdAsync("user-1", CancellationToken.None);
            Assert.Equal(1, deleted);

            Assert.Equal(0, await context.Recommendations.CountAsync(r => r.UserId == "user-1"));
            Assert.Equal(1, await context.Recommendations.CountAsync(r => r.UserId == "user-2"));
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }
}

