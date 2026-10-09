using MealPlanner.App.Storage;
using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

/// <summary>The in-memory data and derived lookups. Pages read it and subscribe to <see cref="OnChange"/>.</summary>
public sealed class AppState
{
    public AppData Data { get; private set; } = new();

    public event Action? OnChange;

    // Live (non-deleted) entities.
    public IEnumerable<Market> Markets => Data.Markets.Values.Where(m => !m.IsDeleted).OrderBy(m => m.SortOrder).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
    public IEnumerable<Ingredient> Ingredients => Data.Ingredients.Values.Where(i => !i.IsDeleted);
    public IEnumerable<Meal> Meals => Data.Meals.Values.Where(m => !m.IsDeleted);
    public IEnumerable<PlannedMeal> PlannedMeals => Data.PlannedMeals.Values.Where(p => !p.IsDeleted);
    public IEnumerable<ShoppingList> ShoppingLists => Data.ShoppingLists.Values.Where(l => !l.IsDeleted);
    public IEnumerable<ShoppingItem> ShoppingItems => Data.ShoppingItems.Values.Where(i => !i.IsDeleted);

    public Market? Market(Guid id) => Data.Markets.GetValueOrDefault(id) is { IsDeleted: false } m ? m : null;
    public Ingredient? Ingredient(Guid id) => Data.Ingredients.GetValueOrDefault(id) is { IsDeleted: false } i ? i : null;
    public Meal? Meal(Guid id) => Data.Meals.GetValueOrDefault(id) is { IsDeleted: false } m ? m : null;

    public void Load(AppData data) => Data = data;

    /// <summary>Replaces an entity in memory. Only <see cref="EntityWriter"/> calls this.</summary>
    public void Put(Entity entity)
    {
        switch (entity)
        {
            case Market e: Data.Markets[e.Id] = e; break;
            case Ingredient e: Data.Ingredients[e.Id] = e; break;
            case Meal e: Data.Meals[e.Id] = e; break;
            case PlannedMeal e: Data.PlannedMeals[e.Id] = e; break;
            case ShoppingList e: Data.ShoppingLists[e.Id] = e; break;
            case ShoppingItem e: Data.ShoppingItems[e.Id] = e; break;
            default: throw new ArgumentException($"Unknown entity type {entity.GetType().Name}.", nameof(entity));
        }
    }

    /// <summary>Removes an entity from memory (sync "full" pulls only). Only SyncService calls this.</summary>
    public void Remove(Entity entity)
    {
        switch (entity)
        {
            case Market e: Data.Markets.Remove(e.Id); break;
            case Ingredient e: Data.Ingredients.Remove(e.Id); break;
            case Meal e: Data.Meals.Remove(e.Id); break;
            case PlannedMeal e: Data.PlannedMeals.Remove(e.Id); break;
            case ShoppingList e: Data.ShoppingLists.Remove(e.Id); break;
            case ShoppingItem e: Data.ShoppingItems.Remove(e.Id); break;
            default: throw new ArgumentException($"Unknown entity type {entity.GetType().Name}.", nameof(entity));
        }
    }

    /// <summary>Every entity (including tombstones) by id.</summary>
    public Dictionary<Guid, Entity> AllEntities()
    {
        var all = new Dictionary<Guid, Entity>();
        foreach (var e in Data.Markets.Values) all[e.Id] = e;
        foreach (var e in Data.Ingredients.Values) all[e.Id] = e;
        foreach (var e in Data.Meals.Values) all[e.Id] = e;
        foreach (var e in Data.PlannedMeals.Values) all[e.Id] = e;
        foreach (var e in Data.ShoppingLists.Values) all[e.Id] = e;
        foreach (var e in Data.ShoppingItems.Values) all[e.Id] = e;
        return all;
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
