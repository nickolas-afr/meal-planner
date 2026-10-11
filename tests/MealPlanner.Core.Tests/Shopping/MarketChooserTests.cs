using MealPlanner.Core.Entities;
using MealPlanner.Core.Shopping;

namespace MealPlanner.Core.Tests.Shopping;

public class MarketChooserTests
{
    private static readonly DateOnly Week = new(2026, 10, 5);
    private static readonly DateTime T0 = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    private static Market Mk(string name, int order, bool deleted = false) =>
        new() { Id = Guid.NewGuid(), Name = name, SortOrder = order, IsDeleted = deleted };

    private static Ingredient Ing(bool staple = false, params Market[] ms) =>
        IngredientMarkets.With(new Ingredient { Id = Guid.NewGuid(), Name = "I", IsStaple = staple }, ms.Select(m => m.Id).ToList());

    private static ShoppingItem ItemOf(Ingredient i, Action<ShoppingItem>? _ = null) =>
        new() { Id = Guid.NewGuid(), WeekStart = Week, IngredientId = i.Id, Name = i.Name, IsStaple = i.IsStaple };

    private static IReadOnlyList<Guid> Choose(Market[] markets, Ingredient[] ings, params ShoppingItem[] items) =>
        MarketChooser.Choose(items, ings.ToDictionary(i => i.Id), markets);

    private static (ShoppingItem[] items, Ingredient[] ings) Flex(params Market[][] candidates)
    {
        var ings = candidates.Select(c => Ing(false, c)).ToArray();
        return (ings.Select(i => ItemOf(i)).ToArray(), ings);
    }

