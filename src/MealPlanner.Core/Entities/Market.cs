namespace MealPlanner.Core.Entities;

public sealed record Market : Entity
{
    public required string Name { get; init; }
    public int SortOrder { get; init; }                        // order in Settings and in "By market"
    public IReadOnlyList<Guid> ItemOrder { get; init; } = [];  // ingredient ids, manual order in this market
}
