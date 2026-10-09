using System.Globalization;

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

    /// <summary>"This week", "Next week", "Last week" or "Week of Oct 26".</summary>
    public static string Name(DateOnly week, DateTime localNow)
    {
        var current = StartOf(DateOnly.FromDateTime(localNow));
        if (week == current) return "This week";
        if (week == current.AddDays(7)) return "Next week";
        if (week == current.AddDays(-7)) return "Last week";
        return "Week of " + week.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    /// <summary>"Mon Oct 12 – Sun Oct 18".</summary>
    public static string RangeLabel(DateOnly week) =>
        week.ToString("ddd MMM d", CultureInfo.InvariantCulture) + " \u2013 " +
        week.AddDays(6).ToString("ddd MMM d", CultureInfo.InvariantCulture);

    /// <summary>Earliest week the Plan screen can navigate to: 2 weeks before the current week.</summary>
    public static DateOnly PlanMin(DateTime localNow) =>
        StartOf(DateOnly.FromDateTime(localNow)).AddDays(-14);

    /// <summary>Latest week the Plan screen can navigate to: 4 weeks after the current week.</summary>
    public static DateOnly PlanMax(DateTime localNow) =>
        StartOf(DateOnly.FromDateTime(localNow)).AddDays(28);
}
