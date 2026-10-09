namespace MealPlanner.Core.Sync;

// POST /api/sync (architecture › Sync protocol).

/// <param name="Type">One of <see cref="EntityTypes"/>.</param>
/// <param name="Json">The entity serialized with <see cref="CoreJson.Options"/>.</param>
/// <param name="Version">Set by the server in responses; 0 in requests.</param>
public sealed record SyncChange(string Type, Guid Id, string Json, bool IsDeleted, DateTime UpdatedAt, long Version = 0);

public sealed record SyncRequest(long Since, IReadOnlyList<SyncChange> Changes);

public sealed record SyncResponse(long ServerVersion, bool Full, IReadOnlyList<SyncChange> Changes);

public static class SyncLimits
{
    public const int MaxChanges = 500;
    public const int MaxJsonBytes = 64 * 1024;
}
