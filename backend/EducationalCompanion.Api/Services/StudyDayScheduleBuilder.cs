namespace EducationalCompanion.Api.Services;

public enum StudyScheduleBlockKind
{
    Study,
    Break
}

public sealed class StudyScheduleTaskInput
{
    public Guid TaskId { get; init; }
    public Guid? LearningResourceId { get; init; }
    public string Title { get; init; } = null!;
    public string? ResourceTitle { get; init; }
    public string? Topic { get; init; }
    public string? ContentType { get; init; }
    public int? Difficulty { get; init; }
    public string Status { get; init; } = null!;
    public DateTime DeadlineUtc { get; init; }
    public int EstimatedMinutes { get; init; }
    public int Priority { get; init; }
    public double? RecommendationScore { get; init; }
    public string? RecommendationExplanation { get; init; }
    public string? Description { get; init; }
    public string? SourceName { get; init; }
    public string? Url { get; init; }
    public string? AccessInstructions { get; init; }
}

public sealed class StudyScheduleBuiltBlock
{
    public int Order { get; init; }
    public StudyScheduleBlockKind Kind { get; init; }
    public string StartTimeLocal { get; init; } = null!;
    public string EndTimeLocal { get; init; } = null!;
    public int DurationMinutes { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? LearningResourceId { get; init; }
    public string? Title { get; init; }
    public string? Topic { get; init; }
    public string? ContentType { get; init; }
    public int? Difficulty { get; init; }
    public string? TaskStatus { get; init; }
    public DateTime? DeadlineUtc { get; init; }
    public double? RecommendationScore { get; init; }
    public string? Explanation { get; init; }
    public string? Label { get; init; }
    public string? Description { get; init; }
    public string? SourceName { get; init; }
    public string? Url { get; init; }
    public string? AccessInstructions { get; init; }
}

public sealed class StudyDayScheduleBuildResult
{
    public int PlannedStudyMinutes { get; init; }
    public int PlannedBreakMinutes { get; init; }
    public int UnscheduledOpenTaskCount { get; init; }
    public IReadOnlyList<StudyScheduleBuiltBlock> Blocks { get; init; } = [];
    public string Summary { get; init; } = "";
}

public static class StudyDayScheduleBuilder
{
    public static int BreakMinutesAfterStudy(int studyMinutes) =>
        studyMinutes switch
        {
            <= 25 => 5,
            <= 45 => 10,
            _ => 15
        };

    public static string BreakLabel(int breakMinutes) =>
        breakMinutes switch
        {
            <= 5 => "Short break",
            <= 10 => "Rest break",
            _ => "Longer rest"
        };

