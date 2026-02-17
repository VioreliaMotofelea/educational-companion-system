using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class LearningResourceRepository 
    : GenericRepository<LearningResource>, ILearningResourceRepository
{
    public LearningResourceRepository(ApplicationDbContext context) 
        : base(context)
    {
    }

    public async Task<IReadOnlyList<LearningResource>> SearchAsync(
        string? topic,
        int? difficulty,
        string? contentType,
        CancellationToken ct = default)
    {
        var query = Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var t = topic.Trim().ToLowerInvariant();
            query = query.Where(x => x.Topic.ToLower() == t);
        }

        if (difficulty.HasValue)
            query = query.Where(x => x.Difficulty == difficulty.Value);

        if (!string.IsNullOrWhiteSpace(contentType) &&
            Enum.TryParse<ResourceContentType>(contentType, true, out var parsed))
        {
            query = query.Where(x => x.ContentType == parsed);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }
}