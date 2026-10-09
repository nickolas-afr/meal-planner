namespace MealPlanner.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealth(this RouteGroupBuilder api) => api.MapGet("health", () => "ok").AllowAnonymous();
}
