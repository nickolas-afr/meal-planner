namespace MealPlanner.Core.Entities;

public sealed record Meal : Entity
{
    public required string Name { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<MealIngredient> Ingredients { get; init; } = [];     // in the order added
}
