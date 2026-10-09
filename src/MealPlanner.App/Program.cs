using Blazored.LocalStorage;
using MealPlanner.App;
using MealPlanner.App.Services;
using MealPlanner.App.Storage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var features = builder.Configuration.GetSection("Features").Get<Features>() ?? new Features();
builder.Services.AddSingleton(features);

builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddSingleton<Clock>();
builder.Services.AddSingleton<IStore, LocalStorageStore>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<EntityWriter>();
builder.Services.AddSingleton<PreferencesService>();
builder.Services.AddSingleton<MealService>();
builder.Services.AddSingleton<IngredientService>();
builder.Services.AddSingleton<MarketService>();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<PlanService>();
builder.Services.AddSingleton<ShoppingService>();

builder.Services.AddSingleton<ConnectivityService>();
builder.Services.AddSingleton<AuthService>();

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";
// Two API clients: "auth" has no handler (auth/* and refresh, so refreshing can't recurse); "api" attaches the bearer token.
builder.Services.AddKeyedSingleton<HttpClient>(AuthService.AuthClientKey, (_, _) => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddKeyedSingleton<HttpClient>(AuthService.ApiClientKey, (sp, _) =>
    new HttpClient(new TokenHandler(sp.GetRequiredService<AuthService>()) { InnerHandler = new HttpClientHandler() })
    { BaseAddress = new Uri(apiBase) });

// SyncService only runs when RequireAuth is on; otherwise changes stay in the outbox.
if (features.RequireAuth)
{
    builder.Services.AddSingleton<SyncService>();
    builder.Services.AddSingleton<ISyncTrigger>(sp => sp.GetRequiredService<SyncService>());
}
else
{
    builder.Services.AddSingleton<ISyncTrigger, NoSyncTrigger>();
}

var host = builder.Build();

var state = host.Services.GetRequiredService<AppState>();
state.Load(await host.Services.GetRequiredService<IStore>().LoadAsync() ?? new AppData());
await host.Services.GetRequiredService<PreferencesService>().LoadAsync();
await host.Services.GetRequiredService<AuthService>().LoadAsync();
await host.Services.GetRequiredService<ConnectivityService>().InitializeAsync();

await host.Services.GetRequiredService<PlanService>().PruneAsync();

await host.Services.GetRequiredService<UpdateService>().InitializeAsync();

if (features.RequireAuth)
    await host.Services.GetRequiredService<SyncService>().StartAsync();

await host.RunAsync();
