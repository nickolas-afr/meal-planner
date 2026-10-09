using MealPlanner.Core.Entities;

namespace MealPlanner.App.Storage;

/// <summary>Everything the app stores on the phone, saved as one JSON value.</summary>
public sealed class AppData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public Dictionary<Guid, Market> Markets { get; set; } = [];
    public Dictionary<Guid, Ingredient> Ingredients { get; set; } = [];
    public Dictionary<Guid, Meal> Meals { get; set; } = [];
    public Dictionary<Guid, PlannedMeal> PlannedMeals { get; set; } = [];
    public Dictionary<Guid, ShoppingList> ShoppingLists { get; set; } = [];
    public Dictionary<Guid, ShoppingItem> ShoppingItems { get; set; } = [];

    /// <summary>Entities changed locally and not yet sent, keyed by id.</summary>
    public Dictionary<Guid, OutboxEntry> Outbox { get; set; } = [];
    public long NextOutboxSeq { get; set; } = 1;

    /// <summary>The last server version seen.</summary>
    public long LastVersion { get; set; }
}

public sealed record OutboxEntry(string Type, long Seq);
