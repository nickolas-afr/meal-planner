using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Shopping;

public enum ShoppingView { ByMeal, ByMarket }

public enum GroupKind { Anywhere, Market, Meal, Extras, CheckYouHave }

/// <param name="Id">The market id for Market groups, the meal id for Meal groups, otherwise null.</param>
/// <param name="Times">How often the meal is planned (Meal groups); 1 for every other group.</param>
public sealed record ShoppingGroup(GroupKind Kind, Guid? Id, string Title, int Times, IReadOnlyList<ShoppingRow> Rows);

/// <param name="Quantity">The amount to show on this row: the source's amount in a Meal group, otherwise the item total.</param>
/// <param name="HasUnspecified">Whether to show " +" after the amount.</param>
/// <param name="MealNames">In a Meal group, the other meals that need the item ("Also in"); elsewhere, every meal that needs it.</param>
public sealed record ShoppingRow(ShoppingItem Item, decimal? Quantity, bool HasUnspecified, IReadOnlyList<string> MealNames);

/// <summary>Turns a week's list into display groups. Rules: spec › Shopping list › Views.</summary>
public static class ShoppingListLayout
{
    public const string AnywhereTitle = "Anywhere";
    public const string ExtrasTitle = "Extras";
    public const string CheckYouHaveTitle = "Check you have";

    public static IReadOnlyList<ShoppingGroup> Build(
        ShoppingView view,
        IEnumerable<ShoppingItem> items,
        IEnumerable<Market> markets,
        IReadOnlyList<Meal> mealsInPlanOrder) =>
        view == ShoppingView.ByMeal ? ByMeal(items, mealsInPlanOrder) : ByMarket(items, markets);

    public static IReadOnlyList<ShoppingGroup> ByMarket(IEnumerable<ShoppingItem> items, IEnumerable<Market> markets)
    {
        var live = items.Where(i => !i.IsDeleted).ToList();
        var regular = live.Where(i => !i.IsStaple).ToList();
        var liveMarkets = markets.Where(m => !m.IsDeleted).OrderBy(m => m.SortOrder).ToList();
        var marketIds = liveMarkets.Select(m => m.Id).ToHashSet();

        var groups = new List<ShoppingGroup>();

        var anywhere = regular.Where(i => i.MarketId is not { } id || !marketIds.Contains(id)).ToList();
        groups.Add(new ShoppingGroup(GroupKind.Anywhere, null, AnywhereTitle, 1,
            Sink(anywhere.Where(Unticked).OrderBy(ByName, StringComparer.OrdinalIgnoreCase), anywhere.Where(Ticked)).Select(MarketRow).ToList()));

        foreach (var market in liveMarkets)
        {
            var inMarket = regular.Where(i => i.MarketId == market.Id).ToList();
            var position = new Dictionary<Guid, int>();
            for (var n = 0; n < market.ItemOrder.Count; n++) position.TryAdd(market.ItemOrder[n], n);

            var unticked = inMarket.Where(Unticked).ToList();
            var ordered = unticked
                .Where(i => i.IngredientId is { } g && position.ContainsKey(g))
                .OrderBy(i => position[i.IngredientId!.Value])
                .Concat(unticked
                    .Where(i => !(i.IngredientId is { } g && position.ContainsKey(g)))
                    .OrderBy(ByName, StringComparer.OrdinalIgnoreCase));
            groups.Add(new ShoppingGroup(GroupKind.Market, market.Id, market.Name, 1,
                Sink(ordered, inMarket.Where(Ticked)).Select(MarketRow).ToList()));
        }

        return Finish(groups, live);
    }

