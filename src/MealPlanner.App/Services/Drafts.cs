using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

/// <summary>What the meal editor hands to <see cref="MealService.SaveMeal"/>. Id is null for a new meal.</summary>
public sealed record MealDraft(Guid? Id, string Name, string? Notes, IReadOnlyList<MealIngredient> Ingredients);

/// <summary>What the ingredient sheet hands to <see cref="IngredientService.SaveIngredient"/>. Id is null for a new ingredient.</summary>
public sealed record IngredientDraft(Guid? Id, string Name, string Unit, IReadOnlyList<Guid> MarketIds, bool IsStaple);
