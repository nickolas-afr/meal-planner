namespace MealPlanner.App.Services;

/// <summary>The "Features" section of wwwroot/appsettings.json.</summary>
public sealed class Features
{
    /// <summary>False until M5: no accounts, NavGuard lets everything through and nothing syncs.</summary>
    public bool RequireAuth { get; set; }
}
