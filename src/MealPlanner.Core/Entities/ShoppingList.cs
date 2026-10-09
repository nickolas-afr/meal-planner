namespace MealPlanner.Core.Entities;

public sealed record ShoppingList : Entity         // Id = Ids.ForList(WeekStart)
{
    public required DateOnly WeekStart { get; init; }
    public required DateTime GeneratedAt { get; init; }
}
