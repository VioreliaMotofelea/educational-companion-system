using EducationalCompanion.Api.Dtos.Users;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IUserProfileService
{
    Task<UserXpResponse> GetXpByUserIdAsync(string userId, CancellationToken ct);
    Task<UserProfileResponse> GetProfileByUserIdAsync(string userId, CancellationToken ct);
    Task<UserPreferencesResponse> GetPreferencesByUserIdAsync(string userId, CancellationToken ct);
    Task UpdatePreferencesAsync(string userId, UpdateUserPreferencesRequest request, CancellationToken ct);
}
