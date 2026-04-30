using EducationalCompanion.Api.Dtos.Tasks;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using DomainTaskStatus = EducationalCompanion.Domain.Enums.TaskStatus;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Api.Services.Implementations;

public class StudyTaskService : IStudyTaskService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly ILearningResourceRepository _learningResourceRepo;

    public StudyTaskService(
        ApplicationDbContext dbContext,
        IUserProfileRepository userProfileRepo,
        ILearningResourceRepository learningResourceRepo)
    {
        _dbContext = dbContext;
        _userProfileRepo = userProfileRepo;
        _learningResourceRepo = learningResourceRepo;
    }

    public async Task<IReadOnlyList<StudyTaskResponse>> GetByUserAsync(string userId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var now = DateTime.UtcNow;
        var staleOverdue = await _dbContext.StudyTasks
            .Where(t => t.UserId == userId && t.Status == DomainTaskStatus.Pending && t.DeadlineUtc < now)
            .ToListAsync(ct);
        if (staleOverdue.Count > 0)
        {
            foreach (var task in staleOverdue)
                task.Status = DomainTaskStatus.Overdue;
            await _dbContext.SaveChangesAsync(ct);
        }

        var tasks = await _dbContext.StudyTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DeadlineUtc)
            .ToListAsync(ct);

        if (tasks.Count == 0)
            return [];

        var resourceIds = tasks.Where(t => t.LearningResourceId.HasValue).Select(t => t.LearningResourceId!.Value).Distinct().ToList();
        var resources = await _dbContext.LearningResources
            .AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        return tasks.Select(t => Map(t, resources)).ToList();
    }

    public async Task<StudyTaskResponse> CreateForUserAsync(string userId, CreateStudyTaskRequest request, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        if (request.LearningResourceId.HasValue)
            await EnsureLearningResourceExistsAsync(request.LearningResourceId.Value, ct);

        var entity = new StudyTask
        {
            UserId = userId,
            LearningResourceId = request.LearningResourceId,
            Title = request.Title.Trim(),
            Notes = request.Notes,
            DeadlineUtc = request.DeadlineUtc,
            EstimatedMinutes = request.EstimatedMinutes,
            Priority = request.Priority,
            Status = DomainTaskStatus.Pending
        };

        await _dbContext.StudyTasks.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);

        var title = entity.LearningResourceId.HasValue
            ? await _dbContext.LearningResources
                .AsNoTracking()
                .Where(r => r.Id == entity.LearningResourceId.Value)
                .Select(r => r.Title)
                .FirstOrDefaultAsync(ct)
            : null;

        return Map(entity, title);
    }

    public async Task<StudyTaskResponse> UpdateStatusAsync(string userId, Guid taskId, UpdateStudyTaskStatusRequest request, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);
        if (!Enum.TryParse<DomainTaskStatus>(request.Status, true, out var parsedStatus))
            throw new ValidationException($"Invalid task status '{request.Status}'. Use Pending, Completed, or Overdue.");

        var task = await _dbContext.StudyTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
        if (task is null)
            throw new StudyTaskNotFoundException(taskId);

        task.Status = parsedStatus;
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var title = task.LearningResourceId.HasValue
            ? await _dbContext.LearningResources
                .AsNoTracking()
                .Where(r => r.Id == task.LearningResourceId.Value)
                .Select(r => r.Title)
                .FirstOrDefaultAsync(ct)
            : null;

        return Map(task, title);
    }

    public async Task EnsurePendingTasksForRecommendationsAsync(string userId, IReadOnlyList<Guid> resourceIds, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);
        if (resourceIds.Count == 0) return;

        var topResourceIds = resourceIds.Distinct().Take(5).ToList();
        var existingPendingIds = await _dbContext.StudyTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Status == DomainTaskStatus.Pending && t.LearningResourceId.HasValue)
            .Select(t => t.LearningResourceId!.Value)
            .ToListAsync(ct);

        var toCreate = topResourceIds.Except(existingPendingIds).ToList();
        if (toCreate.Count == 0) return;

        var resources = await _dbContext.LearningResources
            .AsNoTracking()
            .Where(r => toCreate.Contains(r.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var index = 0;
        foreach (var resource in resources)
        {
            await _dbContext.StudyTasks.AddAsync(new StudyTask
            {
                UserId = userId,
                LearningResourceId = resource.Id,
                Title = $"Study: {resource.Title}",
                Notes = "Auto-created from current recommendations.",
                EstimatedMinutes = Math.Max(5, resource.EstimatedDurationMinutes),
                Priority = 3,
                DeadlineUtc = now.AddDays(1 + index),
                Status = DomainTaskStatus.Pending
            }, ct);
            index++;
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task MarkTaskCompletedForResourceAsync(string userId, Guid learningResourceId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var pending = await _dbContext.StudyTasks
            .Where(t => t.UserId == userId && t.LearningResourceId == learningResourceId && t.Status == DomainTaskStatus.Pending)
            .OrderBy(t => t.DeadlineUtc)
            .FirstOrDefaultAsync(ct);

        if (pending is null)
            return;

        pending.Status = DomainTaskStatus.Completed;
        pending.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task EnsureUserExistsAsync(string userId, CancellationToken ct)
    {
        var profile = await _userProfileRepo.GetByUserIdAsync(userId, ct);
        if (profile is null)
            throw new UserProfileNotFoundException(userId);
    }

    private async Task EnsureLearningResourceExistsAsync(Guid learningResourceId, CancellationToken ct)
    {
        var resource = await _learningResourceRepo.GetByIdAsync(learningResourceId, ct);
        if (resource is null)
            throw new LearningResourceNotFoundException(learningResourceId);
    }

    private static StudyTaskResponse Map(StudyTask task, IReadOnlyDictionary<Guid, LearningResource> resources)
    {
        string? resourceTitle = null;
        if (task.LearningResourceId.HasValue && resources.TryGetValue(task.LearningResourceId.Value, out var resource))
            resourceTitle = resource.Title;
        return Map(task, resourceTitle);
    }

    private static StudyTaskResponse Map(StudyTask task, string? resourceTitle) =>
        new(
            task.Id,
            task.UserId,
            task.LearningResourceId,
            resourceTitle,
            task.Title,
            task.Notes,
            task.DeadlineUtc,
            task.EstimatedMinutes,
            task.Priority,
            task.Status.ToString(),
            task.CreatedAtUtc,
            task.UpdatedAtUtc
        );
}

