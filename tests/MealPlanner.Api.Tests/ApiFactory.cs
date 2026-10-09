using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Headers;
using MealPlanner.Api.Auth;
using MealPlanner.Api.Data;
using MealPlanner.Core.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealPlanner.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:JwtKey", Convert.ToBase64String(new byte[64].Select((_, i) => (byte)(i + 1)).ToArray()));
        builder.UseSetting("Auth:RpId", "localhost");
        builder.UseSetting("Auth:Origins:0", "https://localhost:7002");
        builder.UseSetting("Cors:Origins:0", "https://localhost:7002");
        builder.UseSetting("ConnectionStrings:Db", "DataSource=:memory:");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(DbContextOptions<AppDb>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(AppDb)) && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration"))).ToList())
                services.Remove(d);
            services.AddDbContext<AppDb>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    public async Task<T> WithDb<T>(Func<AppDb, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDb>());
    }

    public Task WithDb(Func<AppDb, Task> action) => WithDb<int>(async db => { await action(db); return 0; });

    /// <summary>Creates a household with <paramref name="members"/> users; the first is returned with real tokens.</summary>
    public async Task<TestUser> CreateHousehold(int members = 1, string? firstName = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var household = new Household { Id = Guid.NewGuid(), Name = "House " + Guid.NewGuid().ToString("N")[..6], CreatedAt = DateTime.UtcNow };
        db.Households.Add(household);
        User? first = null;
        for (int i = 0; i < members; i++)
        {
            var u = new User
            {
                Id = Guid.NewGuid(),
                HouseholdId = household.Id,
                DisplayName = i == 0 ? firstName ?? "Alex" : $"Member{i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i),
            };
            db.Users.Add(u);
            first ??= u;
        }
        await db.SaveChangesAsync();
        TokenPair tokens = await scope.ServiceProvider.GetRequiredService<TokenService>().IssueAsync(first!);
        return new TestUser(first!.Id, household.Id, first.DisplayName, tokens);
    }

    public HttpClient ClientFor(TestUser? user = null)
    {
        HttpClient client = CreateClient();
        if (user is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Tokens.AccessToken);
        return client;
    }
}

public sealed record TestUser(Guid UserId, Guid HouseholdId, string DisplayName, TokenPair Tokens);
