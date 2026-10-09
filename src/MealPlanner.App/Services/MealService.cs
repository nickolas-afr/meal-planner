using MealPlanner.Core;
using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

public sealed class MealService(AppState state, EntityWriter writer, Clock clock)
{
    /// <summary>Live meals sorted by name.</summary>
    public IReadOnlyList<Meal> All() => state.Meals.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Error message for the name field, or null.</summary>
    public string? ValidateName(string? name, Guid? editingId) => Validation.MealName(name, state.Meals, editingId);

    /// <summary>Error message for the whole draft (name, notes, ingredients), or null.</summary>
    public string? Validate(MealDraft draft)
    {
        if (ValidateName(draft.Name, draft.Id) is { } nameError)
            return nameError;
        if (Validation.Notes(draft.Notes) is { } notesError)
            return notesError;

        var seen = new HashSet<Guid>();
        foreach (var row in draft.Ingredients)
        {
            if (state.Ingredient(row.IngredientId) is null)
                return "An ingredient in this meal no longer exists.";
            if (!seen.Add(row.IngredientId))
                return "An ingredient can be added only once.";
            if (row.Quantity is { } q && (q <= 0 || q > 100000m || decimal.Round(q, 2) != q))
                return "Enter a number above 0, up to 100000, with at most 2 decimals.";
        }
        return null;
    }

    /// <summary>Creates or updates the meal. Throws InvalidOperationException when <see cref="Validate"/> fails.</summary>
    public async Task<Meal> SaveMeal(MealDraft draft)
    {
        if (Validate(draft) is { } error)
            throw new InvalidOperationException(error);

        string name = draft.Name.Trim();
        string? notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes;
        var ingredients = draft.Ingredients.ToList();

        Meal meal;
        if (draft.Id is { } id)
        {
            var existing = state.Meal(id) ?? throw new InvalidOperationException("This meal no longer exists.");
            meal = existing with { Name = name, Notes = notes, Ingredients = ingredients, UpdatedAt = clock.UtcNow };
        }
        else
        {
            meal = new Meal
            {
                Id = Guid.NewGuid(),
                Name = name,
                Notes = notes,
                Ingredients = ingredients,
                UpdatedAt = clock.UtcNow,
            };
        }

        await writer.SaveAsync(meal);
        return meal;
    }

    /// <summary>The current and future weeks the meal is planned in (for the delete dialog).</summary>
    public IReadOnlyList<DateOnly> PlannedWeeksFromNow(Guid mealId) =>
        CurrentAndFuturePlanned(mealId).Select(p => p.WeekStart).Distinct().Order().ToList();

    /// <summary>Whether the meal is planned in the active week.</summary>
    public bool IsPlannedInActiveWeek(Guid mealId)
    {
        var active = Weeks.Active(clock.LocalNow);
        return state.PlannedMeals.Any(p => p.MealId == mealId && p.WeekStart == active);
    }

    /// <summary>Soft-deletes the meal and its PlannedMeals in the current and future weeks.</summary>
    public async Task DeleteMeal(Guid id)
    {
        var meal = state.Meal(id);
        if (meal is null)
            return;

        var now = clock.UtcNow;
        var changed = new List<Entity> { meal with { IsDeleted = true, UpdatedAt = now } };
        changed.AddRange(CurrentAndFuturePlanned(id).Select(p => p with { IsDeleted = true, UpdatedAt = now }));
        await writer.SaveAsync(changed);
    }

    private List<PlannedMeal> CurrentAndFuturePlanned(Guid mealId)
    {
        var thisWeek = Weeks.StartOf(DateOnly.FromDateTime(clock.LocalNow));
        return state.PlannedMeals.Where(p => p.MealId == mealId && p.WeekStart >= thisWeek).ToList();
    }
}
