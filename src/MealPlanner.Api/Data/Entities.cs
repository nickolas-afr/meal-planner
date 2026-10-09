namespace MealPlanner.Api.Data;

public sealed class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public long Version { get; set; }
    public long PurgedThroughVersion { get; set; }
    public DateTime? LastPurgeAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class User
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class Passkey
{
    public byte[] CredentialId { get; set; } = [];
    public Guid UserId { get; set; }
    public byte[] PublicKey { get; set; } = [];
    public long SignCount { get; set; }
    public byte[] UserHandle { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public sealed class RefreshToken
{
    /// <summary>SHA-256 hex of the token.</summary>
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class Invite
{
    public string Code { get; set; } = "";
    public Guid HouseholdId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? UsedBy { get; set; }
}

public sealed class EntityRow
{
    public Guid HouseholdId { get; set; }
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Json { get; set; } = "";
    public bool IsDeleted { get; set; }
    public long Version { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}
