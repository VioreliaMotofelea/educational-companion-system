namespace EducationalCompanion.Infrastructure.Edm;

// Raw EDM analytics KPIs from the database (Infrastructure layer only).
public record UserAnalyticsKpisData(
    int TotalResourcesViewed,
    int TotalResourcesCompleted,
    double CompletionRatePercent,
    double? AverageRatingGiven,
    int TotalTimeSpentMinutes,
    int TotalXpEarned,
    int CurrentLevel,
    int TasksCompleted,
    int TasksPending,
    int TasksOverdue,
    int GamificationEventsCount
);
