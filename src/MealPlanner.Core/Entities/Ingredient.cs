namespace MealPlanner.Core.Entities;

public sealed record Ingredient : Entity
{
    public required string Name { get; init; }
    public string Unit { get; init; } = "";        // one of Units.All; "" = no unit
    public Guid? DefaultMarketId { get; init; }    // null = Anywhere
    public bool IsStaple { get; init; }
}
