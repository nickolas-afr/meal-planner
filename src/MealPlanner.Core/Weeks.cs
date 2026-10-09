namespace MealPlanner.Core;

public static class Weeks
{
    /// <summary>The Monday on or before <paramref name="date"/>.</summary>
    public static DateOnly StartOf(DateOnly date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    /// <summary>The current week Monday to Thursday, the next week Friday to Sunday.</summary>
    public static DateOnly Active(DateTime localNow)
    {
        var today = DateOnly.FromDateTime(localNow);
        var start = StartOf(today);
        return today.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday
            ? start.AddDays(7)
            : start;
    }

    /// <summary>Weeks starting before this are pruned.</summary>
    public static DateOnly RetentionCutoff(DateTime localNow) =>
        StartOf(DateOnly.FromDateTime(localNow)).AddDays(-14);
}
