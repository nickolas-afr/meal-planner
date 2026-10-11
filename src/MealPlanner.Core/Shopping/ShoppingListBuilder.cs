using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Shopping;

/// <summary>Builds a week's ingredient items from its plan and merges them into an existing list.</summary>
public static class ShoppingListBuilder
{
    public static IReadOnlyList<ShoppingItem> Build(
        DateOnly week,
        IEnumerable<PlannedMeal> plannedMeals,
        IReadOnlyDictionary<Guid, Meal> meals,
        IReadOnlyDictionary<Guid, Ingredient> ingredients,
        IEnumerable<Market> markets,
        IEnumerable<ShoppingItem> existingItems)
    {
        var marketList = markets.ToList();
        var liveExisting = existingItems.Where(i => !i.IsDeleted).ToList();
        var matches = liveExisting.Where(i => i.IngredientId is not null)
            .GroupBy(i => i.IngredientId!.Value).ToDictionary(g => g.Key, g => g.First());

        var groups = plannedMeals
            .Where(p => !p.IsDeleted && p.WeekStart == week)
            .GroupBy(p => p.MealId)
            .Select(g => (MealId: g.Key, Times: g.Count(), First: g.Min(p => p.AddedAt)))
            .OrderBy(g => g.First)
            .ToList();

        var order = new List<Guid>();
        var buckets = new Dictionary<Guid, List<ItemSource>>();

        foreach (var (mealId, times, _) in groups)
        {
            if (!meals.TryGetValue(mealId, out var meal) || meal.IsDeleted) continue;
            foreach (var row in meal.Ingredients)
            {
                if (!ingredients.TryGetValue(row.IngredientId, out var ing) || ing.IsDeleted) continue;
                if (!buckets.TryGetValue(ing.Id, out var list))
                {
                    buckets[ing.Id] = list = [];
                    order.Add(ing.Id);
                }
                list.Add(new ItemSource(meal.Id, meal.Name, times, row.Quantity * times));
            }
        }

        var drafts = new List<ShoppingItem>(order.Count);
        foreach (var id in order)
        {
            var ing = ingredients[id];
            var sources = buckets[id];
            var known = sources.Where(s => s.Quantity is not null).ToList();
            matches.TryGetValue(id, out var match);
            drafts.Add(new ShoppingItem
            {
                Id = Ids.ForItem(week, id),
                WeekStart = week,
                IngredientId = id,
                Name = ing.Name,
                Unit = ing.Unit,
                IsStaple = ing.IsStaple,
                MarketId = match?.MarketId,
                IsNeeded = match?.IsNeeded ?? false,
                CheckedAt = match?.CheckedAt,
                MarketOverridden = match?.MarketOverridden ?? false,
                Quantity = known.Count == 0 ? null : known.Sum(s => s.Quantity!.Value),
                HasUnspecified = known.Count != sources.Count,
                Sources = sources,
            });
        }

        // drafts carry their match's pin state so the chooser keeps pinned markets.
        var chosen = MarketChooser.Choose(
            drafts.Concat(liveExisting.Where(i => i.IngredientId is null)), ingredients, marketList);
        return drafts.Select(d => d with
        {
            MarketId = MarketChooser.Assign(d, chosen, ingredients, marketList),
            CheckedAt = null,
            MarketOverridden = false,
        }).ToList();
    }

    public static bool IsOutOfDate(IEnumerable<ShoppingItem> existingItems, IEnumerable<ShoppingItem> builtItems)
    {
        var existing = Live(existingItems).ToDictionary(i => i.IngredientId!.Value);
        var built = Live(builtItems).ToDictionary(i => i.IngredientId!.Value);
        if (!existing.Keys.ToHashSet().SetEquals(built.Keys)) return true;

        foreach (var (id, b) in built)
        {
            var e = existing[id];
            if (e.Name != b.Name || e.Unit != b.Unit || e.Quantity != b.Quantity
                || e.HasUnspecified != b.HasUnspecified || e.IsStaple != b.IsStaple
                || !e.Sources.SequenceEqual(b.Sources)
                || (!MarketChooser.IsPinned(e) && e.MarketId != b.MarketId))
                return true;
        }
        return false;
    }

    /// <summary>Returns only the entities that changed, each stamped with <paramref name="now"/>.</summary>
    public static IReadOnlyList<ShoppingItem> Merge(
        IEnumerable<ShoppingItem> existingItems,
        IEnumerable<ShoppingItem> builtItems,
        DateTime now)
    {
        var existing = existingItems.Where(i => i.IngredientId is not null)
            .GroupBy(i => i.IngredientId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.IsDeleted).First());
        var built = builtItems.Where(i => i.IngredientId is not null && !i.IsDeleted).ToList();
        var builtIds = built.Select(i => i.IngredientId!.Value).ToHashSet();

        var changed = new List<ShoppingItem>();

        foreach (var b in built)
        {
            if (!existing.TryGetValue(b.IngredientId!.Value, out var e))
            {
                changed.Add(b with { IsDeleted = false, CheckedAt = null, MarketOverridden = false, UpdatedAt = now });
            }
            else if (e.IsDeleted)
            {
                changed.Add(b with
                {
                    IsDeleted = false, CheckedAt = null, MarketOverridden = false,
                    Version = e.Version, UpdatedAt = now,
                });
            }
            else
            {
                var merged = b with
                {
                    Id = e.Id,
                    IsDeleted = false,
                    Version = e.Version,
                    MarketOverridden = e.MarketOverridden,
                    IsNeeded = e.IsNeeded,
                    MarketId = MarketChooser.IsPinned(e) ? e.MarketId : b.MarketId,
                    CheckedAt = (b.Quantity ?? 0) > (e.Quantity ?? 0) ? null : e.CheckedAt,
                    UpdatedAt = e.UpdatedAt,
                };
                if (!SameContent(merged, e)) changed.Add(merged with { UpdatedAt = now });
            }
        }

        foreach (var e in existing.Values)
            if (!e.IsDeleted && !builtIds.Contains(e.IngredientId!.Value))
                changed.Add(e with { IsDeleted = true, UpdatedAt = now });

        return changed;
    }

    private static IEnumerable<ShoppingItem> Live(IEnumerable<ShoppingItem> items) =>
        items.Where(i => i.IngredientId is not null && !i.IsDeleted);

    private static bool SameContent(ShoppingItem a, ShoppingItem b) =>
        a.Sources.SequenceEqual(b.Sources) &&
        a with { Sources = [], UpdatedAt = default } == b with { Sources = [], UpdatedAt = default };
}
