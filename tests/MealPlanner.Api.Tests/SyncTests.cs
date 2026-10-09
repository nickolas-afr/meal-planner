using System.Net;
using System.Net.Http.Json;
using MealPlanner.Api.Data;
using MealPlanner.Core.Auth;
using MealPlanner.Core.Entities;
using MealPlanner.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests;

public sealed class SyncTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static SyncChange Change(Guid id, string name, bool deleted = false, DateTime? at = null)
    {
        var m = new Market { Id = id, Name = name, IsDeleted = deleted };
        return new SyncChange(EntityTypes.Market, id, EntityTypes.Serialize(m), deleted, at ?? DateTime.UtcNow);
    }

    private static async Task<SyncResponse> Sync(HttpClient c, long since, params SyncChange[] changes)
    {
        var r = await c.PostAsJsonAsync("/api/sync", new SyncRequest(since, changes));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SyncResponse>())!;
    }

    private static async Task AssertInvalid(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(AuthErrors.InvalidRequest, (await r.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    private static string NameOf(SyncChange c) => ((Market)EntityTypes.Deserialize(c.Type, c.Json)!).Name;

    [Fact]
    public async Task Unauthenticated_is_401()
    {
        var r = await factory.ClientFor().PostAsJsonAsync("/api/sync", new SyncRequest(0, []));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Response_includes_own_changes_with_versions()
    {
        var u = await factory.CreateHousehold();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var res = await Sync(factory.ClientFor(u), 0, Change(a, "A"), Change(b, "B"));
        Assert.Equal(2, res.ServerVersion);
        Assert.False(res.Full);
        Assert.Equal([1L, 2L], res.Changes.Select(c => c.Version).ToArray());
        Assert.Equal([a, b], res.Changes.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Two_users_in_one_household_see_each_others_changes()
    {
        var u1 = await factory.CreateHousehold();
        var u2 = await factory.AddMember(u1);
        var id = Guid.NewGuid();
        await Sync(factory.ClientFor(u1), 0, Change(id, "Lidl"));
        var res = await Sync(factory.ClientFor(u2), 0);
        Assert.Single(res.Changes);
        Assert.Equal("Lidl", NameOf(res.Changes[0]));
        var row = await factory.WithDb(db => db.Entities.AsNoTracking().SingleAsync(e => e.Id == id));
        Assert.Equal(u1.UserId, row.UpdatedBy);

        var id2 = Guid.NewGuid();
        await Sync(factory.ClientFor(u2), 0, Change(id2, "Aldi"));
        var back = await Sync(factory.ClientFor(u1), res.ServerVersion);
        Assert.Equal([id2], back.Changes.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Other_households_data_is_never_returned_and_id_collision_does_not_overwrite()
    {
        var a = await factory.CreateHousehold();
        var b = await factory.CreateHousehold();
        var shared = Guid.NewGuid();
        await Sync(factory.ClientFor(a), 0, Change(shared, "Mine"), Change(Guid.NewGuid(), "OnlyA"));
        var bRes = await Sync(factory.ClientFor(b), 0, Change(shared, "Theirs"));
        Assert.Single(bRes.Changes);
        Assert.Equal("Theirs", NameOf(bRes.Changes[0]));
        Assert.Equal(1, bRes.ServerVersion);

        var aRes = await Sync(factory.ClientFor(a), 0);
        Assert.Equal(2, aRes.Changes.Count);
        Assert.Equal("Mine", NameOf(aRes.Changes.Single(c => c.Id == shared)));
    }

    [Fact]
    public async Task Last_write_wins_by_arrival_order()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        var id = Guid.NewGuid();
        // The later push carries an older client timestamp; arrival order still decides.
        await Sync(c, 0, Change(id, "First", at: DateTime.UtcNow));
        var res = await Sync(c, 0, Change(id, "Second", at: DateTime.UtcNow.AddHours(-1)));
        var only = Assert.Single(res.Changes);
        Assert.Equal("Second", NameOf(only));
        Assert.Equal(2, only.Version);
    }

    [Fact]
    public async Task Tombstones_are_returned_incrementally()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        var id = Guid.NewGuid();
        var first = await Sync(c, 0, Change(id, "Gone"));
        var res = await Sync(factory.ClientFor(u), first.ServerVersion, Change(id, "Gone", deleted: true));
        var only = Assert.Single(res.Changes);
        Assert.True(only.IsDeleted);
        Assert.False(res.Full);
    }

    [Fact]
    public async Task Since_below_purged_version_returns_full_without_tombstones()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        var live = Guid.NewGuid();
        var dead = Guid.NewGuid();
        await Sync(c, 0, Change(live, "Live"), Change(dead, "Dead", deleted: true));
        await factory.WithDb(async db =>
        {
            var h = await db.Households.SingleAsync(x => x.Id == u.HouseholdId);
            h.PurgedThroughVersion = 5;
            h.Version = 10;
            h.LastPurgeAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var res = await Sync(c, 3);
        Assert.True(res.Full);
        Assert.Equal([live], res.Changes.Select(x => x.Id).ToArray());
        Assert.Equal(10, res.ServerVersion);

        var inc = await Sync(c, 5);
        Assert.False(inc.Full);
    }

    [Fact]
    public async Task Purge_deletes_old_tombstones_and_raises_purged_version()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        var old = Guid.NewGuid();
        var recent = Guid.NewGuid();
        var oldLive = Guid.NewGuid();
        await Sync(c, 0,
            Change(old, "Old", deleted: true, at: DateTime.UtcNow.AddDays(-61)),
            Change(recent, "Recent", deleted: true, at: DateTime.UtcNow.AddDays(-10)),
            Change(oldLive, "OldLive", at: DateTime.UtcNow.AddDays(-90)));
        // The first sync already purged (LastPurgeAt was null) and set LastPurgeAt; a second push within 24 h must not purge.
        var h1 = await factory.WithDb(db => db.Households.AsNoTracking().SingleAsync(x => x.Id == u.HouseholdId));
        Assert.NotNull(h1.LastPurgeAt);
        Assert.Equal(1, h1.PurgedThroughVersion);
        Assert.DoesNotContain(await factory.WithDb(db => db.Entities.Where(e => e.HouseholdId == u.HouseholdId).Select(e => e.Id).ToListAsync()), id => id == old);

        await Sync(c, 0, Change(old, "Old again", deleted: true, at: DateTime.UtcNow.AddDays(-61)));
        Assert.True(await factory.WithDb(db => db.Entities.AnyAsync(e => e.HouseholdId == u.HouseholdId && e.Id == old)));

        // After 24 h the next sync purges again.
        await factory.WithDb(async db =>
        {
            var h = await db.Households.SingleAsync(x => x.Id == u.HouseholdId);
            h.LastPurgeAt = DateTime.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
        });
        var res = await Sync(c, 0);
        Assert.False(await factory.WithDb(db => db.Entities.AnyAsync(e => e.HouseholdId == u.HouseholdId && e.Id == old)));
        var h2 = await factory.WithDb(db => db.Households.AsNoTracking().SingleAsync(x => x.Id == u.HouseholdId));
        Assert.Equal(4, h2.PurgedThroughVersion);
        Assert.True(res.Full); // since 0 < PurgedThroughVersion, so tombstones (even recent) are omitted
        Assert.DoesNotContain(res.Changes, x => x.Id == recent);
        Assert.Contains(res.Changes, x => x.Id == oldLive);
    }

    [Fact]
    public async Task Oversize_json_is_400()
    {
        var u = await factory.CreateHousehold();
        var id = Guid.NewGuid();
        var json = EntityTypes.Serialize(new Market { Id = id, Name = new string('x', SyncLimits.MaxJsonBytes) });
        var r = await factory.ClientFor(u).PostAsJsonAsync("/api/sync",
            new SyncRequest(0, [new SyncChange(EntityTypes.Market, id, json, false, DateTime.UtcNow)]));
        await AssertInvalid(r);
    }

    [Fact]
    public async Task Unknown_type_is_400()
    {
        var u = await factory.CreateHousehold();
        var good = Change(Guid.NewGuid(), "X");
        var r = await factory.ClientFor(u).PostAsJsonAsync("/api/sync",
            new SyncRequest(0, [good with { Type = "Spaceship" }]));
        await AssertInvalid(r);
    }

    [Fact]
    public async Task Json_that_does_not_fit_type_is_400_and_nothing_is_applied()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        var ok = Change(Guid.NewGuid(), "Fine");
        var bad = Change(Guid.NewGuid(), "Bad") with { Json = "{\"nope\":1}" };
        await AssertInvalid(await c.PostAsJsonAsync("/api/sync", new SyncRequest(0, [ok, bad])));
        await AssertInvalid(await c.PostAsJsonAsync("/api/sync", new SyncRequest(0, [ok with { Json = "not json" }])));
        Assert.Empty((await Sync(c, 0)).Changes);
    }

    [Fact]
    public async Task Json_id_mismatch_is_400()
    {
        var u = await factory.CreateHousehold();
        var r = await factory.ClientFor(u).PostAsJsonAsync("/api/sync",
            new SyncRequest(0, [Change(Guid.NewGuid(), "X") with { Id = Guid.NewGuid() }]));
        await AssertInvalid(r);
    }

    [Fact]
    public async Task More_than_500_changes_is_400()
    {
        var u = await factory.CreateHousehold();
        var changes = Enumerable.Range(0, SyncLimits.MaxChanges + 1).Select(i => Change(Guid.NewGuid(), "M" + i)).ToList();
        await AssertInvalid(await factory.ClientFor(u).PostAsJsonAsync("/api/sync", new SyncRequest(0, changes)));
        var exactly = changes.Take(SyncLimits.MaxChanges).ToList();
        Assert.Equal(SyncLimits.MaxChanges, (await Sync(factory.ClientFor(u), 0, [.. exactly])).Changes.Count);
    }

    [Fact]
    public async Task Negative_since_and_null_body_are_400()
    {
        var u = await factory.CreateHousehold();
        var c = factory.ClientFor(u);
        await AssertInvalid(await c.PostAsJsonAsync("/api/sync", new SyncRequest(-1, [])));
        await AssertInvalid(await c.PostAsync("/api/sync", new StringContent("null", System.Text.Encoding.UTF8, "application/json")));
    }
}
