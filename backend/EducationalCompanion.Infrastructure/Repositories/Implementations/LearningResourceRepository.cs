using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class LearningResourceRepository : GenericRepository<LearningResource>, ILearningResourceRepository
{
    public LearningResourceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<LearningResource>> SearchAsync(
        string? topic,
        int? difficulty,
        string? contentType,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(topic))
            query = query.Where(r => r.Topic.ToLower() == topic.ToLower());

        if (difficulty.HasValue)
            query = query.Where(r => r.Difficulty == difficulty.Value);

        if (!string.IsNullOrWhiteSpace(contentType) &&
            Enum.TryParse<ResourceContentType>(contentType, true, out var parsed))
            query = query.Where(r => r.ContentType == parsed);

        return await query.ToListAsync(ct);
    }
}