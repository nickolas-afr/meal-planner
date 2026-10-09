using System.Text.Json;
using Fido2NetLib;
using MealPlanner.Api.Auth;
using MealPlanner.Api.Data;
using MealPlanner.Api.Endpoints;
using MealPlanner.Api.Options;
using MealPlanner.Core.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
builder.Services.Configure<MealPlanner.Api.Options.CorsOptions>(builder.Configuration.GetSection(CorsOptions.Section));

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Db")));
builder.Services.AddMemoryCache();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<MealPlanner.Api.Sync.SyncHandler>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddSingleton<IFido2>(sp =>
{
    var o = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
    return new Fido2(new Fido2Configuration
    {
        RPID = o.RpId,
        RPName = "Meal Planner",
        Origins = o.Origins.ToHashSet(),
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
// Configured lazily so tests (and the host) can override settings after this point.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>((o, auth) =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            IssuerSigningKey = TokenService.SigningKey(auth.Value),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>().Configure<IOptions<MealPlanner.Api.Options.CorsOptions>>((o, cors) =>
    o.AddDefaultPolicy(p => p
        .WithOrigins(cors.Value.Origins)
        .WithMethods("GET", "POST")
        .WithHeaders("Authorization", "Content-Type")));

var app = builder.Build();

app.Services.InitializeDatabase();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.AddEndpointFilter(async (ctx, next) =>
{
    try
    {
        return await next(ctx);
    }
    catch (ApiException e)
    {
        return Results.Json(new ErrorResponse(e.Code), statusCode: e.Status);
    }
    catch (BadHttpRequestException)
    {
        return Results.Json(new ErrorResponse(AuthErrors.InvalidRequest), statusCode: 400);
    }
});
api.MapHealth();
api.MapAuth();
api.MapHousehold();
api.MapSync();

app.Run();

public static class StartupExtensions
{
    /// <summary>Creates the data folder, applies migrations and switches SQLite off WAL.</summary>
    public static void InitializeDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        string? dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (!string.IsNullOrEmpty(dataSource) && !dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(Path.GetFullPath(dataSource)) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
    }
}

public partial class Program;
