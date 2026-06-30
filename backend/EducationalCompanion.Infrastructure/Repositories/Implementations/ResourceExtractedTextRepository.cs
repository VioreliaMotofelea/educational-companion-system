using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class ResourceExtractedTextRepository : IResourceExtractedTextRepository
{
    private readonly ApplicationDbContext _context;

    public ResourceExtractedTextRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ResourceExtractedText?> GetLatestByLearningResourceIdAsync(
        Guid learningResourceId,
        CancellationToken ct = default) =>
        await _context.ResourceExtractedTexts
            .AsNoTracking()
            .Where(t => t.LearningResourceId == learningResourceId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetLatestSummariesByResourceIdsAsync(
        IEnumerable<Guid> learningResourceIds,
        CancellationToken ct = default)
    {
        var ids = learningResourceIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var rows = await _context.ResourceExtractedTexts
            .AsNoTracking()
            .Where(t => ids.Contains(t.LearningResourceId) && t.Summary != null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new { t.LearningResourceId, t.Summary })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, string>();
        foreach (var row in rows)
        {
            if (!result.ContainsKey(row.LearningResourceId) && !string.IsNullOrWhiteSpace(row.Summary))
                result[row.LearningResourceId] = row.Summary!;
        }

        return result;
    }

    public async Task AddAsync(ResourceExtractedText entity, CancellationToken ct = default) =>
        await _context.ResourceExtractedTexts.AddAsync(entity, ct);

    public async Task RemoveByResourceFileIdAsync(Guid resourceFileId, CancellationToken ct = default)
    {
        var rows = await _context.ResourceExtractedTexts
            .Where(t => t.ResourceFileId == resourceFileId)
            .ToListAsync(ct);
        _context.ResourceExtractedTexts.RemoveRange(rows);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
