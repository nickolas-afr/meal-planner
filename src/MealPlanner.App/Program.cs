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
builder.Services.AddSingleton<PlanService>();
builder.Services.AddSingleton<ShoppingService>();

// SyncService replaces this in M6, and only when RequireAuth is on.
builder.Services.AddSingleton<ISyncTrigger, NoSyncTrigger>();

var host = builder.Build();

var state = host.Services.GetRequiredService<AppState>();
state.Load(await host.Services.GetRequiredService<IStore>().LoadAsync() ?? new AppData());
await host.Services.GetRequiredService<PreferencesService>().LoadAsync();

await host.Services.GetRequiredService<PlanService>().PruneAsync();

await host.RunAsync();
