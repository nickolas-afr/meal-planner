namespace MealPlanner.Core.Entities;

public sealed record MealIngredient(Guid IngredientId, decimal? Quantity);   // null = "some"