    public static StudyDayScheduleBuildResult Build(
        IReadOnlyList<StudyScheduleTaskInput> orderedTasks,
        int dailyAvailableMinutes,
        int studyDayStartHourLocal)
    {
        if (dailyAvailableMinutes < 1 || orderedTasks.Count == 0)
        {
            return new StudyDayScheduleBuildResult
            {
                Summary = orderedTasks.Count == 0
                    ? "No open tasks to schedule."
                    : "Set a daily study budget to build today's plan."
            };
        }

        var blocks = new List<StudyScheduleBuiltBlock>();
        var remaining = dailyAvailableMinutes;
        var cursorMinutes = Math.Clamp(studyDayStartHourLocal, 0, 23) * 60;
        var plannedStudy = 0;
        var plannedBreaks = 0;
        var order = 1;
        var scheduledTaskIds = new HashSet<Guid>();

        for (var i = 0; i < orderedTasks.Count; i++)
        {
            if (remaining <= 0)
                break;

            var task = orderedTasks[i];
            var studyMinutes = Math.Max(1, task.EstimatedMinutes);
            var isLastCandidate = i == orderedTasks.Count - 1;
            var breakMinutes = isLastCandidate ? 0 : BreakMinutesAfterStudy(studyMinutes);

            if (studyMinutes > remaining)
                continue;

            if (!isLastCandidate && studyMinutes + breakMinutes > remaining)
            {
                if (studyMinutes <= remaining)
                {
                    AddStudyBlock(blocks, ref order, ref cursorMinutes, task, studyMinutes);
                    scheduledTaskIds.Add(task.TaskId);
                    plannedStudy += studyMinutes;
                    remaining -= studyMinutes;
                }
                break;
            }

            AddStudyBlock(blocks, ref order, ref cursorMinutes, task, studyMinutes);
            scheduledTaskIds.Add(task.TaskId);
            plannedStudy += studyMinutes;
            remaining -= studyMinutes;

            if (breakMinutes > 0 && breakMinutes <= remaining)
            {
                AddBreakBlock(blocks, ref order, ref cursorMinutes, breakMinutes);
                plannedBreaks += breakMinutes;
                remaining -= breakMinutes;
            }
        }

        var studyCount = blocks.Count(b => b.Kind == StudyScheduleBlockKind.Study);
        var breakCount = blocks.Count(b => b.Kind == StudyScheduleBlockKind.Break);
        var unscheduledCount = orderedTasks.Count(t => !scheduledTaskIds.Contains(t.TaskId));
        var summary = studyCount == 0
            ? unscheduledCount > 0
                ? $"No tasks fit in today's {dailyAvailableMinutes}-minute window. {unscheduledCount} open task{(unscheduledCount == 1 ? "" : "s")} remain — try a longer daily budget in Profile."
                : "No tasks fit in today's study window. Try a longer daily budget or shorter tasks."
            : breakCount == 0
                ? AppendUnscheduledHint(
                    $"{studyCount} study session{(studyCount == 1 ? "" : "s")} within your {dailyAvailableMinutes}-minute plan.",
                    unscheduledCount)
                : AppendUnscheduledHint(
                    $"{studyCount} study session{(studyCount == 1 ? "" : "s")} and {breakCount} break{(breakCount == 1 ? "" : "s")} within your {dailyAvailableMinutes}-minute plan.",
                    unscheduledCount);

        return new StudyDayScheduleBuildResult
        {
            PlannedStudyMinutes = plannedStudy,
            PlannedBreakMinutes = plannedBreaks,
            UnscheduledOpenTaskCount = unscheduledCount,
            Blocks = blocks,
            Summary = summary
        };
    }

    private static string AppendUnscheduledHint(string baseSummary, int unscheduledCount) =>
        unscheduledCount > 0
            ? $"{baseSummary} {unscheduledCount} more open task{(unscheduledCount == 1 ? "" : "s")} continue in a future session."
            : baseSummary;

    private static void AddStudyBlock(
        List<StudyScheduleBuiltBlock> blocks,
        ref int order,
        ref int cursorMinutes,
        StudyScheduleTaskInput task,
        int durationMinutes)
    {
        var start = cursorMinutes;
        var end = start + durationMinutes;
        blocks.Add(new StudyScheduleBuiltBlock
        {
            Order = order++,
            Kind = StudyScheduleBlockKind.Study,
            StartTimeLocal = FormatMinutes(start),
            EndTimeLocal = FormatMinutes(end),
            DurationMinutes = durationMinutes,
            TaskId = task.TaskId,
            LearningResourceId = task.LearningResourceId,
            Title = string.IsNullOrWhiteSpace(task.ResourceTitle) ? task.Title : task.ResourceTitle,
            Topic = task.Topic,
            ContentType = task.ContentType,
            Difficulty = task.Difficulty,
            TaskStatus = task.Status,
            DeadlineUtc = task.DeadlineUtc,
            RecommendationScore = task.RecommendationScore,
            Explanation = task.RecommendationExplanation,
            Description = task.Description,
            SourceName = task.SourceName,
            Url = task.Url,
            AccessInstructions = task.AccessInstructions
        });
        cursorMinutes = end;
    }

    private static void AddBreakBlock(
        List<StudyScheduleBuiltBlock> blocks,
        ref int order,
        ref int cursorMinutes,
        int durationMinutes)
    {
        var start = cursorMinutes;
        var end = start + durationMinutes;
        blocks.Add(new StudyScheduleBuiltBlock
        {
            Order = order++,
            Kind = StudyScheduleBlockKind.Break,
            StartTimeLocal = FormatMinutes(start),
            EndTimeLocal = FormatMinutes(end),
            DurationMinutes = durationMinutes,
            Label = BreakLabel(durationMinutes)
        });
        cursorMinutes = end;
    }

    private static string FormatMinutes(int minutesFromMidnight)
    {
        var hours = minutesFromMidnight / 60;
        var minutes = minutesFromMidnight % 60;
        return $"{hours:00}:{minutes:00}";
    }
}
