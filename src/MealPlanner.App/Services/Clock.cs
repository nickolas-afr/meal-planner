namespace MealPlanner.App.Services;

/// <summary>Wraps the system time so it can be fixed in tests.</summary>
public class Clock
{
    public virtual DateTime UtcNow => DateTime.UtcNow;
    public virtual DateTime LocalNow => DateTime.Now;
}