    public static IReadOnlyList<ShoppingGroup> ByMeal(IEnumerable<ShoppingItem> items, IReadOnlyList<Meal> mealsInPlanOrder)
    {
        var live = items.Where(i => !i.IsDeleted).ToList();
        var regular = live.Where(i => !i.IsStaple).ToList();

        // Meal order: plan order first, then meals only seen in sources, first-seen order.
        var order = new List<(Guid Id, string Name, Meal? Meal)>();
        var seen = new HashSet<Guid>();
        foreach (var m in mealsInPlanOrder)
            if (seen.Add(m.Id)) order.Add((m.Id, m.Name, m));
        foreach (var src in regular.SelectMany(i => i.Sources))
            if (seen.Add(src.MealId)) order.Add((src.MealId, src.MealName, null));

        var groups = new List<ShoppingGroup>();
        foreach (var (mealId, mealName, meal) in order)
        {
            var pairs = regular
                .SelectMany(i => i.Sources.Where(s => s.MealId == mealId).Select(s => (Item: i, Source: s)))
                .ToList();
            if (pairs.Count == 0) continue;

            var position = new Dictionary<Guid, int>();
            if (meal is not null)
                for (var n = 0; n < meal.Ingredients.Count; n++) position.TryAdd(meal.Ingredients[n].IngredientId, n);
            bool Known((ShoppingItem Item, ItemSource Source) p) =>
                p.Item.IngredientId is { } g && position.ContainsKey(g);

            var unticked = pairs.Where(p => Unticked(p.Item)).ToList();
            var ordered = unticked.Where(Known).OrderBy(p => position[p.Item.IngredientId!.Value])
                .Concat(unticked.Where(p => !Known(p)).OrderBy(p => p.Item, Comparer<ShoppingItem>.Create(CompareByName)));
            var ticked = pairs.Where(p => Ticked(p.Item))
                .OrderBy(p => p.Item.CheckedAt).ThenBy(p => p.Item.Name, StringComparer.OrdinalIgnoreCase);

            var rows = ordered.Concat(ticked).Select(p => new ShoppingRow(
                p.Item, p.Source.Quantity, false,
                p.Item.Sources.Where(s => s.MealId != mealId).Select(s => s.MealName).ToList())).ToList();
            groups.Add(new ShoppingGroup(GroupKind.Meal, mealId, mealName, pairs[0].Source.Times, rows));
        }

        var extras = regular.Where(i => i.IngredientId is null).ToList();
        groups.Add(new ShoppingGroup(GroupKind.Extras, null, ExtrasTitle, 1,
            Sink(extras.Where(Unticked).OrderBy(ByName, StringComparer.OrdinalIgnoreCase), extras.Where(Ticked)).Select(MarketRow).ToList()));

        return Finish(groups, live);
    }

    /// <summary>(bought, total) over live, non-staple items.</summary>
    public static (int Bought, int Total) Progress(IEnumerable<ShoppingItem> items)
    {
        var counted = items.Where(i => !i.IsDeleted && !i.IsStaple).ToList();
        return (counted.Count(Ticked), counted.Count);
    }

    private static bool Ticked(ShoppingItem i) => i.CheckedAt is not null;

    private static bool Unticked(ShoppingItem i) => i.CheckedAt is null;

    private static string ByName(ShoppingItem i) => i.Name;

    private static int CompareByName(ShoppingItem a, ShoppingItem b) =>
        StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);

    private static IEnumerable<ShoppingItem> Sink(IEnumerable<ShoppingItem> unticked, IEnumerable<ShoppingItem> ticked) =>
        unticked.Concat(ticked.OrderBy(i => i.CheckedAt).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase));

    private static ShoppingRow MarketRow(ShoppingItem i) =>
        new(i, i.Quantity, i.HasUnspecified, i.Sources.Select(s => s.MealName).ToList());

    /// <summary>Adds the staples group, drops empty groups.</summary>
    private static IReadOnlyList<ShoppingGroup> Finish(List<ShoppingGroup> groups, List<ShoppingItem> live)
    {
        var staples = live.Where(i => i.IsStaple).ToList();
        groups.Add(new ShoppingGroup(GroupKind.CheckYouHave, null, CheckYouHaveTitle, 1,
            Sink(staples.Where(Unticked).OrderBy(ByName, StringComparer.OrdinalIgnoreCase), staples.Where(Ticked))
                .Select(MarketRow).ToList()));
        return groups.Where(g => g.Rows.Count > 0).ToList();
    }
}
