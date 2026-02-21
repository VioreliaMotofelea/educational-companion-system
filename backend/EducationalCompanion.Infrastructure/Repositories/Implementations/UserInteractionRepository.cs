using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class UserInteractionRepository : GenericRepository<UserInteraction>, IUserInteractionRepository
{
    public UserInteractionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<UserInteraction>> GetByUserAsync(string userId, CancellationToken ct = default)
    {
        return await Query()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserInteraction>> SearchAsync(
        string? userId,
        Guid? learningResourceId,
        string? interactionType,
        CancellationToken ct = default)
    {
        var query = Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.UserId == userId);

        if (learningResourceId.HasValue)
            query = query.Where(x => x.LearningResourceId == learningResourceId.Value);

        if (!string.IsNullOrWhiteSpace(interactionType) &&
            Enum.TryParse<InteractionType>(interactionType, true, out var parsed))
            query = query.Where(x => x.InteractionType == parsed);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(string userId, Guid learningResourceId, CancellationToken ct = default)
    {
        return await Query()
            .AnyAsync(x => x.UserId == userId && x.LearningResourceId == learningResourceId, ct);
    }
}
