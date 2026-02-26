using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class UserProfileService : IUserProfileService
{
    private const int MinPreferredDifficulty = 1;
    private const int MaxPreferredDifficulty = 5;

    private readonly IUserProfileRepository _repo;
    private readonly IUserPreferencesRepository _preferencesRepo;

    public UserProfileService(IUserProfileRepository repo, IUserPreferencesRepository preferencesRepo)
    {
        _repo = repo;
        _preferencesRepo = preferencesRepo;
    }

    public async Task<UserXpResponse> GetXpByUserIdAsync(string userId, CancellationToken ct)
    {
        var profile = await _repo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);

        return new UserXpResponse(profile.UserId, profile.Level, profile.Xp);
    }

    public async Task<UserProfileResponse> GetProfileByUserIdAsync(string userId, CancellationToken ct)
    {
        var profile = await _repo.GetByUserIdWithPreferencesAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);

        return MapToProfileResponse(profile);
    }

    public async Task<UserPreferencesResponse> GetPreferencesByUserIdAsync(string userId, CancellationToken ct)
    {
        var profile = await _repo.GetByUserIdWithPreferencesAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);

        return MapToPreferencesResponse(profile.Preferences);
    }

    public async Task UpdatePreferencesAsync(string userId, UpdateUserPreferencesRequest request, CancellationToken ct)
    {
        var profile = await _repo.GetByUserIdWithPreferencesAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);

        var prefs = profile.Preferences;
        if (prefs is null)
        {
            prefs = new UserPreferences
            {
                UserProfileId = profile.Id,
                PreferredDifficulty = request.PreferredDifficulty,
                PreferredContentTypesCsv = request.PreferredContentTypesCsv,
                PreferredTopicsCsv = request.PreferredTopicsCsv
            };
            await _preferencesRepo.AddAsync(prefs, ct);
        }
        else
        {
            if (request.PreferredDifficulty.HasValue)
            {
                ValidatePreferredDifficulty(request.PreferredDifficulty.Value);
                prefs.PreferredDifficulty = request.PreferredDifficulty;
            }
            if (request.PreferredContentTypesCsv is not null)
                prefs.PreferredContentTypesCsv = request.PreferredContentTypesCsv;
            if (request.PreferredTopicsCsv is not null)
                prefs.PreferredTopicsCsv = request.PreferredTopicsCsv;
        }

        await _repo.SaveChangesAsync(ct);
    }

    private static void ValidatePreferredDifficulty(int value)
    {
        if (value < MinPreferredDifficulty || value > MaxPreferredDifficulty)
            throw new InvalidDifficultyException(value);
    }

    private static UserProfileResponse MapToProfileResponse(UserProfile p) =>
        new(
            p.UserId,
            p.Level,
            p.Xp,
            p.DailyAvailableMinutes,
            MapToPreferencesResponse(p.Preferences)
        );

    private static UserPreferencesResponse MapToPreferencesResponse(UserPreferences? p) =>
        p is null ? new UserPreferencesResponse(null, null, null) : new UserPreferencesResponse(p.PreferredDifficulty, p.PreferredContentTypesCsv, p.PreferredTopicsCsv);
}
