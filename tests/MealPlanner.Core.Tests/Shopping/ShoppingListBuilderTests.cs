using MealPlanner.Core.Entities;
using MealPlanner.Core.Shopping;

namespace MealPlanner.Core.Tests.Shopping;

public class ShoppingListBuilderTests
{
    private static readonly DateOnly Week = new(2026, 10, 5);
    private static readonly DateTime T0 = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);

    private static Ingredient Ing(string name, string unit = "g", Guid? market = null, bool staple = false, bool deleted = false) =>
        new() { Id = Guid.NewGuid(), Name = name, Unit = unit, DefaultMarketId = market, IsStaple = staple, IsDeleted = deleted };

    private static Meal MealOf(string name, bool deleted = false, params MealIngredient[] rows) =>
        new() { Id = Guid.NewGuid(), Name = name, Ingredients = rows, IsDeleted = deleted };

    private static PlannedMeal Plan(Meal m, int minutes = 0, DateOnly? week = null, bool deleted = false) =>
        new() { Id = Guid.NewGuid(), WeekStart = week ?? Week, MealId = m.Id, AddedAt = T0.AddMinutes(minutes), IsDeleted = deleted };

    private static IReadOnlyList<ShoppingItem> Build(IEnumerable<PlannedMeal> plans, Meal[] meals, Ingredient[] ings) =>
        ShoppingListBuilder.Build(Week, plans, meals.ToDictionary(m => m.Id), ings.ToDictionary(i => i.Id));

    private static ShoppingItem Manual() =>
        new() { Id = Guid.NewGuid(), WeekStart = Week, Name = "Candles" };

    // One meal with flour 200 (g) and eggs 3; returns built items.
    private static (Ingredient flour, Ingredient eggs, Meal meal, IReadOnlyList<ShoppingItem> built) Basic(decimal flourQty = 200)
    {
        var flour = Ing("Flour");
        var eggs = Ing("Eggs", "");
        var meal = MealOf("Cake", false, new MealIngredient(flour.Id, flourQty), new MealIngredient(eggs.Id, 3));
        return (flour, eggs, meal, Build([Plan(meal)], [meal], [flour, eggs]));
    }

    // ---- Build ----

    [Fact]
    public void Build_OneMeal()
    {
        var (flour, eggs, meal, built) = Basic();
        Assert.Equal(2, built.Count);
        var f = built[0];
        Assert.Equal(flour.Id, f.IngredientId);
        Assert.Equal("Flour", f.Name);
        Assert.Equal("g", f.Unit);
        Assert.Equal(200m, f.Quantity);
        Assert.False(f.HasUnspecified);
        Assert.Null(f.CheckedAt);
        Assert.False(f.MarketOverridden);
        Assert.Equal(new ItemSource(meal.Id, "Cake", 1, 200), Assert.Single(f.Sources));
        Assert.Equal(eggs.Id, built[1].IngredientId);
    }

    [Fact]
    public void Build_IdMatchesIdsForItem()
    {
        var (flour, _, _, built) = Basic();
        Assert.Equal(Ids.ForItem(Week, flour.Id), built[0].Id);
        Assert.Equal(Week, built[0].WeekStart);
    }

    [Fact]
    public void Build_MealPlannedTwiceDoublesQuantity()
    {
        var flour = Ing("Flour");
        var meal = MealOf("Cake", false, new MealIngredient(flour.Id, 200));
        var built = Build([Plan(meal, 0), Plan(meal, 5)], [meal], [flour]);
        var item = Assert.Single(built);
        Assert.Equal(400m, item.Quantity);
        Assert.Equal(new ItemSource(meal.Id, "Cake", 2, 400), Assert.Single(item.Sources));
    }

    [Fact]
    public void Build_SharedIngredientSumsWithTwoSources()
    {
        var flour = Ing("Flour");
        var a = MealOf("A", false, new MealIngredient(flour.Id, 100));
        var b = MealOf("B", false, new MealIngredient(flour.Id, 50));
        var item = Assert.Single(Build([Plan(a, 0), Plan(b, 1)], [a, b], [flour]));
        Assert.Equal(150m, item.Quantity);
        Assert.Equal(2, item.Sources.Count);
        Assert.Equal(a.Id, item.Sources[0].MealId);
        Assert.Equal(b.Id, item.Sources[1].MealId);
    }

    [Fact]
    public void Build_NullQuantitySetsHasUnspecified()
    {
        var flour = Ing("Flour");
        var a = MealOf("A", false, new MealIngredient(flour.Id, 100));
        var b = MealOf("B", false, new MealIngredient(flour.Id, null));
        var item = Assert.Single(Build([Plan(a, 0), Plan(b, 1)], [a, b], [flour]));
        Assert.Equal(100m, item.Quantity);
        Assert.True(item.HasUnspecified);
        Assert.Null(item.Sources[1].Quantity);
    }

    [Fact]
    public void Build_AllNullGivesNullQuantity()
    {
        var salt = Ing("Salt");
        var a = MealOf("A", false, new MealIngredient(salt.Id, null));
        var item = Assert.Single(Build([Plan(a)], [a], [salt]));
        Assert.Null(item.Quantity);
        Assert.True(item.HasUnspecified);
    }

    [Fact]
    public void Build_SkipsDeletedMeals()
    {
        var flour = Ing("Flour");
        var a = MealOf("A", true, new MealIngredient(flour.Id, 1));
        Assert.Empty(Build([Plan(a)], [a], [flour]));
    }

    [Fact]
    public void Build_SkipsMealsMissingFromDictionary()
    {
        var flour = Ing("Flour");
        var a = MealOf("A", false, new MealIngredient(flour.Id, 1));
        Assert.Empty(Build([Plan(a)], [], [flour]));
    }

    [Fact]
    public void Build_SkipsDeletedPlannedEntriesAndOtherWeeks()
    {
        var flour = Ing("Flour");
        var a = MealOf("A", false, new MealIngredient(flour.Id, 1));
        var plans = new[] { Plan(a, deleted: true), Plan(a, week: Week.AddDays(7)) };
        Assert.Empty(Build(plans, [a], [flour]));
    }

    [Fact]
    public void Build_SkipsDeletedAndMissingIngredients()
    {
        var gone = Ing("Gone", deleted: true);
        var missing = Ing("Missing");
        var ok = Ing("Ok");
        var a = MealOf("A", false, new MealIngredient(gone.Id, 1), new MealIngredient(missing.Id, 1), new MealIngredient(ok.Id, 1));
        var item = Assert.Single(Build([Plan(a)], [a], [gone, ok]));
        Assert.Equal(ok.Id, item.IngredientId);
    }

    [Fact]
    public void Build_CopiesStapleAndMarket()
    {
        var market = Guid.NewGuid();
        var salt = Ing("Salt", "g", market, staple: true);
        var a = MealOf("A", false, new MealIngredient(salt.Id, 1));
        var item = Assert.Single(Build([Plan(a)], [a], [salt]));
        Assert.True(item.IsStaple);
        Assert.Equal(market, item.MarketId);
    }

    [Fact]
    public void Build_OrdersGroupsByEarliestAddedAt()
    {
        var x = Ing("X");
        var y = Ing("Y");
        var a = MealOf("A", false, new MealIngredient(x.Id, 1));
        var b = MealOf("B", false, new MealIngredient(y.Id, 1));
        // A's earliest entry (minute 0) precedes B's (minute 5), so A's group comes first.
        var built = Build([Plan(b, 5), Plan(a, 10), Plan(a, 0)], [a, b], [x, y]);
        Assert.Equal(new[] { x.Id, y.Id }, built.Select(i => i.IngredientId!.Value));
        Assert.Equal(2, built[0].Sources[0].Times);
    }

    // ---- IsOutOfDate ----

    [Fact]
    public void IsOutOfDate_IdenticalIsFalse()
    {
        var (_, _, _, built) = Basic();
        Assert.False(ShoppingListBuilder.IsOutOfDate(built, built));
    }

    [Fact]
    public void IsOutOfDate_AddedMealIsTrue()
    {
        var (_, _, _, built) = Basic();
        Assert.True(ShoppingListBuilder.IsOutOfDate(built.Take(1), built));
    }

    [Fact]
    public void IsOutOfDate_RemovedMealIsTrue()
    {
        var (_, _, _, built) = Basic();
        Assert.True(ShoppingListBuilder.IsOutOfDate(built, built.Take(1)));
    }

    [Fact]
    public void IsOutOfDate_ChangedQuantityIsTrue()
    {
        var (_, _, _, built) = Basic();
        var changed = built.Select((i, n) => n == 0 ? i with { Quantity = 999 } : i).ToList();
        Assert.True(ShoppingListBuilder.IsOutOfDate(built, changed));
    }

    [Fact]
    public void IsOutOfDate_RenamedIngredientIsTrue()
    {
        var (_, _, _, built) = Basic();
        var renamed = built.Select((i, n) => n == 0 ? i with { Name = "Plain flour" } : i).ToList();
        Assert.True(ShoppingListBuilder.IsOutOfDate(built, renamed));
    }

    [Fact]
    public void IsOutOfDate_ChangedMarketIsTrue()
    {
        var (_, _, _, built) = Basic();
        var moved = built.Select((i, n) => n == 0 ? i with { MarketId = Guid.NewGuid() } : i).ToList();
        Assert.True(ShoppingListBuilder.IsOutOfDate(built, moved));
    }

    [Fact]
    public void IsOutOfDate_ChangedMarketIgnoredWhenOverridden()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select((i, n) => n == 0 ? i with { MarketId = Guid.NewGuid(), MarketOverridden = true } : i).ToList();
        Assert.False(ShoppingListBuilder.IsOutOfDate(existing, built));
    }

    [Fact]
    public void IsOutOfDate_IgnoresManualItems()
    {
        var (_, _, _, built) = Basic();
        Assert.False(ShoppingListBuilder.IsOutOfDate(built.Append(Manual()), built));
        Assert.False(ShoppingListBuilder.IsOutOfDate(built, built.Append(Manual())));
    }

    // ---- Merge ----

    [Fact]
    public void Merge_NothingChangedReturnsNothing()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { CheckedAt = T0, Version = 4, UpdatedAt = T0 }).ToList();
        Assert.Empty(ShoppingListBuilder.Merge(existing, built, Now));
    }

    [Fact]
    public void Merge_KeepsTickWhenQuantityLower()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { Quantity = 500, CheckedAt = T0, Version = 4 }).ToList();
        var result = ShoppingListBuilder.Merge(existing, built, Now);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(T0, r.CheckedAt));
        Assert.All(result, r => Assert.Equal(4, r.Version));
    }

    [Fact]
    public void Merge_KeepsTickWhenOtherFieldChanges()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { Name = "Old", CheckedAt = T0 }).ToList();
        var result = ShoppingListBuilder.Merge(existing, built, Now);
        Assert.All(result, r => { Assert.Equal(T0, r.CheckedAt); Assert.NotEqual("Old", r.Name); });
    }

    [Fact]
    public void Merge_UntickWhenQuantityRises()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { Quantity = 1, CheckedAt = T0 }).ToList();
        var result = ShoppingListBuilder.Merge(existing, built, Now);
        Assert.All(result, r => Assert.Null(r.CheckedAt));
    }

    [Fact]
    public void Merge_RemovesNoLongerNeededItems()
    {
        var (_, _, _, built) = Basic();
        var result = ShoppingListBuilder.Merge(built, built.Take(1), Now);
        var removed = Assert.Single(result);
        Assert.True(removed.IsDeleted);
        Assert.Equal(built[1].Id, removed.Id);
    }

    [Fact]
    public void Merge_AddsNewItem()
    {
        var (_, _, _, built) = Basic();
        var result = ShoppingListBuilder.Merge(built.Take(1), built, Now);
        var added = Assert.Single(result);
        Assert.Equal(built[1].Id, added.Id);
        Assert.False(added.IsDeleted);
        Assert.Null(added.CheckedAt);
    }

    [Fact]
    public void Merge_ReAddsDeletedItemKeepingVersion()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { IsDeleted = true, Version = 7, CheckedAt = T0 }).ToList();
        var result = ShoppingListBuilder.Merge(existing, built, Now);
        Assert.Equal(2, result.Count);
        Assert.All(result, r =>
        {
            Assert.False(r.IsDeleted);
            Assert.Equal(7, r.Version);
            Assert.Null(r.CheckedAt);
        });
    }

    [Fact]
    public void Merge_NeverReturnsManualItems()
    {
        var (_, _, _, built) = Basic();
        var result = ShoppingListBuilder.Merge([Manual()], built, Now);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.NotNull(r.IngredientId));
        Assert.Empty(ShoppingListBuilder.Merge(built.Append(Manual()), built, Now));
    }

    [Fact]
    public void Merge_KeepsMarketOverride()
    {
        var (_, _, _, built) = Basic();
        var mine = Guid.NewGuid();
        var existing = built.Select((i, n) => n == 0 ? i with { MarketId = mine, MarketOverridden = true } : i).ToList();
        Assert.Empty(ShoppingListBuilder.Merge(existing, built, Now));

        var stale = existing.Select(i => i with { Quantity = 1 }).ToList();
        var first = ShoppingListBuilder.Merge(stale, built, Now).Single(r => r.Id == built[0].Id);
        Assert.Equal(mine, first.MarketId);
        Assert.True(first.MarketOverridden);
    }

    [Fact]
    public void Merge_UpdatesMarketWhenNotOverridden()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select((i, n) => n == 0 ? i with { MarketId = Guid.NewGuid() } : i).ToList();
        var result = Assert.Single(ShoppingListBuilder.Merge(existing, built, Now));
        Assert.Equal(built[0].MarketId, result.MarketId);
    }

    [Fact]
    public void Merge_ReturnedItemsStampedWithNow()
    {
        var (_, _, _, built) = Basic();
        var existing = built.Select(i => i with { Quantity = 1, UpdatedAt = T0 }).ToList();
        var result = ShoppingListBuilder.Merge(existing.Take(1), built, Now);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(Now, r.UpdatedAt));
    }
}
