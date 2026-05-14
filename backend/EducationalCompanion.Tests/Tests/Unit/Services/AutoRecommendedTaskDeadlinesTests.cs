using EducationalCompanion.Api.Services;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public sealed class AutoRecommendedTaskDeadlinesTests
{
    private static readonly DateTime UtcNoon = new(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndOfCalendarDayAfterTomorrow_utc_zone_matches_end_of_tomorrow_utc_calendar()
    {
        var end = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, null, 0);
        var expected = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        Assert.Equal(expected, end);
    }

    [Fact]
    public void Compute_with_invalid_daily_minutes_and_utc_calendar_falls_back_to_sequential_add_days()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 0, [30, 30], null).ToList();
        Assert.Equal(2, deadlines.Count);
        Assert.Equal(UtcNoon.AddDays(1), deadlines[0]);
        Assert.Equal(UtcNoon.AddDays(2), deadlines[1]);
    }

    [Fact]
    public void Compute_with_invalid_daily_minutes_uses_zoned_end_of_day_when_time_zone_set()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 0, [30, 30], tz).ToList();
        var e0 = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, tz, 0);
        var e1 = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, tz, 1);
        Assert.Equal(e0, deadlines[0]);
        Assert.Equal(e1, deadlines[1]);
    }

    [Fact]
    public void Compute_packs_tasks_into_days_by_daily_cap_utc_calendar()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 60, [40, 40, 30], null).ToList();
        var d0 = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, null, 0);
        var d1 = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, null, 1);
        var d2 = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, null, 2);
        Assert.Equal(d0, deadlines[0]);
        Assert.Equal(d1, deadlines[1]);
        Assert.Equal(d2, deadlines[2]);
    }

    [Fact]
    public void Compute_splits_single_task_that_exceeds_daily_cap_across_span_utc_calendar()
    {
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 60, [130], null).ToList();
        var expected = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, null, 2);
        Assert.Single(deadlines);
        Assert.Equal(expected, deadlines[0]);
    }

    [Fact]
    public void Compute_single_task_aligns_with_bucharest_calendar_day()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
        var deadlines = AutoRecommendedTaskDeadlines.ComputeDeadlinesUtc(UtcNoon, 60, [30], tz).ToList();
        var expected = AutoRecommendedTaskDeadlines.EndOfCalendarDayAfterTomorrow(UtcNoon, tz, 0);
        Assert.Single(deadlines);
        Assert.Equal(expected, deadlines[0]);
    }
}
