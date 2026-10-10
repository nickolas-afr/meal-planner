namespace MealPlanner.Api.Options;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Base64 HMAC key.</summary>
    public string JwtKey { get; set; } = "";
    public string RpId { get; set; } = "";
    public string[] Origins { get; set; } = [];
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 180;

    /// <summary>True when the key is valid base64 of at least 32 bytes (HMAC-SHA256 needs 256 bits).</summary>
    public static bool IsValidJwtKey(string? key)
    {
        var buffer = new byte[Math.Max(key?.Length ?? 0, 1)];
        return Convert.TryFromBase64String(key ?? "", buffer, out int bytes) && bytes >= 32;
    }
}

public sealed class CorsOptions
{
    public const string Section = "Cors";

    public string[] Origins { get; set; } = [];
}
