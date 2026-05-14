using EducationalCompanion.Api.Services;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public sealed class AutoRecommendedTaskDeadlinesTests
{
    private static readonly DateTime UtcNoon = new(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndOfUtcCalendarDayAfterTomorrow_offset_zero_is_end_of_tomorrow()
    {
        var end = AutoRecommendedTaskDeadlines.EndOfUtcCalendarDayAfterTomorrow(UtcNoon, 0);
        var expected = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        Assert.Equal(expected, end);
    }

    [Fact]
    public void Compute_with_invalid_daily_minutes_falls_back_to_sequential_add_days()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 0, [30, 30]).ToList();
        Assert.Equal(2, deadlines.Count);
        Assert.Equal(UtcNoon.AddDays(1), deadlines[0]);
        Assert.Equal(UtcNoon.AddDays(2), deadlines[1]);
    }

    [Fact]
    public void Compute_packs_tasks_into_days_by_daily_cap()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 60, [40, 40, 30]).ToList();
        var d0 = AutoRecommendedTaskDeadlines.EndOfUtcCalendarDayAfterTomorrow(UtcNoon, 0);
        var d1 = AutoRecommendedTaskDeadlines.EndOfUtcCalendarDayAfterTomorrow(UtcNoon, 1);
        var d2 = AutoRecommendedTaskDeadlines.EndOfUtcCalendarDayAfterTomorrow(UtcNoon, 2);
        Assert.Equal(d0, deadlines[0]);
        Assert.Equal(d1, deadlines[1]);
        Assert.Equal(d2, deadlines[2]);
    }

    [Fact]
    public void Compute_splits_single_task_that_exceeds_daily_cap_across_span()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 60, [130]).ToList();
        var expected = AutoRecommendedTaskDeadlines.EndOfUtcCalendarDayAfterTomorrow(UtcNoon, 2);
        Assert.Single(deadlines);
        Assert.Equal(expected, deadlines[0]);
    }
}
