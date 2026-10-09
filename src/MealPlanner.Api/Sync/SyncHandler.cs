using System.Text;
using MealPlanner.Api.Auth;
using MealPlanner.Api.Data;
using MealPlanner.Core.Auth;
using MealPlanner.Core.Entities;
using MealPlanner.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Sync;

/// <summary>Validate, apply, purge, respond (architecture › Sync protocol).</summary>
public sealed class SyncHandler(AppDb db, TimeProvider time)
{
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromDays(60);

    public async Task<SyncResponse> SyncAsync(Guid householdId, Guid userId, SyncRequest? request, CancellationToken ct)
    {
        if (request is null)
            throw Invalid();
        Validate(request);
        DateTime now = time.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Take SQLite's write lock before reading, so concurrent syncs serialise instead of reusing a version.
        await db.Database.ExecuteSqlAsync($"UPDATE Households SET Version = Version WHERE Id = {householdId}", ct);
        Household household = await db.Households.FirstOrDefaultAsync(h => h.Id == householdId, ct)
            ?? throw new ApiException(401, AuthErrors.InvalidToken);

        foreach (SyncChange c in request.Changes)
        {
            household.Version++;
            DateTime updatedAt = c.UpdatedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(c.UpdatedAt, DateTimeKind.Utc)
                : c.UpdatedAt.ToUniversalTime();
            EntityRow? row = await db.Entities.FindAsync([householdId, c.Id], ct);
            if (row is null)
            {
                row = new EntityRow { HouseholdId = householdId, Id = c.Id };
                db.Entities.Add(row);
            }
            row.Type = c.Type;
            row.Json = c.Json;
            row.IsDeleted = c.IsDeleted;
            row.Version = household.Version;
            row.UpdatedAt = updatedAt;
            row.UpdatedBy = userId;
        }
        await db.SaveChangesAsync(ct);

        if (household.LastPurgeAt is null || now - household.LastPurgeAt.Value >= PurgeInterval)
        {
            DateTime cutoff = now - TombstoneRetention;
            var old = db.Entities.Where(e => e.HouseholdId == householdId && e.IsDeleted && e.UpdatedAt < cutoff);
            long? highest = await old.MaxAsync(e => (long?)e.Version, ct);
            if (highest is not null)
            {
                await old.ExecuteDeleteAsync(ct);
                household.PurgedThroughVersion = Math.Max(household.PurgedThroughVersion, highest.Value);
            }
            household.LastPurgeAt = now;
            await db.SaveChangesAsync(ct);
        }

        bool full = request.Since < household.PurgedThroughVersion;
        var rows = db.Entities.AsNoTracking().Where(e => e.HouseholdId == householdId);
        rows = full ? rows.Where(e => !e.IsDeleted) : rows.Where(e => e.Version > request.Since);
        List<EntityRow> result = await rows.OrderBy(e => e.Version).ToListAsync(ct);

        await tx.CommitAsync(ct);

        return new SyncResponse(
            household.Version,
            full,
            result.Select(e => new SyncChange(e.Type, e.Id, e.Json, e.IsDeleted,
                DateTime.SpecifyKind(e.UpdatedAt, DateTimeKind.Utc), e.Version)).ToList());
    }

    private static void Validate(SyncRequest request)
    {
        if (request.Since < 0 || request.Changes is null || request.Changes.Count > SyncLimits.MaxChanges)
            throw Invalid();
        foreach (SyncChange c in request.Changes)
        {
            if (c is null || c.Type is null || c.Json is null || !EntityTypes.IsKnown(c.Type)
                || Encoding.UTF8.GetByteCount(c.Json) > SyncLimits.MaxJsonBytes)
                throw Invalid();
            Entity? entity = EntityTypes.Deserialize(c.Type, c.Json);
            if (entity is null || entity.Id != c.Id)
                throw Invalid();
        }
    }

    private static ApiException Invalid() => new(400, AuthErrors.InvalidRequest);
}
