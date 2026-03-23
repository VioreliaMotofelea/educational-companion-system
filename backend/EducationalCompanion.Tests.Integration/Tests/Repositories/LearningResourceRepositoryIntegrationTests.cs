using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

public class LearningResourceRepositoryIntegrationTests
{
    [Fact]
    public async Task SearchAsync_FiltersAndOrdersByCreatedAtUtc()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var pythonArticleOld = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "t1",
                Description = "d1",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
                CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            var pythonVideoNew = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "t2",
                Description = "d2",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 20,
                ContentType = ResourceContentType.Video,
                CreatedAtUtc = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            };

            var otherTopic = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "t3",
                Description = "d3",
                Topic = "C#",
                Difficulty = 2,
                EstimatedDurationMinutes = 30,
                ContentType = ResourceContentType.Article,
                CreatedAtUtc = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            };

            context.LearningResources.AddRange(pythonArticleOld, pythonVideoNew, otherTopic);
            await context.SaveChangesAsync();

            var repo = new LearningResourceRepository(context);

            // contentType filter should match only Article; topic filter should be case/space-insensitive; ordering by CreatedAtUtc desc.
            var results = await repo.SearchAsync(topic: " python ", difficulty: 2, contentType: "aRtIcLe", CancellationToken.None);

            Assert.Single(results);
            Assert.Equal(pythonArticleOld.Id, results[0].Id);

            // invalid contentType should not filter (repository uses TryParse and just skips contentType filter).
            var invalidResults = await repo.SearchAsync(topic: "python", difficulty: 2, contentType: "NotAType", CancellationToken.None);

            Assert.Equal(2, invalidResults.Count);
            Assert.Equal(pythonVideoNew.Id, invalidResults[0].Id); // newest first
            Assert.Equal(pythonArticleOld.Id, invalidResults[1].Id);
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }
}

