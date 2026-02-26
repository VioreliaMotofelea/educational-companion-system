using EducationalCompanion.Domain.Entities;

namespace EducationalCompanion.Infrastructure.Repositories.Abstractions;

public interface IUserInteractionRepository : IGenericRepository<UserInteraction>
{
    Task<IReadOnlyList<UserInteraction>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserInteraction>> SearchAsync(
        string? userId,
        Guid? learningResourceId,
        string? interactionType,
        CancellationToken ct = default);
    
    Task<bool> ExistsAsync(string userId, Guid learningResourceId, CancellationToken ct = default);
}
