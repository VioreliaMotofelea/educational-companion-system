using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _repo;

    public UserProfileService(IUserProfileRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserXpResponse> GetXpByUserIdAsync(string userId, CancellationToken ct)
    {
        var profile = await _repo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);

        return new UserXpResponse(profile.UserId, profile.Level, profile.Xp);
    }
}
