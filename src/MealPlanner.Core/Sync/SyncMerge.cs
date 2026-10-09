using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Sync;

/// <summary>What the client must do to its local data after a sync response.</summary>
/// <param name="Upserts">Entities to put into local state as-is (no outbox entry), incoming Version included.</param>
/// <param name="Removals">Local entities to drop entirely (only for a full response).</param>
public sealed record SyncMergeResult(IReadOnlyList<Entity> Upserts, IReadOnlyList<Entity> Removals);

/// <summary>The client side of the sync algorithm (architecture › SyncService), as pure functions.</summary>
public static class SyncMerge
{
    /// <summary>The request changes for the outbox ids that still exist locally.</summary>
    public static IReadOnlyList<SyncChange> BuildChanges(IEnumerable<Guid> outboxIds, IReadOnlyDictionary<Guid, Entity> local)
    {
        var changes = new List<SyncChange>();
        foreach (var id in outboxIds)
        {
            if (!local.TryGetValue(id, out var e)) continue;
            changes.Add(new SyncChange(EntityTypes.NameOf(e), e.Id, EntityTypes.Serialize(e), e.IsDeleted, e.UpdatedAt, 0));
        }
        return changes;
    }

    /// <summary>
    /// Applies a response: skip incoming entities whose id is still in the outbox; otherwise replace the local one.
    /// When <see cref="SyncResponse.Full"/>, also drop every local entity that is neither in the response nor in the outbox.
    /// Incoming changes whose Type is unknown or whose Json doesn't deserialize are ignored.
    /// </summary>
    public static SyncMergeResult Apply(SyncResponse response, IReadOnlyDictionary<Guid, Entity> local, IReadOnlySet<Guid> outboxIds)
    {
        var upserts = new List<Entity>();
        foreach (var change in response.Changes)
        {
            if (outboxIds.Contains(change.Id)) continue;
            var entity = EntityTypes.Deserialize(change.Type, change.Json);
            if (entity is null) continue;
            upserts.Add(entity with { Version = change.Version, IsDeleted = change.IsDeleted });
        }

        var removals = new List<Entity>();
        if (response.Full)
        {
            var responseIds = response.Changes.Select(c => c.Id).ToHashSet();
            foreach (var e in local.Values)
                if (!responseIds.Contains(e.Id) && !outboxIds.Contains(e.Id))
                    removals.Add(e);
        }
        return new SyncMergeResult(upserts, removals);
    }
}
