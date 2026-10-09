using MealPlanner.Core;
using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

/// <summary>A meal planned in a week and how many times.</summary>
public sealed record PlannedMealGroup(Meal Meal, int Count);

public sealed class PlanService(AppState state, EntityWriter writer, Clock clock)
{
    public DateOnly CurrentWeek => Weeks.StartOf(DateOnly.FromDateTime(clock.LocalNow));

    public DateOnly ActiveWeek => Weeks.Active(clock.LocalNow);

    /// <summary>Past weeks can't be edited.</summary>
    public bool IsReadOnly(DateOnly week) => week < CurrentWeek;

    public bool CanGoTo(DateOnly week) => week >= Weeks.PlanMin(clock.LocalNow) && week <= Weeks.PlanMax(clock.LocalNow);

    /// <summary>One group per distinct live meal planned in the week, by earliest AddedAt.</summary>
    public IReadOnlyList<PlannedMealGroup> Groups(DateOnly week) =>
        state.PlannedMeals
            .Where(p => p.WeekStart == week)
            .GroupBy(p => p.MealId)
            .Select(g => (Meal: state.Meal(g.Key), Count: g.Count(), First: g.Min(p => p.AddedAt)))
            .Where(g => g.Meal is not null)
            .OrderBy(g => g.First)
            .Select(g => new PlannedMealGroup(g.Meal!, g.Count))
            .ToList();

    public int CountFor(DateOnly week, Guid mealId) =>
        state.Meal(mealId) is null ? 0 : state.PlannedMeals.Count(p => p.WeekStart == week && p.MealId == mealId);

    /// <summary>All live entries for live meals.</summary>
    public int EntryCount(DateOnly week) =>
        state.PlannedMeals.Count(p => p.WeekStart == week && state.Meal(p.MealId) is not null);

    /// <summary>Live meals with no entry in the week, by name.</summary>
    public IReadOnlyList<Meal> Unplanned(DateOnly week)
    {
        var planned = state.PlannedMeals.Where(p => p.WeekStart == week).Select(p => p.MealId).ToHashSet();
        return state.Meals.Where(m => !planned.Contains(m.Id))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Adds one entry. Throws InvalidOperationException for a read-only or out-of-range week, or a missing meal.</summary>
    public async Task Add(DateOnly week, Guid mealId)
    {
        if (IsReadOnly(week))
            throw new InvalidOperationException("Past weeks can't be changed.");
        if (!CanGoTo(week))
            throw new InvalidOperationException("This week is out of range.");
        if (state.Meal(mealId) is null)
            throw new InvalidOperationException("This meal no longer exists.");

        var now = clock.UtcNow;
        await writer.SaveAsync(new PlannedMeal
        {
            Id = Guid.NewGuid(),
            WeekStart = week,
            MealId = mealId,
            AddedAt = now,
            UpdatedAt = now,
        });
    }

    /// <summary>Soft-deletes the newest entry of the meal in the week.</summary>
    public async Task RemoveOne(DateOnly week, Guid mealId)
    {
        if (IsReadOnly(week))
            throw new InvalidOperationException("Past weeks can't be changed.");

        var newest = state.PlannedMeals.Where(p => p.WeekStart == week && p.MealId == mealId)
            .OrderByDescending(p => p.AddedAt).FirstOrDefault();
        if (newest is null)
            return;
        await writer.SaveAsync(newest with { IsDeleted = true, UpdatedAt = clock.UtcNow });
    }

    /// <summary>Soft-deletes plans, lists and items for weeks before the retention cutoff.</summary>
    public async Task PruneAsync()
    {
        var cutoff = Weeks.RetentionCutoff(clock.LocalNow);
        var now = clock.UtcNow;
        var changed = new List<Entity>();
        changed.AddRange(state.PlannedMeals.Where(p => p.WeekStart < cutoff).Select(p => p with { IsDeleted = true, UpdatedAt = now }));
        changed.AddRange(state.ShoppingLists.Where(l => l.WeekStart < cutoff).Select(l => l with { IsDeleted = true, UpdatedAt = now }));
        changed.AddRange(state.ShoppingItems.Where(i => i.WeekStart < cutoff).Select(i => i with { IsDeleted = true, UpdatedAt = now }));
        await writer.SaveAsync(changed);
    }
}