    [Fact]
    public void OneMarketSellsEverything()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var (items, ings) = Flex([a, b], [b], [b, a]);
        Assert.Equal([b.Id], Choose([a, b], ings, items));
    }

    [Fact]
    public void TwoMarketsNeeded()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var (items, ings) = Flex([a], [b]);
        Assert.Equal([a.Id, b.Id], Choose([a, b], ings, items));
    }

    [Fact]
    public void FewestBeatsPriority()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var (items, ings) = Flex([a, b], [b]);
        var chosen = Choose([a, b], ings, items);
        Assert.Equal([b.Id], chosen);
        Assert.Equal(b.Id, MarketChooser.Assign(items[0], chosen, ings.ToDictionary(i => i.Id), [a, b]));
    }

    [Fact]
    public void PriorityBreaksTiesBetweenEqualSets()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        // x prefers b, y prefers b: {b} rank 0; {a} rank 1+1.
        var (items, ings) = Flex([b, a], [b, a]);
        Assert.Equal([b.Id], Choose([a, b], ings, items));
    }

    [Fact]
    public void SettingsOrderBreaksTheRest()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var (items, ings) = Flex([b, a], [a, b]);   // either alone: total rank 1
        Assert.Equal([a.Id], Choose([a, b], ings, items));
        var swapped = Choose([Mk2(a, 5), Mk2(b, 1)], ings, items);
        Assert.Equal([b.Id], swapped);
    }

    private static Market Mk2(Market m, int order) => m with { SortOrder = order };

    [Fact]
    public void DeletedMarketsAreIgnored()
    {
        var a = Mk("A", 0, deleted: true); var b = Mk("B", 1);
        var (items, ings) = Flex([a, b], [a]);
        Assert.Equal([b.Id], Choose([a, b], ings, items));
    }

    [Fact]
    public void PlainStaplesNeverAddAMarket()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var normal = Ing(false, a); var staple = Ing(true, b);
        Assert.Equal([a.Id], Choose([a, b], [normal, staple], ItemOf(normal), ItemOf(staple)));
    }

    [Fact]
    public void AnywhereItemsNeverAddAMarket()
    {
        var a = Mk("A", 0);
        var any = Ing(false); var normal = Ing(false, a);
        Assert.Equal([a.Id], Choose([a], [any, normal], ItemOf(any), ItemOf(normal)));
        Assert.Empty(Choose([a], [any], ItemOf(any)));
    }

    [Fact]
    public void NeededStapleAddsItsPinnedMarket()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var normal = Ing(false, a); var staple = Ing(true, b);
        var needed = ItemOf(staple) with { IsNeeded = true, MarketId = b.Id };
        Assert.Equal([a.Id, b.Id], Choose([a, b], [normal, staple], ItemOf(normal), needed));
    }

    [Fact]
    public void PinnedManualMovedAndTickedMarketsAlwaysIncluded()
    {
        var a = Mk("A", 0); var b = Mk("B", 1); var c = Mk("C", 2); var d = Mk("D", 3);
        var sold = Ing(false, a, b, c, d);
        var other = Ing(false, a, b, c, d);
        var third = Ing(false, a, b, c, d);
        var manual = new ShoppingItem { Id = Guid.NewGuid(), WeekStart = Week, Name = "m", MarketId = d.Id };
        var moved = ItemOf(other) with { MarketOverridden = true, MarketId = c.Id };
        var ticked = ItemOf(third) with { CheckedAt = T0, MarketId = b.Id };
        var all = new[] { sold, other, third };
        Assert.Equal([b.Id, c.Id, d.Id],
            Choose([a, b, c, d], all, ItemOf(sold), manual, moved, ticked));
    }

    [Fact]
    public void PinnedItemOnDeletedMarketIsIgnored()
    {
        var a = Mk("A", 0); var gone = Mk("G", 1, deleted: true);
        var manual = new ShoppingItem { Id = Guid.NewGuid(), WeekStart = Week, Name = "m", MarketId = gone.Id };
        Assert.Empty(Choose([a, gone], [], manual));
    }

    [Fact]
    public void LegacyDefaultMarketIdOnlyIngredientWorks()
    {
        var a = Mk("A", 0);
        var legacy = new Ingredient { Id = Guid.NewGuid(), Name = "L", DefaultMarketId = a.Id };
        Assert.Equal([a.Id], Choose([a], [legacy], ItemOf(legacy)));
    }

    [Fact]
    public void GreedyPathCoversEverything()
    {
        var ms = Enumerable.Range(0, 14).Select(i => Mk("M" + i, i)).ToArray();
        var ings = ms.Select(m => Ing(false, m)).ToArray();         // each item only at its own market
        var extra = Ing(false, ms[0], ms[1], ms[2]);
        var items = ings.Append(extra).Select(i => ItemOf(i)).ToArray();
        var chosen = Choose(ms, ings.Append(extra).ToArray(), items);
        Assert.Equal(ms.Select(m => m.Id), chosen);
    }

    [Fact]
    public void GreedyPicksMarketCoveringMostItems()
    {
        var ms = Enumerable.Range(0, 13).Select(i => Mk("M" + i, i)).ToArray();
        // 13 candidate markets; big market 12 sells items 1..3, each item also sold at its own market.
        var ings = Enumerable.Range(0, 3).Select(i => Ing(false, ms[i], ms[12])).ToArray();
        var pad = Enumerable.Range(3, 9).Select(i => Ing(false, ms[i])).ToArray();
        var all = ings.Concat(pad).ToArray();
        var chosen = Choose(ms, all, all.Select(i => ItemOf(i)).ToArray());
        Assert.Contains(ms[12].Id, chosen);
        Assert.Equal(10, chosen.Count);
    }

    [Fact]
    public void ResultIsInSortOrder()
    {
        var a = Mk("A", 2); var b = Mk("B", 0); var c = Mk("C", 1);
        var (items, ings) = Flex([a], [b], [c]);
        Assert.Equal([b.Id, c.Id, a.Id], Choose([a, b, c], ings, items));
    }

    // ---- Assign ----

    [Fact]
    public void Assign_HighestPriorityChosen()
    {
        var a = Mk("A", 0); var b = Mk("B", 1); var c = Mk("C", 2);
        var ing = Ing(false, a, b, c);
        var d = new Dictionary<Guid, Ingredient> { [ing.Id] = ing };
        Assert.Equal(b.Id, MarketChooser.Assign(ItemOf(ing), [b.Id, c.Id], d, [a, b, c]));
    }

    [Fact]
    public void Assign_StapleNotAtChosenMarketGetsFirstCandidate()
    {
        var a = Mk("A", 0); var b = Mk("B", 1); var c = Mk("C", 2);
        var ing = Ing(true, b, c);
        var d = new Dictionary<Guid, Ingredient> { [ing.Id] = ing };
        Assert.Equal(b.Id, MarketChooser.Assign(ItemOf(ing), [a.Id], d, [a, b, c]));
    }

    [Fact]
    public void Assign_NoCandidatesIsNull()
    {
        var a = Mk("A", 0);
        var ing = Ing(false);
        var d = new Dictionary<Guid, Ingredient> { [ing.Id] = ing };
        Assert.Null(MarketChooser.Assign(ItemOf(ing), [a.Id], d, [a]));
    }

    [Fact]
    public void Assign_SkipsDeletedFirstCandidate()
    {
        var gone = Mk("G", 0, deleted: true); var b = Mk("B", 1);
        var ing = Ing(true, gone, b);
        var d = new Dictionary<Guid, Ingredient> { [ing.Id] = ing };
        Assert.Equal(b.Id, MarketChooser.Assign(ItemOf(ing), [], d, [gone, b]));
    }

    [Fact]
    public void Assign_PinnedKeepsMarketId()
    {
        var a = Mk("A", 0); var b = Mk("B", 1);
        var ing = Ing(false, a);
        var d = new Dictionary<Guid, Ingredient> { [ing.Id] = ing };
        var pinned = ItemOf(ing) with { MarketOverridden = true, MarketId = b.Id };
        Assert.Equal(b.Id, MarketChooser.Assign(pinned, [a.Id], d, [a, b]));
        var manual = new ShoppingItem { Id = Guid.NewGuid(), WeekStart = Week, Name = "m", MarketId = b.Id };
        Assert.Equal(b.Id, MarketChooser.Assign(manual, [a.Id], d, [a, b]));
    }
}
