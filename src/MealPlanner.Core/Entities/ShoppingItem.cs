namespace MealPlanner.Core.Entities;

public sealed record ShoppingItem : Entity         // ingredient item: Id = Ids.ForItem(WeekStart, IngredientId)
{                                                  // manual item: Id = Guid.NewGuid()
    public required DateOnly WeekStart { get; init; }
    public Guid? IngredientId { get; init; }       // null = manual item
    public required string Name { get; init; }
    public string Unit { get; init; } = "";
    public decimal? Quantity { get; init; }        // sum of known source quantities; null = none known
    public bool HasUnspecified { get; init; }      // at least one source had no quantity
    public IReadOnlyList<ItemSource> Sources { get; init; } = [];   // empty for manual items
    public Guid? MarketId { get; init; }           // null = Anywhere
    public bool MarketOverridden { get; init; }    // moved for this week only
    public bool IsStaple { get; init; }
    public DateTime? CheckedAt { get; init; }      // null = not bought
    public bool IsNeeded { get; init; }            // staple marked "Need to buy": listed and counted like a normal item
}
