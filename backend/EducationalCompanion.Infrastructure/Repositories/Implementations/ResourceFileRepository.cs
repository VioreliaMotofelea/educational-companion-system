using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Repositories.Implementations;

public class ResourceFileRepository : IResourceFileRepository
{
    private readonly ApplicationDbContext _context;

    public ResourceFileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ResourceFile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.ResourceFiles.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<ResourceFile>> GetByLearningResourceIdAsync(
        Guid learningResourceId,
        CancellationToken ct = default) =>
        await _context.ResourceFiles
            .AsNoTracking()
            .Where(f => f.LearningResourceId == learningResourceId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> GetResourceIdsWithFilesAsync(
        IEnumerable<Guid> learningResourceIds,
        CancellationToken ct = default)
    {
        var ids = learningResourceIds.Distinct().ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var withFiles = await _context.ResourceFiles
            .AsNoTracking()
            .Where(f => ids.Contains(f.LearningResourceId))
            .Select(f => f.LearningResourceId)
            .Distinct()
            .ToListAsync(ct);

        return withFiles.ToHashSet();
    }

    public async Task AddAsync(ResourceFile entity, CancellationToken ct = default)
    {
        await _context.ResourceFiles.AddAsync(entity, ct);
    }

    public void Remove(ResourceFile entity) => _context.ResourceFiles.Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
