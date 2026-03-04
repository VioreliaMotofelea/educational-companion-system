namespace EducationalCompanion.Api.Dtos.Analytics;

// EDM layer: user analytics summary and KPIs for dashboards and reporting.
public record UserAnalyticsResponse(
    string UserId,
    UserAnalyticsSummary Summary,
    UserAnalyticsKpis Kpis
);

public record UserAnalyticsSummary(
    string? SummaryText,
    DateTime ComputedAtUtc
);

public record UserAnalyticsKpis(
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
