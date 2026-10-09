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
}

public sealed class CorsOptions
{
    public const string Section = "Cors";

    public string[] Origins { get; set; } = [];
}
