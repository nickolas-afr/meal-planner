using MealPlanner.Api.Auth;
using MealPlanner.Api.Data;
using MealPlanner.Core.Auth;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Endpoints;

public static class HouseholdEndpoints
{
    public static void MapHousehold(this RouteGroupBuilder api)
    {
        api.MapGet("me", async (HttpContext http, AppDb db) =>
        {
            Guid userId = http.User.UserId(), householdId = http.User.HouseholdId();
            User? me = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.HouseholdId == householdId);
            Household? household = await db.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == householdId);
            if (me is null || household is null)
                return Results.Json(new ErrorResponse(AuthErrors.InvalidToken), statusCode: 401);
            List<MemberInfo> members = await db.Users.AsNoTracking()
                .Where(u => u.HouseholdId == householdId)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new MemberInfo(u.Id, u.DisplayName))
                .ToListAsync();
            return Results.Ok(new MeResponse(me.Id, me.DisplayName, new HouseholdInfo(household.Id, household.Name), members));
        });

        api.MapPost("household/invites", async (HttpContext http, PasskeyService s) =>
            Results.Ok(await s.CreateInviteAsync(http.User.UserId(), http.User.HouseholdId())));
    }
}
