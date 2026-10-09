using System.Security.Claims;

namespace MealPlanner.Api.Auth;

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    public static Guid HouseholdId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("hid")!);
}
