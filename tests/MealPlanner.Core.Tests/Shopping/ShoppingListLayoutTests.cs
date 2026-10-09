using MealPlanner.Core.Entities;
using MealPlanner.Core.Shopping;

namespace MealPlanner.Core.Tests.Shopping;

public class ShoppingListLayoutTests
{
    private static readonly DateOnly Week = new(2026, 10, 5);
    private static readonly DateTime T0 = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    private static ShoppingItem Item(string name, Guid? ingredient = null, Guid? market = null, bool staple = false,
        DateTime? checkedAt = null, bool deleted = false, decimal? qty = null, bool unspecified = false,
        params ItemSource[] sources) => new()
    {
        Id = Guid.NewGuid(), WeekStart = Week, Name = name, IngredientId = ingredient, MarketId = market,
        IsStaple = staple, CheckedAt = checkedAt, IsDeleted = deleted, Quantity = qty, HasUnspecified = unspecified,
        Sources = sources,
    };

    private static Market Mkt(string name, int order, bool deleted = false, params Guid[] itemOrder) => new()
    {
        Id = Guid.NewGuid(), Name = name, SortOrder = order, IsDeleted = deleted, ItemOrder = itemOrder,
    };

    private static Meal MealOf(string name, params Guid[] ingredients) => new()
    {
        Id = Guid.NewGuid(), Name = name, Ingredients = ingredients.Select(g => new MealIngredient(g, null)).ToList(),
    };

    private static ItemSource Src(Meal m, decimal? qty = null, int times = 1) => new(m.Id, m.Name, times, qty);

    private static string[] Names(ShoppingGroup g) => g.Rows.Select(r => r.Item.Name).ToArray();

    [Fact]
    public void ByMarket_AnywhereFirst_ThenMarketsBySortOrder()
    {
        var b = Mkt("B", 2);
        var a = Mkt("A", 1);
        var items = new[] { Item("x", market: b.Id), Item("y", market: a.Id), Item("z") };
        var groups = ShoppingListLayout.ByMarket(items, [b, a]);
        Assert.Equal(["Anywhere", "A", "B"], groups.Select(g => g.Title));
        Assert.Equal(GroupKind.Anywhere, groups[0].Kind);
        Assert.Null(groups[0].Id);
        Assert.Equal(a.Id, groups[1].Id);
        Assert.Equal(GroupKind.Market, groups[1].Kind);
    }

    [Fact]
    public void ByMarket_DeletedOrMissingMarket_FallsBackToAnywhere()
    {
        var gone = Mkt("Gone", 1, deleted: true);
        var items = new[] { Item("a", market: gone.Id), Item("b", market: Guid.NewGuid()), Item("c") };
        var groups = ShoppingListLayout.ByMarket(items, [gone]);
        var g = Assert.Single(groups);
        Assert.Equal(GroupKind.Anywhere, g.Kind);
        Assert.Equal(["a", "b", "c"], Names(g));
    }

    [Fact]
    public void ByMarket_RespectsItemOrder_ThenAlphabetical()
    {
        var i1 = Guid.NewGuid();
        var i2 = Guid.NewGuid();
        var m = Mkt("M", 1, false, i2, i1);
        var items = new[]
        {
            Item("apple", i1, m.Id), Item("zucchini", i2, m.Id),
            Item("Banana", Guid.NewGuid(), m.Id), Item("cherry", null, m.Id),
        };
        var g = Assert.Single(ShoppingListLayout.ByMarket(items, [m]));
        Assert.Equal(["zucchini", "apple", "Banana", "cherry"], Names(g));
    }

    [Fact]
    public void ByMarket_TickedSinkOrderedByCheckedAt()
    {
        var m = Mkt("M", 1);
        var items = new[]
        {
            Item("a", market: m.Id, checkedAt: T0.AddMinutes(5)),
            Item("b", market: m.Id, checkedAt: T0),
            Item("c", market: m.Id),
        };
        var g = Assert.Single(ShoppingListLayout.ByMarket(items, [m]));
        Assert.Equal(["c", "b", "a"], Names(g));
    }

    [Fact]
    public void ByMarket_StaplesGoToCheckYouHave_AndGroupsAreDropped()
    {
        var m = Mkt("M", 1);
        var items = new[]
        {
            Item("salt", staple: true, market: m.Id, checkedAt: T0),
            Item("oil", staple: true, market: m.Id),
            Item("milk", market: m.Id),
        };
        var groups = ShoppingListLayout.ByMarket(items, [m, Mkt("Empty", 2)]);
        Assert.Equal(["M", "Check you have"], groups.Select(g => g.Title));
        Assert.Equal(GroupKind.CheckYouHave, groups[1].Kind);
        Assert.Equal(["oil", "salt"], Names(groups[1]));
        Assert.Equal(["milk"], Names(groups[0]));
    }

    [Fact]
    public void ByMarket_RowCarriesTotalsAndMealNames_AndIgnoresDeleted()
    {
        var m1 = MealOf("Soup");
        var m2 = MealOf("Stew");
        var items = new[]
        {
            Item("carrot", Guid.NewGuid(), qty: 3, unspecified: true, sources: [Src(m1, 1), Src(m2, 2)]),
            Item("ghost", deleted: true),
        };
        var g = Assert.Single(ShoppingListLayout.ByMarket(items, []));
        var row = Assert.Single(g.Rows);
        Assert.Equal(3m, row.Quantity);
        Assert.True(row.HasUnspecified);
        Assert.Equal(["Soup", "Stew"], row.MealNames);
    }

