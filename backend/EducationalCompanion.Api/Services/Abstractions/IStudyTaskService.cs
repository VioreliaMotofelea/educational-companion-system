using EducationalCompanion.Api.Dtos.Tasks;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IStudyTaskService
{
    Task<IReadOnlyList<StudyTaskResponse>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<StudyTaskResponse> CreateForUserAsync(string userId, CreateStudyTaskRequest request, CancellationToken ct = default);
    Task<StudyTaskResponse> UpdateAsync(string userId, Guid taskId, UpdateStudyTaskRequest request, CancellationToken ct = default);
    Task<StudyTaskResponse> UpdateStatusAsync(string userId, Guid taskId, UpdateStudyTaskStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(string userId, Guid taskId, CancellationToken ct = default);
    Task EnsurePendingTasksForRecommendationsAsync(string userId, IReadOnlyList<Guid> resourceIds, CancellationToken ct = default);
    Task MarkTaskCompletedForResourceAsync(string userId, Guid learningResourceId, CancellationToken ct = default);
}

