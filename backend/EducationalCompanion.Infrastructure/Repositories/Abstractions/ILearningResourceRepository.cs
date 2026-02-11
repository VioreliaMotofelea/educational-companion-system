using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface ILearningResourceRepository : IGenericRepository<LearningResource>
{
    Task<IReadOnlyList<LearningResource>> SearchAsync(
        string? topic,
        int? difficulty,
        string? contentType,
        CancellationToken ct = default);
}