    [Fact]
    public void ByMeal_FollowsPlanOrder_WithTimes_AndSourceQuantity()
    {
        var ia = Guid.NewGuid();
        var ib = Guid.NewGuid();
        var pasta = MealOf("Pasta", ia);
        var salad = MealOf("Salad", ib);
        var items = new[]
        {
            Item("tomato", ia, qty: 6, sources: [Src(pasta, 2, 2), Src(salad, 4)]),
            Item("lettuce", ib, sources: [Src(salad, null)]),
        };
        var groups = ShoppingListLayout.ByMeal(items, [salad, pasta]);
        Assert.Equal(["Salad", "Pasta"], groups.Select(g => g.Title));
        Assert.Equal(pasta.Id, groups[1].Id);
        Assert.Equal(2, groups[1].Times);
        Assert.Equal(1, groups[0].Times);
        Assert.Equal(2m, groups[1].Rows[0].Quantity);
        Assert.False(groups[1].Rows[0].HasUnspecified);
        Assert.Equal(4m, groups[0].Rows.Single(r => r.Item.Name == "tomato").Quantity);
    }

    [Fact]
    public void ByMeal_AlsoInNamesOtherSources_AndMealOrderingOfIngredients()
    {
        var ia = Guid.NewGuid();
        var ib = Guid.NewGuid();
        var pasta = MealOf("Pasta", ib, ia);
        var salad = MealOf("Salad");
        var items = new[]
        {
            Item("apple", ia, sources: [Src(pasta), Src(salad)]),
            Item("basil", ib, sources: [Src(pasta)]),
            Item("extra", Guid.NewGuid(), sources: [Src(pasta)]),
        };
        var groups = ShoppingListLayout.ByMeal(items, [pasta, salad]);
        Assert.Equal(["basil", "apple", "extra"], Names(groups[0]));
        Assert.Equal(["Salad"], groups[0].Rows[1].MealNames);
        Assert.Empty(groups[0].Rows[0].MealNames);
        Assert.Equal(["Pasta"], groups[1].Rows[0].MealNames);
    }

    [Fact]
    public void ByMeal_MealsMissingFromPlanListGoAfter_FirstSeen()
    {
        var planned = MealOf("Planned");
        var other = new Meal { Id = Guid.NewGuid(), Name = "Other" };
        var items = new[]
        {
            Item("a", Guid.NewGuid(), sources: [Src(other)]),
            Item("b", Guid.NewGuid(), sources: [Src(planned)]),
        };
        var groups = ShoppingListLayout.ByMeal(items, [planned]);
        Assert.Equal(["Planned", "Other"], groups.Select(g => g.Title));
    }

    [Fact]
    public void ByMeal_ManualItemsGoToExtras_AfterMeals()
    {
        var meal = MealOf("Soup");
        var items = new[]
        {
            Item("zeta", checkedAt: T0),
            Item("alpha"),
            Item("leek", Guid.NewGuid(), sources: [Src(meal)]),
        };
        var groups = ShoppingListLayout.ByMeal(items, [meal]);
        Assert.Equal(["Soup", "Extras"], groups.Select(g => g.Title));
        Assert.Equal(GroupKind.Extras, groups[1].Kind);
        Assert.Null(groups[1].Id);
        Assert.Equal(["alpha", "zeta"], Names(groups[1]));
    }

    [Fact]
    public void ByMeal_StaplesGoToCheckYouHave_NotMealGroups()
    {
        var meal = MealOf("Soup");
        var items = new[]
        {
            Item("salt", Guid.NewGuid(), staple: true, sources: [Src(meal)]),
            Item("pepper", staple: true, checkedAt: T0),
            Item("oil", staple: true),
        };
        var groups = ShoppingListLayout.ByMeal(items, [meal]);
        var g = Assert.Single(groups);
        Assert.Equal(GroupKind.CheckYouHave, g.Kind);
        Assert.Equal(["oil", "salt", "pepper"], Names(g).OrderBy(n => n == "pepper").ThenBy(n => n, StringComparer.Ordinal));
        Assert.Equal("pepper", Names(g)[^1]);
    }

    [Fact]
    public void ByMeal_TickedRowsSink_InEveryMealGroup()
    {
        var ia = Guid.NewGuid();
        var ib = Guid.NewGuid();
        var m1 = MealOf("One", ia, ib);
        var m2 = MealOf("Two", ia);
        var items = new[]
        {
            Item("a", ia, checkedAt: T0, sources: [Src(m1), Src(m2)]),
            Item("b", ib, sources: [Src(m1)]),
        };
        var groups = ShoppingListLayout.ByMeal(items, [m1, m2]);
        Assert.Equal(["b", "a"], Names(groups[0]));
        Assert.Equal(["a"], Names(groups[1]));
    }

    [Fact]
    public void Progress_ExcludesStaplesAndDeleted()
    {
        var items = new[]
        {
            Item("a", checkedAt: T0), Item("b"), Item("c", staple: true, checkedAt: T0),
            Item("d", deleted: true, checkedAt: T0),
        };
        Assert.Equal((1, 2), ShoppingListLayout.Progress(items));
    }
}
