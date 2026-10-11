namespace MealPlanner.Core.Entities;

public sealed record Ingredient : Entity
{
    public required string Name { get; init; }
    public string Unit { get; init; } = "";        // one of Units.All; "" = no unit
    public IReadOnlyList<Guid> MarketIds { get; init; } = [];  // markets that sell it, priority order; empty = Anywhere
    public Guid? DefaultMarketId { get; init; }    // legacy (schema 1): read through IngredientMarkets.Of; written as MarketIds[0]
    public bool IsStaple { get; init; }
}
