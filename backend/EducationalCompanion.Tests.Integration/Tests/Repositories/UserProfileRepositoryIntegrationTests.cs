using System;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Repositories.Implementations;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

public class UserProfileRepositoryIntegrationTests
{
    [Fact]
    public async Task GetByUserIdWithPreferencesAsync_LoadsPreferencesWhenPresent()
    {
        var (connection, context) = TestDbContextFactory.CreateContext();
        try
        {
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                Level = 2,
                Xp = 120,
                DailyAvailableMinutes = 60,
                Preferences = new UserPreferences
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = Guid.Empty, // will be set by relationship conventions; keep non-null
                    PreferredDifficulty = 2,
                    PreferredContentTypesCsv = "Article",
                    PreferredTopicsCsv = "Python"
                }
            };

            // Since UserProfileId is required in configuration but derived from relationship, we set it after profile id.
            profile.Preferences!.UserProfileId = profile.Id;

            context.UserProfiles.Add(profile);
            await context.SaveChangesAsync();

            var repo = new UserProfileRepository(context);
            var loaded = await repo.GetByUserIdWithPreferencesAsync("user-1", CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.Preferences);
            Assert.Equal(2, loaded.Preferences!.PreferredDifficulty);
            Assert.Equal("Article", loaded.Preferences!.PreferredContentTypesCsv);
            Assert.Equal("Python", loaded.Preferences!.PreferredTopicsCsv);
        }
        finally
        {
            await context.DisposeAsync();
            connection.Dispose();
        }
    }
}

