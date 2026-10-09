using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using MealPlanner.Api.Data;
using MealPlanner.Api.Options;
using MealPlanner.Core.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanner.Api.Auth;

public sealed class TokenService(AppDb db, IOptions<AuthOptions> options)
{
    private readonly AuthOptions _o = options.Value;

    public static SymmetricSecurityKey SigningKey(AuthOptions o) => new(Convert.FromBase64String(o.JwtKey));

    public async Task<TokenPair> IssueAsync(User user)
    {
        DateTime now = DateTime.UtcNow;
        DateTime expires = now.AddMinutes(_o.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(
            claims: [new Claim("sub", user.Id.ToString()), new Claim("hid", user.HouseholdId.ToString())],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(SigningKey(_o), SecurityAlgorithms.HmacSha256));
        string access = new JwtSecurityTokenHandler().WriteToken(jwt);

        string refresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = Hash(refresh),
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_o.RefreshTokenDays),
        });
        await db.SaveChangesAsync();
        return new TokenPair(access, expires, refresh);
    }

    /// <summary>Rotates the token; null when it is unknown, revoked or expired.</summary>
    public async Task<TokenPair?> RefreshAsync(string? token)
    {
        RefreshToken? row = await Find(token);
        if (row is null || row.RevokedAt is not null || row.ExpiresAt <= DateTime.UtcNow)
            return null;
        User? user = await db.Users.FindAsync(row.UserId);
        if (user is null)
            return null;
        row.RevokedAt = DateTime.UtcNow;
        return await IssueAsync(user); // saves the revocation too
    }

    public async Task RevokeAsync(string? token)
    {
        RefreshToken? row = await Find(token);
        if (row is { RevokedAt: null })
        {
            row.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task<RefreshToken?> Find(string? token) =>
        string.IsNullOrEmpty(token) ? null : await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(token));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
