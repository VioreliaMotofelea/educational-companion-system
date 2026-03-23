using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Edm;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

public class UserEdmReadRepositoryIntegrationTests
{
    [Fact]
    public async Task GetUserAnalyticsKpisAsync_ComputesKpisCorrectly()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var user = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                Level = 3,
                Xp = 250,
                DailyAvailableMinutes = 60
            };

            context.UserProfiles.Add(user);

            var resource1 = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "R1",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
            };
            var resource2 = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "R2",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article,
            };
            context.LearningResources.AddRange(resource1, resource2);
            await context.SaveChangesAsync();

            // viewed=2, completed=1 => completionRate=50
            context.UserInteractions.AddRange(
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = resource1.Id,
                    LearningResource = resource1,
                    InteractionType = InteractionType.Viewed,
                    Rating = 4,
                    TimeSpentMinutes = 10
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = resource2.Id,
                    LearningResource = resource2,
                    InteractionType = InteractionType.Viewed,
                    Rating = 2,
                    TimeSpentMinutes = 20
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = resource2.Id,
                    LearningResource = resource2,
                    InteractionType = InteractionType.Completed,
                    Rating = null,
                    TimeSpentMinutes = 30
                });

            context.StudyTasks.AddRange(
                new StudyTask
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    Title = "t1",
                    DeadlineUtc = DateTime.UtcNow.AddDays(1),
                    EstimatedMinutes = 10,
                    Priority = 3,
                    Status = EducationalCompanion.Domain.Enums.TaskStatus.Completed
                },
                new StudyTask
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    Title = "t2",
                    DeadlineUtc = DateTime.UtcNow.AddDays(1),
                    EstimatedMinutes = 10,
                    Priority = 3,
                    Status = EducationalCompanion.Domain.Enums.TaskStatus.Pending
                },
                new StudyTask
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    Title = "t3",
                    DeadlineUtc = DateTime.UtcNow.AddDays(1),
                    EstimatedMinutes = 10,
                    Priority = 3,
                    Status = EducationalCompanion.Domain.Enums.TaskStatus.Overdue
                });

            context.GamificationEvents.AddRange(
                new GamificationEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    EventType = GamificationEventType.CompletedResource,
                    XpGranted = 10
                },
                new GamificationEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    EventType = GamificationEventType.StreakAchieved,
                    XpGranted = 25
                });

            await context.SaveChangesAsync();

            var edmRepo = new UserEdmReadRepository(context);
            var kpis = await edmRepo.GetUserAnalyticsKpisAsync(user.UserId, CancellationToken.None);

            Assert.NotNull(kpis);
            Assert.Equal(2, kpis!.TotalResourcesViewed);
            Assert.Equal(1, kpis.TotalResourcesCompleted);
            Assert.Equal(50, kpis.CompletionRatePercent);
            Assert.Equal(3, kpis.AverageRatingGiven); // avg(4,2) = 3
            Assert.Equal(60, kpis.TotalTimeSpentMinutes); // 10+20+30
            Assert.Equal(250, kpis.TotalXpEarned);
            Assert.Equal(3, kpis.CurrentLevel);
            Assert.Equal(1, kpis.TasksCompleted);
            Assert.Equal(1, kpis.TasksPending);
            Assert.Equal(1, kpis.TasksOverdue);
            Assert.Equal(2, kpis.GamificationEventsCount);
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetTopicMasteryDataAsync_GroupsAndRounds()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var user = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                Level = 1,
                Xp = 0,
                DailyAvailableMinutes = 60
            };
            context.UserProfiles.Add(user);

            var python1 = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "p1",
                Topic = "Python",
                Difficulty = 2,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };
            var python2 = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "p2",
                Topic = "Python",
                Difficulty = 4,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };
            var db1 = new LearningResource
            {
                Id = Guid.NewGuid(),
                Title = "d1",
                Topic = "Databases",
                Difficulty = 3,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };

            context.LearningResources.AddRange(python1, python2, db1);
            await context.SaveChangesAsync();

            // Python: 2 completed resources, rating only on one => avgRating = 5
            // Databases: 1 completed resource, rating null => AverageRating should be null
            context.UserInteractions.AddRange(
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = python1.Id,
                    LearningResource = python1,
                    InteractionType = InteractionType.Completed,
                    Rating = 5,
                    TimeSpentMinutes = 10
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = python2.Id,
                    LearningResource = python2,
                    InteractionType = InteractionType.Completed,
                    Rating = null,
                    TimeSpentMinutes = 10
                },
                new UserInteraction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    LearningResourceId = db1.Id,
                    LearningResource = db1,
                    InteractionType = InteractionType.Completed,
                    Rating = null,
                    TimeSpentMinutes = 10
                });

            await context.SaveChangesAsync();

            var edmRepo = new UserEdmReadRepository(context);
            var mastery = await edmRepo.GetTopicMasteryDataAsync(user.UserId, CancellationToken.None);

            Assert.Equal(2, mastery.Count);

            // Python should come first (2 completed resources)
            Assert.Equal("Python", mastery[0].Topic);
            Assert.Equal(2, mastery[0].ResourcesCompleted);
            Assert.Equal(5, mastery[0].AverageRating);
            Assert.Equal(3, mastery[0].AverageDifficultyCompleted); // avg(2,4)=3

            Assert.Equal("Databases", mastery[1].Topic);
            Assert.Equal(1, mastery[1].ResourcesCompleted);
            Assert.Null(mastery[1].AverageRating);
            Assert.Equal(3, mastery[1].AverageDifficultyCompleted);
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }
}

