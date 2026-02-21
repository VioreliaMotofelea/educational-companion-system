using EducationalCompanion.Api.Dtos.UserInteractions;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IUserInteractionService
{
    Task<IReadOnlyList<UserInteractionResponse>> GetAllAsync(CancellationToken ct);
    Task<UserInteractionResponse> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<UserInteractionResponse>> GetByUserAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<UserInteractionResponse>> SearchAsync(string? userId, Guid? learningResourceId, string? interactionType, CancellationToken ct);
    Task<UserInteractionResponse> CreateAsync(CreateUserInteractionRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateUserInteractionRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
