namespace EducationalCompanion.Api.Services;

/// <summary>
/// Computes deadlines for tasks auto-created from recommendations, using the learner's
/// daily study budget. Calendar-day boundaries follow <paramref name="deadlineTimeZone"/> when set,
/// otherwise UTC (matches empty frontend <c>VITE_CALENDAR_TIMEZONE</c> / local-browser behaviour only on the client).
/// </summary>
public static class AutoRecommendedTaskDeadlines
{
    /// <summary>
    /// End of the UTC calendar day that is <paramref name="dayOffsetFromTomorrow"/> full days after
    /// tomorrow in UTC (offset 0 = end of tomorrow UTC).
    /// </summary>
    private static DateTime EndOfUtcCalendarDayAfterTomorrow(DateTime utcNow, int dayOffsetFromTomorrow)
    {
        var startOfTargetDay = utcNow.Date.AddDays(2 + dayOffsetFromTomorrow);
        return startOfTargetDay.AddTicks(-1);
    }

    /// <summary>
    /// End of the calendar day in <paramref name="deadlineTimeZone"/> that is <paramref name="dayOffsetFromTomorrow"/>
    /// full days after "tomorrow" in that zone (offset 0 = end of tomorrow local). Returned as UTC.
    /// </summary>
    public static DateTime EndOfCalendarDayAfterTomorrow(DateTime utcNow, TimeZoneInfo? deadlineTimeZone, int dayOffsetFromTomorrow)
    {
        if (deadlineTimeZone is null)
            return EndOfUtcCalendarDayAfterTomorrow(utcNow, dayOffsetFromTomorrow);

        var utcInstant = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, deadlineTimeZone);
        var tomorrowLocalMidnight = local.Date.AddDays(1);
        var targetDayStart = tomorrowLocalMidnight.AddDays(dayOffsetFromTomorrow);
        var nextDayStart = targetDayStart.AddDays(1);
        var endLocal = DateTime.SpecifyKind(nextDayStart.AddTicks(-1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(endLocal, deadlineTimeZone);
    }

    /// <param name="utcNow">Current instant (UTC).</param>
    /// <param name="dailyAvailableMinutes">Learner budget; if &lt; 1, uses sequential deadlines (see below).</param>
    /// <param name="estimatedMinutesPerTaskInOrder">One estimate per task, same order as creation.</param>
    /// <param name="deadlineTimeZone">When null, calendar days are UTC; when set, matches frontend fixed IANA zone.</param>
    public static IReadOnlyList<DateTime> ComputeDeadlinesUtc(
        DateTime utcNow,
        int dailyAvailableMinutes,
        IReadOnlyList<int> estimatedMinutesPerTaskInOrder,
        TimeZoneInfo? deadlineTimeZone = null)
    {
        if (estimatedMinutesPerTaskInOrder.Count == 0)
            return [];

        if (dailyAvailableMinutes < 1)
        {
            return Enumerable.Range(0, estimatedMinutesPerTaskInOrder.Count)
                .Select(i =>
                    deadlineTimeZone is null
                        ? utcNow.AddDays(1 + i)
                        : EndOfCalendarDayAfterTomorrow(utcNow, deadlineTimeZone, i))
                .ToList();
        }

        var dailyCap = dailyAvailableMinutes;
        var deadlines = new List<DateTime>(estimatedMinutesPerTaskInOrder.Count);
        var dayOffsetFromTomorrow = 0;
        var usedOnDay = 0;

        foreach (var raw in estimatedMinutesPerTaskInOrder)
        {
            var est = Math.Max(5, raw);
            if (est > dailyCap)
            {
                var spanDays = (est + dailyCap - 1) / dailyCap;
                deadlines.Add(EndOfCalendarDayAfterTomorrow(utcNow, deadlineTimeZone, dayOffsetFromTomorrow + spanDays - 1));
                dayOffsetFromTomorrow += spanDays;
                usedOnDay = 0;
                continue;
            }

            while (usedOnDay + est > dailyCap)
            {
                dayOffsetFromTomorrow++;
                usedOnDay = 0;
            }

            deadlines.Add(EndOfCalendarDayAfterTomorrow(utcNow, deadlineTimeZone, dayOffsetFromTomorrow));
            usedOnDay += est;
            if (usedOnDay >= dailyCap)
            {
                dayOffsetFromTomorrow++;
                usedOnDay = 0;
            }
        }

        return deadlines;
    }
}
