namespace MealPlanner.Core.Entities;

public abstract record Entity
{
    public required Guid Id { get; init; }
    public DateTime UpdatedAt { get; init; }      // UTC, set by the client service on every change
    public bool IsDeleted { get; init; }          // soft delete; syncs like any change
    public long Version { get; init; }            // set by the server; 0 = never synced
}
