using EducationalCompanion.Api.Services;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public class StudyDayScheduleBuilderTests
{
    [Fact]
    public void Build_InsertsBreakBetweenStudyBlocks()
    {
        var tasks = new[]
        {
            CreateTask(estimate: 30),
            CreateTask(estimate: 20),
        };

        var result = StudyDayScheduleBuilder.Build(tasks, dailyAvailableMinutes: 60, studyDayStartHourLocal: 9);

        Assert.Equal(3, result.Blocks.Count);
        Assert.Equal(StudyScheduleBlockKind.Study, result.Blocks[0].Kind);
        Assert.Equal(StudyScheduleBlockKind.Break, result.Blocks[1].Kind);
        Assert.Equal(StudyScheduleBlockKind.Study, result.Blocks[2].Kind);
        Assert.Equal("09:00", result.Blocks[0].StartTimeLocal);
        Assert.Equal("09:30", result.Blocks[0].EndTimeLocal);
        Assert.Equal("09:30", result.Blocks[1].StartTimeLocal);
        Assert.Equal("09:40", result.Blocks[1].EndTimeLocal);
        Assert.Equal(50, result.PlannedStudyMinutes);
        Assert.Equal(10, result.PlannedBreakMinutes);
    }

    [Fact]
    public void Build_SkipsTaskThatDoesNotFitWholeWindow()
    {
        var tasks = new[]
        {
            CreateTask(estimate: 50),
            CreateTask(estimate: 15),
        };

        var result = StudyDayScheduleBuilder.Build(tasks, dailyAvailableMinutes: 60, studyDayStartHourLocal: 9);

        Assert.Single(result.Blocks);
        Assert.Equal(50, result.Blocks[0].DurationMinutes);
    }

    [Fact]
    public void Build_ReturnsEmptySummaryWhenNoTasks()
    {
        var result = StudyDayScheduleBuilder.Build([], dailyAvailableMinutes: 60, studyDayStartHourLocal: 9);
        Assert.Empty(result.Blocks);
        Assert.Contains("No open tasks", result.Summary);
    }

    private static StudyScheduleTaskInput CreateTask(int estimate) =>
        new()
        {
            TaskId = Guid.NewGuid(),
            Title = "Study task",
            Status = "Pending",
            DeadlineUtc = DateTime.UtcNow.AddDays(1),
            EstimatedMinutes = estimate,
            Priority = 3,
        };
}
