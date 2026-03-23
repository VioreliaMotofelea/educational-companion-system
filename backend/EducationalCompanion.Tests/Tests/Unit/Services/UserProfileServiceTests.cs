using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class UserProfileServiceTests
{
    [Fact]
    public async Task GetXpByUserIdAsync_ThrowsWhenUserMissing()
    {
        var profileRepo = new FakeUserProfileRepository(profile: null);
        var prefsRepo = new FakeUserPreferencesRepository();
        var service = new UserProfileService(profileRepo, prefsRepo);

        await Assert.ThrowsAsync<UserProfileNotFoundException>(() => service.GetXpByUserIdAsync("missing-user", CancellationToken.None));
    }

    [Fact]
    public async Task GetProfileByUserIdAsync_ReturnsPreferences()
    {
        var profile = new UserProfile
        {
            UserId = "user-1",
            Level = 2,
            Xp = 120,
            DailyAvailableMinutes = 60,
            Preferences = new UserPreferences
            {
                PreferredDifficulty = 2,
                PreferredContentTypesCsv = "Article,Video",
                PreferredTopicsCsv = "Python,AI"
            }
        };

        var profileRepo = new FakeUserProfileRepository(profile: profile);
        var prefsRepo = new FakeUserPreferencesRepository();
        var service = new UserProfileService(profileRepo, prefsRepo);

        var result = await service.GetProfileByUserIdAsync("user-1", CancellationToken.None);

        Assert.Equal("user-1", result.UserId);
        Assert.Equal(2, result.Level);
        Assert.Equal(120, result.Xp);
        Assert.Equal(60, result.DailyAvailableMinutes);
        Assert.NotNull(result.Preferences);
        Assert.Equal(2, result.Preferences!.PreferredDifficulty);
        Assert.Equal("Article,Video", result.Preferences.PreferredContentTypesCsv);
        Assert.Equal("Python,AI", result.Preferences.PreferredTopicsCsv);
    }

    [Fact]
    public async Task UpdatePreferencesAsync_WhenPrefsNull_AddsNewPreferences()
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Level = 2,
            Xp = 120,
            DailyAvailableMinutes = 60,
            Preferences = null
        };

        var profileRepo = new FakeUserProfileRepository(profile: profile);
        var prefsRepo = new FakeUserPreferencesRepository();
        var service = new UserProfileService(profileRepo, prefsRepo);

        await service.UpdatePreferencesAsync(
            "user-1",
            new UpdateUserPreferencesRequest(
                PreferredDifficulty: 3,
                PreferredContentTypesCsv: "Video",
                PreferredTopicsCsv: "C#,Databases"),
            CancellationToken.None);

        Assert.Equal(1, prefsRepo.AddCalls);
        Assert.Equal(1, profileRepo.SaveCalls);

        Assert.NotNull(prefsRepo.Stored);
        Assert.Equal(profile.Id, prefsRepo.Stored!.UserProfileId);
        Assert.Equal(3, prefsRepo.Stored.PreferredDifficulty);
        Assert.Equal("Video", prefsRepo.Stored.PreferredContentTypesCsv);
        Assert.Equal("C#,Databases", prefsRepo.Stored.PreferredTopicsCsv);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task UpdatePreferencesAsync_InvalidPreferredDifficulty_Throws(int difficulty)
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Preferences = new UserPreferences { PreferredDifficulty = 2 }
        };

        var profileRepo = new FakeUserProfileRepository(profile: profile);
        var prefsRepo = new FakeUserPreferencesRepository();
        var service = new UserProfileService(profileRepo, prefsRepo);

        await Assert.ThrowsAsync<InvalidDifficultyException>(() =>
            service.UpdatePreferencesAsync(
                "user-1",
                new UpdateUserPreferencesRequest(
                    PreferredDifficulty: difficulty,
                    PreferredContentTypesCsv: null,
                    PreferredTopicsCsv: null),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePreferencesAsync_WhenPrefsExists_UpdatesOnlyProvidedFields()
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Preferences = new UserPreferences
            {
                PreferredDifficulty = 2,
                PreferredContentTypesCsv = "Article",
                PreferredTopicsCsv = "Python"
            }
        };

        var profileRepo = new FakeUserProfileRepository(profile: profile);
        var prefsRepo = new FakeUserPreferencesRepository();
        var service = new UserProfileService(profileRepo, prefsRepo);

        await service.UpdatePreferencesAsync(
            "user-1",
            new UpdateUserPreferencesRequest(
                PreferredDifficulty: 4,
                PreferredContentTypesCsv: null,
                PreferredTopicsCsv: "AI"),
            CancellationToken.None);

        Assert.Equal(0, prefsRepo.AddCalls);
        Assert.Equal(1, profileRepo.SaveCalls);

        Assert.Equal(4, profile.Preferences!.PreferredDifficulty);
        Assert.Equal("Article", profile.Preferences.PreferredContentTypesCsv); // unchanged
        Assert.Equal("AI", profile.Preferences.PreferredTopicsCsv); // updated
    }

    private sealed class FakeUserProfileRepository : IUserProfileRepository
    {
        private readonly UserProfile? _profile;
        public int SaveCalls { get; private set; }

        public FakeUserProfileRepository(UserProfile? profile) => _profile = profile;

        public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_profile != null && _profile.UserId == userId ? _profile : null);

        public Task<UserProfile?> GetByUserIdWithPreferencesAsync(string userId, CancellationToken ct = default) =>
            GetByUserIdAsync(userId, ct);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<UserProfile?>(null);
        public Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserProfile>>(Array.Empty<UserProfile>());
        public IQueryable<UserProfile> Query() => Array.Empty<UserProfile>().AsQueryable();
        public Task AddAsync(UserProfile entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(UserProfile entity) { }
        public void Remove(UserProfile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeUserPreferencesRepository : IUserPreferencesRepository
    {
        public int AddCalls { get; private set; }
        public UserPreferences? Stored { get; private set; }

        public Task<IReadOnlyList<UserPreferences>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UserPreferences>>(Array.Empty<UserPreferences>());
        public IQueryable<UserPreferences> Query() => Array.Empty<UserPreferences>().AsQueryable();

        public Task<UserPreferences?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<UserPreferences?>(null);
        public Task AddAsync(UserPreferences entity, CancellationToken ct = default)
        {
            AddCalls++;
            Stored = entity;
            return Task.CompletedTask;
        }

        public void Update(UserPreferences entity) { }
        public void Remove(UserPreferences entity) { }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

