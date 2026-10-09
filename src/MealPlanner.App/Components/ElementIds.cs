namespace MealPlanner.App.Components;

/// <summary>Unique DOM ids, e.g. to point an input's aria-describedby at its FieldError.</summary>
public static class ElementIds
{
    private static int _next;

    public static string New(string prefix = "el") => $"{prefix}-{Interlocked.Increment(ref _next)}";
}
