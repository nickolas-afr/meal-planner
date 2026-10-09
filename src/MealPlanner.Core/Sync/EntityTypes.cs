using System.Text.Json;
using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Sync;

/// <summary>The type names used in sync changes and their records.</summary>
public static class EntityTypes
{
    public const string Market = nameof(Entities.Market);
    public const string Ingredient = nameof(Entities.Ingredient);
    public const string Meal = nameof(Entities.Meal);
    public const string PlannedMeal = nameof(Entities.PlannedMeal);
    public const string ShoppingList = nameof(Entities.ShoppingList);
    public const string ShoppingItem = nameof(Entities.ShoppingItem);

    public static readonly IReadOnlyDictionary<string, Type> All = new Dictionary<string, Type>
    {
        [Market] = typeof(Market),
        [Ingredient] = typeof(Ingredient),
        [Meal] = typeof(Meal),
        [PlannedMeal] = typeof(PlannedMeal),
        [ShoppingList] = typeof(ShoppingList),
        [ShoppingItem] = typeof(ShoppingItem),
    };

    public static bool IsKnown(string type) => All.ContainsKey(type);

    public static string NameOf(Entity entity) => entity.GetType().Name;

    public static string Serialize(Entity entity) =>
        JsonSerializer.Serialize(entity, entity.GetType(), CoreJson.Options);

    /// <summary>Returns null when the type is unknown or the JSON doesn't fit the record.</summary>
    public static Entity? Deserialize(string type, string json)
    {
        if (!All.TryGetValue(type, out var clrType))
            return null;
        try
        {
            return JsonSerializer.Deserialize(json, clrType, CoreJson.Options) as Entity;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
