using MealPlanner.App.Storage;
using MealPlanner.Core.Entities;
using MealPlanner.Core.Sync;

namespace MealPlanner.App.Services;

/// <summary>
/// Steps 3–7 of every mutation (architecture › State and services): put the records in AppState,
/// add them to the outbox, save, raise OnChange and request a sync. Services validate and build
/// the records (with UpdatedAt = Clock.UtcNow) before calling <see cref="SaveAsync"/>.
/// </summary>
public sealed class EntityWriter(AppState state, IStore store, ISyncTrigger sync)
{
    public async Task SaveAsync(params IReadOnlyList<Entity> entities)
    {
        if (entities.Count == 0)
            return;

        foreach (var entity in entities)
        {
            state.Put(entity);
            state.Data.Outbox[entity.Id] = new OutboxEntry(EntityTypes.NameOf(entity), state.Data.NextOutboxSeq++);
        }

        await store.SaveAsync(state.Data);
        state.NotifyChanged();
        sync.RequestSync();
    }
}
