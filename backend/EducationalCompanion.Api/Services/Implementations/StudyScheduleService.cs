using EducationalCompanion.Api.Dtos.Schedule;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DomainTaskStatus = EducationalCompanion.Domain.Enums.TaskStatus;

namespace EducationalCompanion.Api.Services.Implementations;

public class StudyScheduleService : IStudyScheduleService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly IOptions<CalendarOptions> _calendarOptions;

    public StudyScheduleService(
        ApplicationDbContext dbContext,
        IUserProfileRepository userProfileRepo,
        IOptions<CalendarOptions> calendarOptions)
    {
        _dbContext = dbContext;
        _userProfileRepo = userProfileRepo;
        _calendarOptions = calendarOptions;
    }

    public async Task<StudyDayScheduleResponse> GetTodayScheduleAsync(
        string userId,
        DateOnly? date = null,
        CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);

        var timeZone = ResolveTimeZone();
        var timeZoneId = timeZone?.Id ?? "UTC";
        var scheduleDate = date ?? TodayInTimeZone(timeZone);
        var dailyMinutes = (await _userProfileRepo.GetByUserIdAsync(userId, ct))?.DailyAvailableMinutes ?? 60;
        var startHour = Math.Clamp(_calendarOptions.Value.StudyDayStartHourLocal, 0, 23);

        var openTasks = await LoadOpenTasksAsync(userId, ct);
        var recommendationByResource = await LoadRecommendationMapAsync(userId, ct);
        var ordered = OrderTasksForSchedule(openTasks, recommendationByResource);
        var inputs = ordered.Select(t => ToScheduleInput(t, recommendationByResource)).ToList();

        var built = StudyDayScheduleBuilder.Build(inputs, dailyMinutes, startHour);
        var blocks = built.Blocks
            .Select(MapBlock)
            .ToList();

        return new StudyDayScheduleResponse(
            userId,
            scheduleDate.ToString("yyyy-MM-dd"),
            timeZoneId,
            dailyMinutes,
            built.PlannedStudyMinutes,
            built.PlannedBreakMinutes,
            built.UnscheduledOpenTaskCount,
            built.Summary,
            blocks
        );
    }

    private async Task<List<Domain.Entities.StudyTask>> LoadOpenTasksAsync(string userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stalePending = await _dbContext.StudyTasks
            .Where(t => t.UserId == userId && t.Status == DomainTaskStatus.Pending && t.DeadlineUtc < now)
            .ToListAsync(ct);
        if (stalePending.Count > 0)
        {
            foreach (var task in stalePending)
                task.Status = DomainTaskStatus.Overdue;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.StudyTasks
            .AsNoTracking()
            .Include(t => t.LearningResource)
            .Where(t => t.UserId == userId && (t.Status == DomainTaskStatus.Pending || t.Status == DomainTaskStatus.Overdue))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, Domain.Entities.Recommendation>> LoadRecommendationMapAsync(
        string userId,
        CancellationToken ct)
    {
        var recommendations = await _dbContext.Recommendations
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);

        return recommendations
            .GroupBy(r => r.LearningResourceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).First());
    }

    private static List<Domain.Entities.StudyTask> OrderTasksForSchedule(
        IReadOnlyList<Domain.Entities.StudyTask> tasks,
        IReadOnlyDictionary<Guid, Domain.Entities.Recommendation> recommendationByResource)
    {
        return tasks
            .OrderBy(t => t.Status == DomainTaskStatus.Overdue ? 0 : 1)
            .ThenBy(t => t.DeadlineUtc)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t =>
                t.LearningResourceId.HasValue && recommendationByResource.TryGetValue(t.LearningResourceId.Value, out var rec)
                    ? rec.Score
                    : 0.0)
            .ToList();
    }

    private static StudyScheduleTaskInput ToScheduleInput(
        Domain.Entities.StudyTask task,
        IReadOnlyDictionary<Guid, Domain.Entities.Recommendation> recommendationByResource)
    {
        double? score = null;
        string? explanation = null;
        if (task.LearningResourceId.HasValue &&
            recommendationByResource.TryGetValue(task.LearningResourceId.Value, out var rec))
        {
            score = rec.Score;
            explanation = rec.Explanation;
        }

        var resource = task.LearningResource;
        return new StudyScheduleTaskInput
        {
            TaskId = task.Id,
            LearningResourceId = task.LearningResourceId,
            Title = task.Title,
            ResourceTitle = resource?.Title,
            Topic = resource?.Topic,
            ContentType = resource?.ContentType.ToString(),
            Difficulty = resource?.Difficulty,
            Status = task.Status.ToString(),
            DeadlineUtc = task.DeadlineUtc,
            EstimatedMinutes = task.EstimatedMinutes,
            Priority = task.Priority,
            RecommendationScore = score,
            RecommendationExplanation = explanation,
            Description = resource?.Description,
            SourceName = resource?.SourceName,
            Url = resource?.Url,
            AccessInstructions = resource?.AccessInstructions
        };
    }

    private static StudyScheduleBlockResponse MapBlock(StudyScheduleBuiltBlock block) =>
        new(
            block.Order,
            block.Kind == StudyScheduleBlockKind.Study ? "Study" : "Break",
            block.StartTimeLocal,
            block.EndTimeLocal,
            block.DurationMinutes,
            block.TaskId,
            block.LearningResourceId,
            block.Title,
            block.Topic,
            block.ContentType,
            block.Difficulty,
            block.TaskStatus,
            block.DeadlineUtc,
            block.RecommendationScore,
            block.Explanation,
            block.Label,
            block.Description,
            block.SourceName,
            block.Url,
            block.AccessInstructions
        );

    private static DateOnly TodayInTimeZone(TimeZoneInfo? timeZone)
    {
        var utcNow = DateTime.UtcNow;
        if (timeZone is null)
            return DateOnly.FromDateTime(utcNow);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        return DateOnly.FromDateTime(local);
    }

    private TimeZoneInfo? ResolveTimeZone()
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
}
