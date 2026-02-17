using EducationalCompanion.Api.Dtos.LearningResources;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface ILearningResourceService
{
    Task<IReadOnlyList<LearningResourceResponse>> GetAllAsync(CancellationToken ct);
    Task<LearningResourceResponse> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<LearningResourceResponse>> SearchAsync(string? topic, int? difficulty, string? contentType, CancellationToken ct);
    Task<LearningResourceResponse> CreateAsync(CreateLearningResourceRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateLearningResourceRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}