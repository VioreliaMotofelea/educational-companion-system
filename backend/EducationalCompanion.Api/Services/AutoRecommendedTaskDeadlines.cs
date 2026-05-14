namespace EducationalCompanion.Api.Services;

public static class AutoRecommendedTaskDeadlines
{
    /// End of the UTC calendar day that is <paramref name="dayOffsetFromTomorrow"/> days after tomorrow
    /// (offset 0 = end of tomorrow, 1 = end of the following day).
    public static DateTime EndOfUtcCalendarDayAfterTomorrow(DateTime utcNow, int dayOffsetFromTomorrow)
    {
        var startOfTargetDay = utcNow.Date.AddDays(2 + dayOffsetFromTomorrow);
        return startOfTargetDay.AddTicks(-1);
    }

    /// <param name="utcNow">Current instant (UTC).</param>
    /// <param name="dailyAvailableMinutes">Learner budget; if &lt; 1, uses legacy sequential deadlines.</param>
    /// <param name="estimatedMinutesPerTaskInOrder">One estimate per task, same order as creation.</param>
    public static IReadOnlyList<DateTime> ComputeDeadlinesUtc(
        DateTime utcNow,
        int dailyAvailableMinutes,
        IReadOnlyList<int> estimatedMinutesPerTaskInOrder)
    {
        if (estimatedMinutesPerTaskInOrder.Count == 0)
            return [];

        if (dailyAvailableMinutes < 1)
        {
            return Enumerable.Range(0, estimatedMinutesPerTaskInOrder.Count)
                .Select(i => utcNow.AddDays(1 + i))
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
                deadlines.Add(EndOfUtcCalendarDayAfterTomorrow(utcNow, dayOffsetFromTomorrow + spanDays - 1));
                dayOffsetFromTomorrow += spanDays;
                usedOnDay = 0;
                continue;
            }

            while (usedOnDay + est > dailyCap)
            {
                dayOffsetFromTomorrow++;
                usedOnDay = 0;
            }

            deadlines.Add(EndOfUtcCalendarDayAfterTomorrow(utcNow, dayOffsetFromTomorrow));
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
