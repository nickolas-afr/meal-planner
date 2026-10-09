using MealPlanner.Api.Auth;
using MealPlanner.Api.Sync;
using MealPlanner.Core.Sync;

namespace MealPlanner.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSync(this RouteGroupBuilder api)
    {
        api.MapPost("sync", async (HttpContext http, SyncRequest? request, SyncHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.SyncAsync(http.User.HouseholdId(), http.User.UserId(), request, ct)));
    }
}
