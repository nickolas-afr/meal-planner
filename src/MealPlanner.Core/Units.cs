namespace MealPlanner.Core;

public static class Units
{
    // "" = no unit
    public static readonly IReadOnlyList<string> All =
        ["", "g", "kg", "ml", "l", "pc", "clove", "tsp", "tbsp", "cup", "tin", "pack", "bunch", "slice"];

    public static bool IsValid(string? unit) => unit is not null && All.Contains(unit);
}
