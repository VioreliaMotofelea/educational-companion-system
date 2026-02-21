using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class UserProfileRepository : GenericRepository<UserProfile>, IUserProfileRepository
{
    public UserProfileRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await Query()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
    }

    public async Task<UserProfile?> GetByUserIdWithPreferencesAsync(string userId, CancellationToken ct = default)
    {
        return await Query()
            .Include(p => p.Preferences)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
    }
}
