using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class UserPreferencesRepository : GenericRepository<UserPreferences>, IUserPreferencesRepository
{
    public UserPreferencesRepository(ApplicationDbContext context) : base(context) { }
}
