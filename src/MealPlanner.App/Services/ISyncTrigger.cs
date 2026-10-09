namespace MealPlanner.App.Services;

/// <summary>Asks for a sync after a local change. SyncService implements it from M6.</summary>
public interface ISyncTrigger
{
    void RequestSync();
}

/// <summary>Used while <see cref="Features.RequireAuth"/> is false: changes stay in the outbox.</summary>
public sealed class NoSyncTrigger : ISyncTrigger
{
    public void RequestSync() { }
}
