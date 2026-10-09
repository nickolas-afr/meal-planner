using System.Text.Json;
using Blazored.LocalStorage;
using MealPlanner.Core;

namespace MealPlanner.App.Storage;

/// <summary>Keeps the whole <see cref="AppData"/> as one JSON value in localStorage.</summary>
public sealed class LocalStorageStore(ILocalStorageService localStorage) : IStore
{
    public const string Key = "mealplanner.data";

    public async Task<AppData?> LoadAsync()
    {
        string? json = await localStorage.GetItemAsStringAsync(Key);
        if (string.IsNullOrEmpty(json))
            return null;
        return JsonSerializer.Deserialize<AppData>(json, CoreJson.Options);
    }

    public Task SaveAsync(AppData data) =>
        localStorage.SetItemAsStringAsync(Key, JsonSerializer.Serialize(data, CoreJson.Options)).AsTask();
}
