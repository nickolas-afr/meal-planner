using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Passkey> Passkeys => Set<Passkey>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<EntityRow> Entities => Set<EntityRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Household>().HasKey(x => x.Id);
        b.Entity<User>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.HouseholdId); });
        b.Entity<Passkey>(e => { e.HasKey(x => x.CredentialId); e.HasIndex(x => x.UserId); });
        b.Entity<RefreshToken>().HasKey(x => x.TokenHash);
        b.Entity<Invite>().HasKey(x => x.Code);
        b.Entity<EntityRow>(e =>
        {
            e.ToTable("Entities");
            e.HasKey(x => new { x.HouseholdId, x.Id });
            e.HasIndex(x => new { x.HouseholdId, x.Version });
        });
    }
}
