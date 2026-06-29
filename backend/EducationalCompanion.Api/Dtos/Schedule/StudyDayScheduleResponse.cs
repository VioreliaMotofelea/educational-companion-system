namespace EducationalCompanion.Api.Dtos.Schedule;

public record StudyDayScheduleResponse(
    string UserId,
    string ScheduleDate,
    string TimeZoneId,
    int DailyAvailableMinutes,
    int PlannedStudyMinutes,
    int PlannedBreakMinutes,
    int UnscheduledOpenTaskCount,
    string Summary,
    IReadOnlyList<StudyScheduleBlockResponse> Blocks
);

public record StudyScheduleBlockResponse(
    int Order,
    string Type,
    string StartTimeLocal,
    string EndTimeLocal,
    int DurationMinutes,
    Guid? TaskId,
    Guid? LearningResourceId,
    string? Title,
    string? Topic,
    string? ContentType,
    int? Difficulty,
    string? TaskStatus,
    DateTime? DeadlineUtc,
    double? RecommendationScore,
    string? Explanation,
    string? Label,
    string? Description,
    string? SourceName,
    string? Url,
    string? AccessInstructions
);
