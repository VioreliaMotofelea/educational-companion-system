using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IRecommendationRepository : IGenericRepository<Recommendation>
{
    // Gets recommendations for a user with LearningResource loaded (for EDM content list).
    Task<IReadOnlyList<Recommendation>> GetByUserIdWithResourceAsync(
        string userId,
        int? limit,
        CancellationToken ct = default);
}
