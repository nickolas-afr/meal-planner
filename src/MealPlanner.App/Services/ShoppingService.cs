using MealPlanner.Core;
using MealPlanner.Core.Entities;
using MealPlanner.Core.Shopping;

namespace MealPlanner.App.Services;

public enum ShoppingListState { None, UpToDate, OutOfDate }

public sealed class ShoppingService(AppState state, EntityWriter writer, Clock clock, MarketService markets)
{
    public ShoppingList? ListFor(DateOnly week) =>
        state.Data.ShoppingLists.GetValueOrDefault(Ids.ForList(week)) is { IsDeleted: false } l ? l : null;

    public IReadOnlyList<ShoppingItem> Items(DateOnly week) =>
        state.ShoppingItems.Where(i => i.WeekStart == week).ToList();

    /// <summary>Distinct live meals planned in the week, by earliest AddedAt.</summary>
    public IReadOnlyList<Meal> MealsInPlanOrder(DateOnly week) =>
        state.PlannedMeals
            .Where(p => p.WeekStart == week)
            .GroupBy(p => p.MealId)
            .Select(g => (Meal: state.Meal(g.Key), First: g.Min(p => p.AddedAt)))
            .Where(g => g.Meal is not null)
            .OrderBy(g => g.First)
            .Select(g => g.Meal!)
            .ToList();

    public IReadOnlyList<DateOnly> WeeksWithList() =>
        state.ShoppingLists.Select(l => l.WeekStart).Distinct().Order().ToList();

    public ShoppingListState GetState(DateOnly week)
    {
        if (ListFor(week) is null)
            return ShoppingListState.None;
        return ShoppingListBuilder.IsOutOfDate(Items(week), Build(week))
            ? ShoppingListState.OutOfDate
            : ShoppingListState.UpToDate;
    }

    /// <summary>Creates the week's list, or brings an existing one up to date with the plan.</summary>
    public async Task CreateOrUpdate(DateOnly week)
    {
        var built = Build(week);
        var now = clock.UtcNow;
        var existingItems = state.Data.ShoppingItems.Values.Where(i => i.WeekStart == week);
        var changed = new List<Entity>();

        var id = Ids.ForList(week);
        var existing = state.Data.ShoppingLists.GetValueOrDefault(id);
        if (existing is null)
            changed.Add(new ShoppingList { Id = id, WeekStart = week, GeneratedAt = now, UpdatedAt = now });
        else
            changed.Add(existing with { IsDeleted = false, GeneratedAt = now, UpdatedAt = now });

        changed.AddRange(ShoppingListBuilder.Merge(existingItems, built, now));
        await writer.SaveAsync(changed);
    }

    public async Task Toggle(Guid itemId)
    {
        if (LiveItem(itemId) is not { } item)
            return;
        var now = clock.UtcNow;
        await writer.SaveAsync(item with { CheckedAt = item.CheckedAt is null ? now : null, UpdatedAt = now });
    }

    /// <summary>Moves the item to a market (null = Anywhere) for this week only.</summary>
    public async Task MoveToMarket(Guid itemId, Guid? marketId)
    {
        if (marketId is { } m && state.Market(m) is null)
            throw new InvalidOperationException("This market no longer exists.");
        if (LiveItem(itemId) is not { } item)
            return;
        await writer.SaveAsync(item with { MarketId = marketId, MarketOverridden = true, UpdatedAt = clock.UtcNow });
    }

    public string? ValidateManual(string? name, string? quantityText, string unit)
    {
        if (Validation.ItemName(name) is { } nameError)
            return nameError;
        if (Validation.Quantity(quantityText) is { } quantityError)
            return quantityError;
        return Units.IsValid(unit) ? null : "Choose a unit from the list.";
    }

    public async Task AddManual(DateOnly week, string name, string? quantityText, string unit, Guid? marketId)
    {
        if (ListFor(week) is null)
            throw new InvalidOperationException("Create the shopping list for this week first.");
        Check(name, quantityText, unit, marketId);
        QuantityFormat.TryParse(quantityText, out var quantity);

        await writer.SaveAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(),
            WeekStart = week,
            IngredientId = null,
            Name = name.Trim(),
            Unit = unit,
            Quantity = quantity,
            MarketId = marketId,
            MarketOverridden = false,
            IsStaple = false,
            UpdatedAt = clock.UtcNow,
        });
    }

    public async Task EditManual(Guid itemId, string name, string? quantityText, string unit, Guid? marketId)
    {
        if (LiveItem(itemId) is not { IngredientId: null } item)
            throw new InvalidOperationException("Only manual items can be edited.");
        Check(name, quantityText, unit, marketId);
        QuantityFormat.TryParse(quantityText, out var quantity);

        await writer.SaveAsync(item with
        {
            Name = name.Trim(),
            Unit = unit,
            Quantity = quantity,
            MarketId = marketId,
            UpdatedAt = clock.UtcNow,
        });
    }

    public async Task DeleteManual(Guid itemId)
    {
        if (LiveItem(itemId) is not { IngredientId: null } item)
            throw new InvalidOperationException("Only manual items can be deleted.");
        await writer.SaveAsync(item with { IsDeleted = true, UpdatedAt = clock.UtcNow });
    }

    /// <summary>Stores the order of the given ingredients; ids not in the list keep their relative order after them.</summary>
    public async Task SaveMarketOrder(Guid marketId, IReadOnlyList<Guid> orderedIngredientIds)
    {
        if (state.Market(marketId) is not { } market)
            return;
        var listed = orderedIngredientIds.ToHashSet();
        var order = orderedIngredientIds.Concat(market.ItemOrder.Where(id => !listed.Contains(id))).ToList();
        await markets.SetItemOrder(marketId, order);
    }

    private IReadOnlyList<ShoppingItem> Build(DateOnly week) =>
        ShoppingListBuilder.Build(week, state.Data.PlannedMeals.Values, state.Data.Meals, state.Data.Ingredients);

    private ShoppingItem? LiveItem(Guid id) =>
        state.Data.ShoppingItems.GetValueOrDefault(id) is { IsDeleted: false } i ? i : null;

    private void Check(string name, string? quantityText, string unit, Guid? marketId)
    {
        if (ValidateManual(name, quantityText, unit) is { } error)
            throw new InvalidOperationException(error);
        if (marketId is { } m && state.Market(m) is null)
            throw new InvalidOperationException("This market no longer exists.");
    }
}
