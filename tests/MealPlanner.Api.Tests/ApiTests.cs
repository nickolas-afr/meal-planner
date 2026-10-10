using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MealPlanner.Api.Data;
using MealPlanner.Core.Auth;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<string?> ErrorOf(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<ErrorResponse>())?.Error;

    private static Task<HttpResponseMessage> Refresh(HttpClient c, string token) =>
        c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(token));

    private async Task<string> NewInvite(TestUser user)
    {
        var r = await factory.ClientFor(user).PostAsync("/api/household/invites", null);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<InviteResponse>())!.Code;
    }

    private static Task<HttpResponseMessage> Begin(HttpClient c, string? code) =>
        c.PostAsJsonAsync("/api/auth/register/begin", new RegisterBeginRequest("Sam", code));

    [Fact]
    public async Task Health_returns_ok()
    {
        var r = await factory.ClientFor().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("ok", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_rotates_and_old_token_is_rejected()
    {
        var user = await factory.CreateHousehold();
        var c = factory.ClientFor();

        var r1 = await Refresh(c, user.Tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var b = (await r1.Content.ReadFromJsonAsync<TokenPair>())!;
        Assert.NotEqual(user.Tokens.RefreshToken, b.RefreshToken);

        var replay = await Refresh(c, user.Tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("invalid_token", await ErrorOf(replay));

        var r2 = await Refresh(c, b.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var user = await factory.CreateHousehold();
        var c = factory.ClientFor();
        var b = (await (await Refresh(c, user.Tokens.RefreshToken)).Content.ReadFromJsonAsync<TokenPair>())!;

        var logout = await c.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(b.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var after = await Refresh(c, b.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal("invalid_token", await ErrorOf(after));
    }

    [Fact]
    public async Task Refresh_with_unknown_token_is_401()
    {
        var r = await Refresh(factory.ClientFor(), "nonsense");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("invalid_token", await ErrorOf(r));
    }

    [Fact]
    public async Task Expired_refresh_token_is_401()
    {
        var user = await factory.CreateHousehold();
        await factory.WithDb(async db =>
        {
            await db.RefreshTokens.Where(t => t.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        });
        var r = await Refresh(factory.ClientFor(), user.Tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("invalid_token", await ErrorOf(r));
    }

    [Fact]
    public async Task Me_returns_caller_household_and_only_its_members()
    {
        var a = await factory.CreateHousehold(2, "Alice");
        var other = await factory.CreateHousehold(3, "Zed");

        var r = await factory.ClientFor(a).GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = (await r.Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.Equal(a.UserId, me.UserId);
        Assert.Equal("Alice", me.DisplayName);
        Assert.Equal(a.HouseholdId, me.Household.Id);
        Assert.Equal(2, me.Members.Count);
        Assert.Contains(me.Members, m => m.UserId == a.UserId);
        Assert.DoesNotContain(me.Members, m => m.UserId == other.UserId);
        Assert.DoesNotContain(me.Members, m => m.DisplayName == "Zed");
    }

    [Fact]
    public async Task Me_without_token_is_401()
    {
        var r = await factory.ClientFor().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Create_invite_returns_code_valid_for_48_hours()
    {
        var user = await factory.CreateHousehold();
        DateTime before = DateTime.UtcNow;
        var r = await factory.ClientFor(user).PostAsync("/api/household/invites", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var invite = (await r.Content.ReadFromJsonAsync<InviteResponse>())!;
        Assert.Matches(new Regex("^[0-9A-HJKMNP-TV-Z]{8}$"), invite.Code);
        Assert.InRange(invite.ExpiresAt, before.AddHours(48).AddMinutes(-1), DateTime.UtcNow.AddHours(48).AddMinutes(1));
    }

    [Fact]
    public async Task Create_invite_without_token_is_401()
    {
        var r = await factory.ClientFor().PostAsync("/api/household/invites", null);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Register_begin_accepts_valid_invite_and_normalises_case_and_dash()
    {
        var user = await factory.CreateHousehold();
        string code = await NewInvite(user);
        var c = factory.ClientFor();

        Assert.Equal(HttpStatusCode.OK, (await Begin(c, code)).StatusCode);
        string messy = (code[..4] + "-" + code[4..]).ToLowerInvariant();
        Assert.Equal(HttpStatusCode.OK, (await Begin(c, messy)).StatusCode);
    }

    [Fact]
    public async Task Register_begin_without_invite_starts_new_household_flow()
    {
        var r = await Begin(factory.ClientFor(), null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = (await r.Content.ReadFromJsonAsync<RegisterBeginResponse>())!;
        Assert.False(string.IsNullOrEmpty(body.FlowId));
    }

    [Fact]
    public async Task Register_begin_with_unknown_invite_is_400()
    {
        var r = await Begin(factory.ClientFor(), "ZZZZZZZZ");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invite_invalid", await ErrorOf(r));
    }

    [Fact]
    public async Task Register_begin_with_expired_invite_is_400()
    {
        var user = await factory.CreateHousehold();
        string code = await NewInvite(user);
        await factory.WithDb(db => db.Invites.Where(i => i.Code == code)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        var r = await Begin(factory.ClientFor(), code);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invite_invalid", await ErrorOf(r));
    }

    [Fact]
    public async Task Register_begin_with_used_invite_is_400()
    {
        var user = await factory.CreateHousehold();
        string code = await NewInvite(user);
        await factory.WithDb(db => db.Invites.Where(i => i.Code == code)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedAt, DateTime.UtcNow).SetProperty(i => i.UsedBy, user.UserId)));
        var r = await Begin(factory.ClientFor(), code);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invite_invalid", await ErrorOf(r));
    }

    [Fact]
    public async Task Register_begin_with_blank_name_is_400_invalid_request()
    {
        var r = await factory.ClientFor().PostAsJsonAsync("/api/auth/register/begin", new RegisterBeginRequest("  ", null));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invalid_request", await ErrorOf(r));
    }

    [Fact]
    public async Task Full_household_cannot_create_invite()
    {
        var user = await factory.CreateHousehold(4);
        var r = await factory.ClientFor(user).PostAsync("/api/household/invites", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("household_full", await ErrorOf(r));
    }

    [Fact]
    public async Task Three_member_household_can_still_invite()
    {
        var user = await factory.CreateHousehold(3);
        var r = await factory.ClientFor(user).PostAsync("/api/household/invites", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // The API does NOT check household size at register/begin (only at register/complete,
    // which needs a real WebAuthn attestation and is not unit-tested). So begin succeeds
    // for an invite whose household filled up after the invite was issued.
    [Fact]
    public async Task Register_begin_with_invite_to_full_household_is_409()
    {
        var user = await factory.CreateHousehold(3);
        string code = await NewInvite(user);
        await factory.WithDb(async db =>
        {
            db.Users.Add(new User { Id = Guid.NewGuid(), HouseholdId = user.HouseholdId, DisplayName = "Fourth", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var r = await Begin(factory.ClientFor(), code);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("household_full", await ErrorOf(r));
    }

    [Fact]
    public async Task With_signups_closed_new_households_are_refused_but_invites_still_work()
    {
        var user = await factory.CreateHousehold(1);
        string code = await NewInvite(user);
        using var closed = factory.WithWebHostBuilder(b => b.UseSetting("Auth:AllowNewHouseholds", "false"));
        var client = closed.CreateClient();

        var refused = await Begin(client, null);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("signups_closed", await ErrorOf(refused));

        Assert.Equal(HttpStatusCode.OK, (await Begin(client, code)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login/begin", new { })).StatusCode);
    }
}
