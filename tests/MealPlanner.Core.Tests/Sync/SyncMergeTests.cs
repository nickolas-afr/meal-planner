using MealPlanner.Core.Entities;
using MealPlanner.Core.Sync;

namespace MealPlanner.Core.Tests.Sync;

public class SyncMergeTests
{
    private static readonly DateTime T = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static Market Mk(string name, Guid? id = null, long version = 0, bool deleted = false) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, UpdatedAt = T, Version = version, IsDeleted = deleted };

    private static SyncChange Ch(Entity e, long version, bool? deleted = null) =>
        new(EntityTypes.NameOf(e), e.Id, EntityTypes.Serialize(e), deleted ?? e.IsDeleted, e.UpdatedAt, version);

    private static Dictionary<Guid, Entity> Local(params Entity[] es) => es.ToDictionary(e => e.Id);

    private static HashSet<Guid> Ids(params Guid[] ids) => ids.ToHashSet();

    [Fact]
    public void Incoming_entity_still_in_outbox_is_skipped()
    {
        var local = Mk("local");
        var incoming = local with { Name = "server" };
        var r = SyncMerge.Apply(new SyncResponse(5, false, [Ch(incoming, 5)]), Local(local), Ids(local.Id));
        Assert.Empty(r.Upserts);
        Assert.Empty(r.Removals);
    }

    [Fact]
    public void Incoming_entity_replaces_local_and_carries_server_version()
    {
        var local = Mk("old", version: 1);
        var incoming = local with { Name = "new", Version = 0 };
        var r = SyncMerge.Apply(new SyncResponse(7, false, [Ch(incoming, 7)]), Local(local), Ids());
        var m = Assert.IsType<Market>(Assert.Single(r.Upserts));
        Assert.Equal("new", m.Name);
        Assert.Equal(7, m.Version);
        Assert.Equal(local.Id, m.Id);
    }

    [Fact]
    public void Tombstones_come_through_as_deleted_upserts()
    {
        var e = Mk("gone");
        var wire = Ch(e, 9, deleted: true);
        var r = SyncMerge.Apply(new SyncResponse(9, false, [wire]), Local(), Ids());
        var m = Assert.Single(r.Upserts);
        Assert.True(m.IsDeleted);
        Assert.Equal(9, m.Version);
    }

    [Fact]
    public void Full_response_keeps_outbox_entities_and_removes_other_locals()
    {
        var inResponse = Mk("a");
        var inOutbox = Mk("b");
        var stale = Mk("c");
        var response = new SyncResponse(3, true, [Ch(inResponse, 3), Ch(inOutbox, 3)]);
        var staleOnly = Mk("d");
        var r = SyncMerge.Apply(response, Local(inResponse, inOutbox, stale, staleOnly), Ids(inOutbox.Id));
        Assert.Equal([inResponse.Id], r.Upserts.Select(u => u.Id));
        Assert.Equivalent(new[] { stale.Id, staleOnly.Id }, r.Removals.Select(x => x.Id));
        Assert.DoesNotContain(r.Removals, x => x.Id == inOutbox.Id);
        Assert.DoesNotContain(r.Removals, x => x.Id == inResponse.Id);
    }

    [Fact]
    public void Full_response_keeps_outbox_entity_that_is_not_in_response()
    {
        var pending = Mk("pending");
        var r = SyncMerge.Apply(new SyncResponse(1, true, []), Local(pending), Ids(pending.Id));
        Assert.Empty(r.Removals);
    }

    [Fact]
    public void Non_full_response_never_removes()
    {
        var other = Mk("other");
        var r = SyncMerge.Apply(new SyncResponse(1, false, []), Local(other), Ids());
        Assert.Empty(r.Removals);
        Assert.Empty(r.Upserts);
    }

    [Fact]
    public void Unknown_type_or_broken_json_is_ignored()
    {
        var good = Mk("good");
        var changes = new[]
        {
            new SyncChange("Nope", Guid.NewGuid(), "{}", false, T, 1),
            new SyncChange(EntityTypes.Market, Guid.NewGuid(), "{ not json", false, T, 2),
            Ch(good, 3),
        };
        var r = SyncMerge.Apply(new SyncResponse(3, false, changes), Local(), Ids());
        Assert.Equal(good.Id, Assert.Single(r.Upserts).Id);
    }

    [Fact]
    public void Entities_of_different_types_in_one_response()
    {
        var market = Mk("m");
        var ingredient = new Ingredient { Id = Guid.NewGuid(), Name = "Rice", UpdatedAt = T };
        var r = SyncMerge.Apply(new SyncResponse(2, false, [Ch(market, 1), Ch(ingredient, 2)]), Local(), Ids());
        Assert.Equal(2, r.Upserts.Count);
        Assert.IsType<Market>(r.Upserts[0]);
        var i = Assert.IsType<Ingredient>(r.Upserts[1]);
        Assert.Equal("Rice", i.Name);
        Assert.Equal(2, i.Version);
    }

    [Fact]
    public void BuildChanges_skips_missing_ids_and_keeps_order()
    {
        var a = Mk("a");
        var b = Mk("b");
        var missing = Guid.NewGuid();
        var changes = SyncMerge.BuildChanges([b.Id, missing, a.Id], Local(a, b));
        Assert.Equal([b.Id, a.Id], changes.Select(c => c.Id));
    }

    [Fact]
    public void BuildChanges_round_trips_and_sets_metadata()
    {
        var m = new Ingredient { Id = Guid.NewGuid(), Name = "rt", Unit = "g", UpdatedAt = T, Version = 4, IsDeleted = true };
        var c = Assert.Single(SyncMerge.BuildChanges([m.Id], Local(m)));
        Assert.Equal(EntityTypes.Ingredient, c.Type);
        Assert.True(c.IsDeleted);
        Assert.Equal(m.UpdatedAt, c.UpdatedAt);
        Assert.Equal(0, c.Version);
        Assert.Equal(m, EntityTypes.Deserialize(c.Type, c.Json));
    }
}
