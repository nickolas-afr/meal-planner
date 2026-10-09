using MealPlanner.Api.Auth;
using MealPlanner.Core.Auth;

namespace MealPlanner.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("auth").AllowAnonymous();

        auth.MapPost("register/begin", async (RegisterBeginRequest r, PasskeyService s) => Results.Ok(await s.BeginRegisterAsync(r)));
        auth.MapPost("register/complete", async (RegisterCompleteRequest r, PasskeyService s) => Results.Ok(await s.CompleteRegisterAsync(r)));
        auth.MapPost("login/begin", (PasskeyService s) => Results.Ok(s.BeginLogin()));
        auth.MapPost("login/complete", async (LoginCompleteRequest r, PasskeyService s) => Results.Ok(await s.CompleteLoginAsync(r)));

        auth.MapPost("refresh", async (RefreshRequest r, TokenService t) =>
            await t.RefreshAsync(r.RefreshToken) is { } pair
                ? Results.Ok(pair)
                : Results.Json(new ErrorResponse(AuthErrors.InvalidToken), statusCode: 401));

        auth.MapPost("logout", async (RefreshRequest r, TokenService t) =>
        {
            await t.RevokeAsync(r.RefreshToken);
            return Results.NoContent();
        });
    }
}
