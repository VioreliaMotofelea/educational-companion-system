using EducationalCompanion.Api.Dtos.Users;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IUserProfileService
{
    Task<UserXpResponse> GetXpByUserIdAsync(string userId, CancellationToken ct);
}
