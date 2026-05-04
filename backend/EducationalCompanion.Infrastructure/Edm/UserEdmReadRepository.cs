using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Infrastructure.Persistence;
using TaskStatus = EducationalCompanion.Domain.Enums.TaskStatus;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Edm;

public class UserEdmReadRepository : IUserEdmReadRepository
{
    private readonly ApplicationDbContext _context;

    public UserEdmReadRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserAnalyticsKpisData?> GetUserAnalyticsKpisAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
            return null;

        var viewed = await _context.UserInteractions
            .AsNoTracking()
            .CountAsync(i => i.UserId == userId && i.InteractionType == InteractionType.Viewed, ct);
        var completed = await _context.UserInteractions
            .AsNoTracking()
            .CountAsync(i => i.UserId == userId && i.InteractionType == InteractionType.Completed, ct);
        // Compute completion rate on distinct engaged resources to keep metric bounded [0, 100].
        var engagedResourceCount = await _context.UserInteractions
            .AsNoTracking()
            .Where(i =>
                i.UserId == userId &&
                (i.InteractionType == InteractionType.Viewed || i.InteractionType == InteractionType.Completed))
            .Select(i => i.LearningResourceId)
            .Distinct()
            .CountAsync(ct);
        var completedResourceCount = await _context.UserInteractions
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.InteractionType == InteractionType.Completed)
            .Select(i => i.LearningResourceId)
            .Distinct()
            .CountAsync(ct);
        var completionRate = engagedResourceCount > 0
            ? (double)completedResourceCount / engagedResourceCount * 100.0
            : 0.0;

        var avgRating = await _context.UserInteractions
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.Rating != null)
            .AverageAsync(i => (double?)i.Rating!.Value, ct);

        var totalTime = await _context.UserInteractions
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.TimeSpentMinutes != null)
            .SumAsync(i => i.TimeSpentMinutes ?? 0, ct);

        var tasksCompleted = await _context.StudyTasks
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.Status == TaskStatus.Completed, ct);
        var tasksPending = await _context.StudyTasks
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.Status == TaskStatus.Pending, ct);
        var tasksOverdue = await _context.StudyTasks
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.Status == TaskStatus.Overdue, ct);

        var gamificationCount = await _context.GamificationEvents
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId, ct);

        return new UserAnalyticsKpisData(
            TotalResourcesViewed: viewed,
            TotalResourcesCompleted: completed,
            CompletionRatePercent: Math.Round(completionRate, 2),
            AverageRatingGiven: avgRating.HasValue ? Math.Round(avgRating.Value, 2) : null,
            TotalTimeSpentMinutes: totalTime,
            TotalXpEarned: profile.Xp,
            CurrentLevel: profile.Level,
            TasksCompleted: tasksCompleted,
            TasksPending: tasksPending,
            TasksOverdue: tasksOverdue,
            GamificationEventsCount: gamificationCount
        );
    }

    public async Task<IReadOnlyList<TopicMasteryData>> GetTopicMasteryDataAsync(string userId, CancellationToken ct = default)
    {
        var completedWithResource = await _context.UserInteractions
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.InteractionType == InteractionType.Completed && i.LearningResource != null)
            .Select(i => new { i.LearningResource!.Topic, i.LearningResource!.Difficulty, i.Rating })
            .ToListAsync(ct);

        var byTopic = completedWithResource
            .GroupBy(x => x.Topic)
            .Select(g =>
            {
                var ratings = g.Where(x => x.Rating.HasValue).Select(x => x.Rating!.Value).ToList();
                var avgRating = ratings.Count > 0 ? (double?)Math.Round(ratings.Average(), 2) : null;
                var avgDiff = Math.Round(g.Average(x => x.Difficulty), 2);
                return new TopicMasteryData(
                    Topic: g.Key,
                    ResourcesCompleted: g.Count(),
                    AverageRating: avgRating,
                    AverageDifficultyCompleted: avgDiff
                );
            })
            .OrderByDescending(x => x.ResourcesCompleted)
            .ToList();

        return byTopic;
    }
}
