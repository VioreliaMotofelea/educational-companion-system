using System;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

public class UserInteractionRepositoryIntegrationTests
{
    [Fact]
    public async Task SearchAsync_FiltersAndOrdersByCreatedAtUtc()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var resource = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "R",
                Description = "d",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };
            context.LearningResources.Add(resource);
            await context.SaveChangesAsync();

            var userId = "user-1";
            var otherUser = "user-2";

            context.UserInteractions.AddRange(
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LearningResourceId = resource.Id,
                    LearningResource = resource,
                    InteractionType = InteractionType.Viewed,
                    Rating = null,
                    TimeSpentMinutes = 10,
                    CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LearningResourceId = resource.Id,
                    LearningResource = resource,
                    InteractionType = InteractionType.Completed,
                    Rating = 4,
                    TimeSpentMinutes = 20,
                    CreatedAtUtc = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc)
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = otherUser,
                    LearningResourceId = resource.Id,
                    LearningResource = resource,
                    InteractionType = InteractionType.Completed,
                    Rating = 5,
                    TimeSpentMinutes = 30,
                    CreatedAtUtc = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            await context.SaveChangesAsync();

            var repo = new UserInteractionRepository(context);

            var completed = await repo.SearchAsync(
                userId: userId,
                learningResourceId: resource.Id,
                interactionType: "completed",
                CancellationToken.None);

            Assert.Single(completed);
            Assert.Equal(InteractionType.Completed, completed[0].InteractionType);
            Assert.Equal(new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc), completed[0].CreatedAtUtc);

            // Invalid interactionType => repository skips filter, so we should get both viewed + completed for that user/resource.
            var invalidInteractionType = await repo.SearchAsync(
                userId: userId,
                learningResourceId: resource.Id,
                interactionType: "NotAType",
                CancellationToken.None);

            Assert.Equal(2, invalidInteractionType.Count);
            Assert.Equal(InteractionType.Completed, invalidInteractionType[0].InteractionType); // newest first
            Assert.Equal(InteractionType.Viewed, invalidInteractionType[1].InteractionType);
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }
}

