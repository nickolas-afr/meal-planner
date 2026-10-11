using MealPlanner.Core.Entities;

namespace MealPlanner.Core;

/// <summary>Reads an ingredient's markets, including data written before ingredients had several (schema 1).</summary>
public static class IngredientMarkets
{
    /// <summary>The markets in priority order: MarketIds, else [DefaultMarketId], else empty (Anywhere).</summary>
    public static IReadOnlyList<Guid> Of(Ingredient ingredient) =>
        ingredient.MarketIds.Count > 0 ? ingredient.MarketIds
        : ingredient.DefaultMarketId is { } id ? [id]
        : [];

    /// <summary>The ingredient with its markets set, keeping the legacy field in step for older clients.</summary>
    public static Ingredient With(Ingredient ingredient, IReadOnlyList<Guid> marketIds) =>
        ingredient with { MarketIds = marketIds, DefaultMarketId = marketIds.Count > 0 ? marketIds[0] : null };
}
