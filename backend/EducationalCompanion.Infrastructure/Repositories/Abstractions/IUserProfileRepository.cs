using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IUserProfileRepository : IGenericRepository<UserProfile>
{
    Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserProfile?> GetByUserIdWithPreferencesAsync(string userId, CancellationToken ct = default);
}
