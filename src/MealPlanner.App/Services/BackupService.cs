using MealPlanner.Core.Backup;
using MealPlanner.Core.Entities;
using Microsoft.JSInterop;

namespace MealPlanner.App.Services;

/// <summary>Counts shown in the import confirm dialog.</summary>
public sealed record BackupPreview(BackupFile File, int Meals, int Ingredients, int Markets);

/// <summary>Settings › Backup: export to a .json file and import (replace everything).</summary>
public sealed class BackupService(AppState state, EntityWriter writer, Clock clock, PreferencesService prefs, IJSRuntime js)
{
    /// <summary>Downloads meal-planner-backup-YYYY-MM-DD.json with every live entity, and records the export time in prefs.</summary>
    public async Task ExportAsync()
    {
        var d = state.Data;
        var file = BackupFile.Create(clock.UtcNow, d.Markets.Values, d.Ingredients.Values, d.Meals.Values,
            d.PlannedMeals.Values, d.ShoppingLists.Values, d.ShoppingItems.Values);
        await js.InvokeVoidAsync("mealPlanner.downloadFile", BackupFile.FileName(DateOnly.FromDateTime(clock.LocalNow)),
            "application/json", file.Serialize());
        await prefs.SetLastExportAtAsync(clock.UtcNow);
    }

    /// <summary>Parses a backup file's text. Null when it isn't a readable backup.</summary>
    public BackupPreview? Preview(string json)
    {
        var file = BackupFile.Deserialize(json);
        if (file is null) return null;
        return new BackupPreview(file,
            file.Meals.Count(e => !e.IsDeleted), file.Ingredients.Count(e => !e.IsDeleted), file.Markets.Count(e => !e.IsDeleted));
    }

    /// <summary>
    /// Replaces the household's data with the file: every entity in the file is saved live (UpdatedAt = now),
    /// every live local entity not in the file is soft-deleted. One save, so it all goes through the outbox.
    /// </summary>
    public Task ImportAsync(BackupFile file)
    {
        var now = clock.UtcNow;
        var d = state.Data;
        var all = new List<Entity>();
        Merge(file.Markets, d.Markets.Values);
        Merge(file.Ingredients, d.Ingredients.Values);
        Merge(file.Meals, d.Meals.Values);
        Merge(file.PlannedMeals, d.PlannedMeals.Values);
        Merge(file.ShoppingLists, d.ShoppingLists.Values);
        Merge(file.ShoppingItems, d.ShoppingItems.Values);
        return writer.SaveAsync(all);

        void Merge<T>(IEnumerable<T> incoming, IEnumerable<T> current) where T : Entity
        {
            var list = incoming.ToList();
            var ids = list.Select(e => e.Id).ToHashSet();
            foreach (var e in list)
                all.Add(e with { UpdatedAt = now, IsDeleted = false });
            foreach (var e in current.ToList())
                if (!e.IsDeleted && !ids.Contains(e.Id))
                    all.Add(e with { IsDeleted = true, UpdatedAt = now });
        }
    }
}
