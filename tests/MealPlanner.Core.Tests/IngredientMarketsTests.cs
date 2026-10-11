using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Tests;

public class IngredientMarketsTests
{
    private static Ingredient Ing() => new() { Id = Guid.NewGuid(), Name = "Rice" };

    [Fact]
    public void Of_MarketIds_win_over_legacy_field()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), legacy = Guid.NewGuid();
        var i = Ing() with { MarketIds = [a, b], DefaultMarketId = legacy };
        Assert.Equal([a, b], IngredientMarkets.Of(i));
    }

    [Fact]
    public void Of_legacy_DefaultMarketId_only_gives_that_market()
    {
        var m = Guid.NewGuid();
        Assert.Equal([m], IngredientMarkets.Of(Ing() with { DefaultMarketId = m }));
    }

    [Fact]
    public void Of_neither_is_empty() => Assert.Empty(IngredientMarkets.Of(Ing()));

    [Fact]
    public void With_sets_both_fields_first_id_as_legacy()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var i = IngredientMarkets.With(Ing(), [a, b]);
        Assert.Equal([a, b], i.MarketIds);
        Assert.Equal(a, i.DefaultMarketId);
    }

    [Fact]
    public void With_empty_clears_legacy_field()
    {
        var i = IngredientMarkets.With(Ing() with { DefaultMarketId = Guid.NewGuid() }, []);
        Assert.Empty(i.MarketIds);
        Assert.Null(i.DefaultMarketId);
    }
}
