using System.Globalization;
using System.Text.Json;
using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Backup;

/// <summary>The Settings › Export file: every non-deleted entity and a schema version.</summary>
public sealed record BackupFile
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTime ExportedAt { get; init; }
    public IReadOnlyList<Market> Markets { get; init; } = [];
    public IReadOnlyList<Ingredient> Ingredients { get; init; } = [];
    public IReadOnlyList<Meal> Meals { get; init; } = [];
    public IReadOnlyList<PlannedMeal> PlannedMeals { get; init; } = [];
    public IReadOnlyList<ShoppingList> ShoppingLists { get; init; } = [];
    public IReadOnlyList<ShoppingItem> ShoppingItems { get; init; } = [];

    public static BackupFile Create(
        DateTime exportedAt,
        IEnumerable<Market> markets,
        IEnumerable<Ingredient> ingredients,
        IEnumerable<Meal> meals,
        IEnumerable<PlannedMeal> plannedMeals,
        IEnumerable<ShoppingList> shoppingLists,
        IEnumerable<ShoppingItem> shoppingItems) => new()
    {
        ExportedAt = exportedAt,
        Markets = Live(markets),
        Ingredients = Live(ingredients),
        Meals = Live(meals),
        PlannedMeals = Live(plannedMeals),
        ShoppingLists = Live(shoppingLists),
        ShoppingItems = Live(shoppingItems),
    };

    /// <summary>"meal-planner-backup-YYYY-MM-DD.json"</summary>
    public static string FileName(DateOnly date) =>
        $"meal-planner-backup-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";

    public string Serialize() => JsonSerializer.Serialize(this, CoreJson.Options);

    /// <summary>Returns null when the text isn't a backup this app can read.</summary>
    public static BackupFile? Deserialize(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<BackupFile>(json, CoreJson.Options);
            return file is { SchemaVersion: >= 1 and <= CurrentSchemaVersion } ? file : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<T> Live<T>(IEnumerable<T> entities) where T : Entity =>
        entities.Where(e => !e.IsDeleted).ToList();
}
