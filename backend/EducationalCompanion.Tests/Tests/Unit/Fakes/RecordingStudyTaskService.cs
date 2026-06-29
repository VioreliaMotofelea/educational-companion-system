using EducationalCompanion.Api.Dtos.Tasks;
using EducationalCompanion.Api.Services.Abstractions;

namespace EducationalCompanion.Tests.Tests.Unit.Fakes;

/// <summary>
/// Records study-task side effects for recommendation batch tests.
/// </summary>
public sealed class RecordingStudyTaskService : IStudyTaskService
{
    public IReadOnlyList<Guid> LastRecommendationResourceIds { get; private set; } = Array.Empty<Guid>();
    public string? LastRecommendationUserId { get; private set; }
    public int EnsurePendingTasksCallCount { get; private set; }

    public Task<IReadOnlyList<StudyTaskResponse>> GetByUserAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StudyTaskResponse>>(Array.Empty<StudyTaskResponse>());

    public Task<StudyTaskResponse> CreateForUserAsync(string userId, CreateStudyTaskRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<StudyTaskResponse> UpdateAsync(string userId, Guid taskId, UpdateStudyTaskRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<StudyTaskResponse> UpdateStatusAsync(string userId, Guid taskId, UpdateStudyTaskStatusRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string userId, Guid taskId, CancellationToken ct = default) => Task.CompletedTask;

    public Task EnsurePendingTasksForRecommendationsAsync(
        string userId,
        IReadOnlyList<Guid> resourceIds,
        CancellationToken ct = default)
    {
        EnsurePendingTasksCallCount++;
        LastRecommendationUserId = userId;
        LastRecommendationResourceIds = resourceIds.ToList();
        return Task.CompletedTask;
    }

    public Task SyncRecommendationLinkedTasksAsync(string userId, IReadOnlyList<Guid> activeResourceIds, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task MarkTaskCompletedForResourceAsync(string userId, Guid learningResourceId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
