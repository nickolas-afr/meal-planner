namespace MealPlanner.Core.Entities;

public sealed record PlannedMeal : Entity          // one entry; a meal planned twice = two entries
{
    public required DateOnly WeekStart { get; init; }   // always a Monday
    public required Guid MealId { get; init; }
    public required DateTime AddedAt { get; init; }     // UTC; orders the "By meal" groups
}
