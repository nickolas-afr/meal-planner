using System.Text.Json;
using Blazored.LocalStorage;
using MealPlanner.Core;
using MealPlanner.Core.Shopping;

namespace MealPlanner.App.Services;

/// <summary>Per-phone preferences, not synced.</summary>
public sealed class PreferencesService(ILocalStorageService localStorage)
{
    public const string Key = "mealplanner.prefs";

    private Preferences _prefs = new();

    public ShoppingView ShoppingView => _prefs.ShoppingView;
    public DateTime? LastExportAt => _prefs.LastExportAt;

    public async Task LoadAsync()
    {
        string? json = await localStorage.GetItemAsStringAsync(Key);
        _prefs = string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<Preferences>(json, CoreJson.Options) ?? new();
    }

    public Task SetShoppingViewAsync(ShoppingView view) => SaveAsync(_prefs with { ShoppingView = view });

    public Task SetLastExportAtAsync(DateTime utc) => SaveAsync(_prefs with { LastExportAt = utc });

    private async Task SaveAsync(Preferences prefs)
    {
        _prefs = prefs;
        await localStorage.SetItemAsStringAsync(Key, JsonSerializer.Serialize(prefs, CoreJson.Options));
    }

    private sealed record Preferences
    {
        public ShoppingView ShoppingView { get; init; } = ShoppingView.ByMeal;
        public DateTime? LastExportAt { get; init; }
    }
}
