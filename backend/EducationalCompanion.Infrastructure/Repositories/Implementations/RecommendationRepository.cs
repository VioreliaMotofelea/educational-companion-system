using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class RecommendationRepository : GenericRepository<Recommendation>, IRecommendationRepository
{
    public RecommendationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Recommendation>> GetByUserIdWithResourceAsync(
        string userId,
        int? limit,
        CancellationToken ct = default)
    {
        var query = Query()
            .AsNoTracking()
            .Include(r => r.LearningResource)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAtUtc);

        if (limit.HasValue && limit.Value > 0)
            query = query.Take(limit.Value);

        return await query.ToListAsync(ct);
    }
}
