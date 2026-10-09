namespace MealPlanner.Core.Entities;

// Quantity = meal quantity × Times, or null when the meal row has no quantity
public sealed record ItemSource(Guid MealId, string MealName, int Times, decimal? Quantity);
