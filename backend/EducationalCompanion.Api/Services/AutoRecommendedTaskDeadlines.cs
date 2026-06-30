namespace EducationalCompanion.Api.Services;

public static class AutoRecommendedTaskDeadlines
{
    private static DateTime EndOfUtcCalendarDayAfterTomorrow(DateTime utcNow, int dayOffsetFromTomorrow)
    {
        var startOfTargetDay = utcNow.Date.AddDays(2 + dayOffsetFromTomorrow);
        return startOfTargetDay.AddTicks(-1);
    }

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

    public static IReadOnlyList<DateTime> ComputeDeadlinesUtc(
        DateTime utcNow,
        int dailyAvailableMinutes,
        IReadOnlyList<int> estimatedMinutesPerTaskInOrder,
        TimeZoneInfo? deadlineTimeZone = null,
        int minimumPlanningDays = 7)
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

        if (deadlines.Count <= 1 || minimumPlanningDays <= 1)
            return deadlines;

        var stretched = new List<DateTime>(deadlines.Count);
        var maxIndex = deadlines.Count - 1;
        for (var i = 0; i < deadlines.Count; i++)
        {
            var stretchedOffset = (int)Math.Round(i * (minimumPlanningDays - 1d) / maxIndex);
            var stretchedDeadline = EndOfCalendarDayAfterTomorrow(utcNow, deadlineTimeZone, stretchedOffset);
            stretched.Add(deadlines[i] > stretchedDeadline ? deadlines[i] : stretchedDeadline);
        }

        return stretched;
    }
}
