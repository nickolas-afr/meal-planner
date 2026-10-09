using MealPlanner.Core;
using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

public sealed class IngredientService(AppState state, EntityWriter writer, Clock clock)
{
    /// <summary>Live ingredients sorted by name.</summary>
    public IReadOnlyList<Ingredient> All() =>
        state.Ingredients.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public string? ValidateName(string? name, Guid? editingId) =>
        Validation.IngredientName(name, state.Ingredients, editingId);

    /// <summary>Error message for the whole draft, or null. Includes "unit locked" when a used ingredient's unit changes.</summary>
    public string? Validate(IngredientDraft draft)
    {
        if (ValidateName(draft.Name, draft.Id) is { } nameError)
            return nameError;
        if (!Units.IsValid(draft.Unit))
            return "Choose a unit.";
        if (draft.DefaultMarketId is { } marketId && state.Market(marketId) is null)
            return "Choose an existing market.";
        if (draft.Id is { } id && state.Ingredient(id) is { } existing
            && existing.Unit != draft.Unit && IsUsed(id))
            return "The unit can't change while meals use this ingredient.";
        return null;
    }

    /// <summary>Creates or updates the ingredient. Throws InvalidOperationException when <see cref="Validate"/> fails.</summary>
    public async Task<Ingredient> SaveIngredient(IngredientDraft draft)
    {
        if (Validate(draft) is { } error)
            throw new InvalidOperationException(error);

        string name = draft.Name.Trim();
        Ingredient ingredient;
        if (draft.Id is { } id)
        {
            var existing = state.Ingredient(id) ?? throw new InvalidOperationException("This ingredient no longer exists.");
            ingredient = existing with
            {
                Name = name,
                Unit = draft.Unit,
                DefaultMarketId = draft.DefaultMarketId,
                IsStaple = draft.IsStaple,
                UpdatedAt = clock.UtcNow,
            };
        }
        else
        {
            ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = name,
                Unit = draft.Unit,
                DefaultMarketId = draft.DefaultMarketId,
                IsStaple = draft.IsStaple,
                UpdatedAt = clock.UtcNow,
            };
        }

        await writer.SaveAsync(ingredient);
        return ingredient;
    }

    /// <summary>Whether any live meal uses the ingredient.</summary>
    public bool IsUsed(Guid id) => state.Meals.Any(m => m.Ingredients.Any(r => r.IngredientId == id));

    /// <summary>Live meals that use the ingredient, sorted by name.</summary>
    public IReadOnlyList<Meal> UsedBy(Guid id) =>
        state.Meals.Where(m => m.Ingredients.Any(r => r.IngredientId == id))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public bool CanDelete(Guid id) => !IsUsed(id);

    /// <summary>Soft-deletes the ingredient. Throws InvalidOperationException while it is used.</summary>
    public async Task DeleteIngredient(Guid id)
    {
        if (IsUsed(id))
            throw new InvalidOperationException("This ingredient is used by a meal.");
        if (state.Ingredient(id) is not { } ingredient)
            return;
        await writer.SaveAsync(ingredient with { IsDeleted = true, UpdatedAt = clock.UtcNow });
    }
}
