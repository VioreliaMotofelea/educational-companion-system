using EducationalCompanion.Api.Dtos.Tasks;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using DomainTaskStatus = EducationalCompanion.Domain.Enums.TaskStatus;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EducationalCompanion.Api.Services.Implementations;

public class StudyTaskService : IStudyTaskService
{
    internal const string AutoCreatedFromRecommendationsNote = "Auto-created from current recommendations.";
    private const int AutoTaskPipelineTarget = 10;

    private readonly ApplicationDbContext _dbContext;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly ILearningResourceRepository _learningResourceRepo;
    private readonly IOptions<CalendarOptions> _calendarOptions;

    public StudyTaskService(
        ApplicationDbContext dbContext,
        IUserProfileRepository userProfileRepo,
        ILearningResourceRepository learningResourceRepo,
        IOptions<CalendarOptions> calendarOptions)
    {
        _dbContext = dbContext;
        _userProfileRepo = userProfileRepo;
        _learningResourceRepo = learningResourceRepo;
        _calendarOptions = calendarOptions;
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

        await RefillAutoTasksFromCurrentRecommendationsAsync(userId, ct);

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

    public async Task<StudyTaskResponse> UpdateAsync(string userId, Guid taskId, UpdateStudyTaskRequest request, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);
        if (request.LearningResourceId.HasValue)
            await EnsureLearningResourceExistsAsync(request.LearningResourceId.Value, ct);

        var task = await _dbContext.StudyTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
        if (task is null)
            throw new StudyTaskNotFoundException(taskId);
        if (task.LearningResourceId.HasValue)
            throw new ValidationException("This task is linked to a recommendation resource and cannot be edited.");

        task.LearningResourceId = request.LearningResourceId;
        task.Title = request.Title.Trim();
        task.Notes = request.Notes;
        task.DeadlineUtc = request.DeadlineUtc;
        task.EstimatedMinutes = request.EstimatedMinutes;
        task.Priority = request.Priority;
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

        var topResourceIds = resourceIds.Distinct().Take(AutoTaskPipelineTarget).ToList();
        var existingOpenIds = await _dbContext.StudyTasks
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId
                && t.LearningResourceId.HasValue
                && (t.Status == DomainTaskStatus.Pending || t.Status == DomainTaskStatus.Overdue))
            .Select(t => t.LearningResourceId!.Value)
            .ToListAsync(ct);

        var toCreate = topResourceIds.Except(existingOpenIds).ToList();
        if (toCreate.Count == 0) return;

        var resources = await _dbContext.LearningResources
            .AsNoTracking()
            .Where(r => toCreate.Contains(r.Id))
            .ToListAsync(ct);

        var resourceById = resources.ToDictionary(r => r.Id);
        var ordered = toCreate.Select(id => resourceById[id]).ToList();
        var estimates = ordered.Select(r => Math.Max(5, r.EstimatedDurationMinutes)).ToList();

        var profile = await _userProfileRepo.GetByUserIdAsync(userId, ct);
        var dailyMinutes = profile?.DailyAvailableMinutes ?? 60;
        var now = DateTime.UtcNow;
        var deadlineZone = ResolveDeadlineTimeZone();
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(now, dailyMinutes, estimates, deadlineZone);

        for (var i = 0; i < ordered.Count; i++)
        {
            var resource = ordered[i];
            await _dbContext.StudyTasks.AddAsync(new StudyTask
            {
                UserId = userId,
                LearningResourceId = resource.Id,
                Title = $"Study: {resource.Title}",
                Notes = AutoCreatedFromRecommendationsNote,
                EstimatedMinutes = estimates[i],
                Priority = 3,
                DeadlineUtc = deadlines[i],
                Status = DomainTaskStatus.Pending
            }, ct);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task SyncRecommendationLinkedTasksAsync(
        string userId,
        IReadOnlyList<Guid> activeResourceIds,
        CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var activeSet = activeResourceIds.Distinct().ToHashSet();
        var stalePending = await _dbContext.StudyTasks
            .Where(t =>
                t.UserId == userId
                && t.Status == DomainTaskStatus.Pending
                && t.LearningResourceId.HasValue
                && t.Notes == AutoCreatedFromRecommendationsNote)
            .ToListAsync(ct);

        var removed = stalePending.Where(t => !activeSet.Contains(t.LearningResourceId!.Value)).ToList();
        if (removed.Count == 0)
            return;

        _dbContext.StudyTasks.RemoveRange(removed);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task MarkTaskCompletedForResourceAsync(string userId, Guid learningResourceId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var openTasks = await _dbContext.StudyTasks
            .Where(t =>
                t.UserId == userId
                && t.LearningResourceId == learningResourceId
                && (t.Status == DomainTaskStatus.Pending || t.Status == DomainTaskStatus.Overdue))
            .ToListAsync(ct);

        if (openTasks.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var task in openTasks)
        {
            task.Status = DomainTaskStatus.Completed;
            task.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(ct);
        await RefillAutoTasksFromCurrentRecommendationsAsync(userId, ct);
    }

    public async Task DeleteAsync(string userId, Guid taskId, CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var task = await _dbContext.StudyTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
        if (task is null)
            throw new StudyTaskNotFoundException(taskId);

        _dbContext.StudyTasks.Remove(task);
        await _dbContext.SaveChangesAsync(ct);
    }

    private TimeZoneInfo? ResolveDeadlineTimeZone()
    {
        var id = _calendarOptions.Value.DeadlineTimeZoneId;
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
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

    private async Task RefillAutoTasksFromCurrentRecommendationsAsync(string userId, CancellationToken ct)
    {
        var openLinkedCount = await _dbContext.StudyTasks
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId
                && t.LearningResourceId.HasValue
                && (t.Status == DomainTaskStatus.Pending || t.Status == DomainTaskStatus.Overdue))
            .CountAsync(ct);
        if (openLinkedCount >= AutoTaskPipelineTarget)
            return;

        var openLinkedResourceIds = await _dbContext.StudyTasks
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId
                && t.LearningResourceId.HasValue
                && (t.Status == DomainTaskStatus.Pending || t.Status == DomainTaskStatus.Overdue))
            .Select(t => t.LearningResourceId!.Value)
            .ToListAsync(ct);

        var completedResourceIds = await _dbContext.UserInteractions
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.InteractionType == EducationalCompanion.Domain.Enums.InteractionType.Completed)
            .Select(i => i.LearningResourceId)
            .Distinct()
            .ToListAsync(ct);

        var candidates = await _dbContext.Recommendations
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.CreatedAtUtc)
            .Select(r => r.LearningResourceId)
            .Distinct()
            .ToListAsync(ct);

        var excluded = openLinkedResourceIds.Concat(completedResourceIds).ToHashSet();
        var refillIds = candidates
            .Where(id => !excluded.Contains(id))
            .Take(AutoTaskPipelineTarget - openLinkedCount)
            .ToList();

        if (refillIds.Count == 0)
            return;

        await EnsurePendingTasksForRecommendationsAsync(userId, refillIds, ct);
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

