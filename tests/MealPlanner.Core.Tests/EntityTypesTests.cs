using MealPlanner.Core.Entities;
using MealPlanner.Core.Sync;

namespace MealPlanner.Core.Tests;

public class EntityTypesTests
{
    [Fact]
    public void Type_names_map_to_their_records()
    {
        Assert.Equal(6, EntityTypes.All.Count);
        Assert.Equal(typeof(Market), EntityTypes.All[EntityTypes.Market]);
        Assert.Equal(typeof(Ingredient), EntityTypes.All[EntityTypes.Ingredient]);
        Assert.Equal(typeof(Meal), EntityTypes.All[EntityTypes.Meal]);
        Assert.Equal(typeof(PlannedMeal), EntityTypes.All[EntityTypes.PlannedMeal]);
        Assert.Equal(typeof(ShoppingList), EntityTypes.All[EntityTypes.ShoppingList]);
        Assert.Equal(typeof(ShoppingItem), EntityTypes.All[EntityTypes.ShoppingItem]);
    }

    [Fact]
    public void Type_names_are_the_record_names()
    {
        Assert.Equal("Market", EntityTypes.Market);
        Assert.Equal("Ingredient", EntityTypes.Ingredient);
        Assert.Equal("Meal", EntityTypes.Meal);
        Assert.Equal("PlannedMeal", EntityTypes.PlannedMeal);
        Assert.Equal("ShoppingList", EntityTypes.ShoppingList);
        Assert.Equal("ShoppingItem", EntityTypes.ShoppingItem);
    }

    [Fact]
    public void IsKnown_is_true_only_for_the_six_types()
    {
        Assert.True(EntityTypes.IsKnown("Meal"));
        Assert.False(EntityTypes.IsKnown("Recipe"));
    }

    [Fact]
    public void NameOf_returns_the_record_name()
    {
        var meal = new Meal { Id = Guid.NewGuid(), Name = "Soup" };

        Assert.Equal(EntityTypes.Meal, EntityTypes.NameOf(meal));
    }

    [Fact]
    public void Serialize_then_Deserialize_round_trips_a_meal_with_ingredients()
    {
        var meal = new Meal
        {
            Id = new Guid("33333333-3333-3333-3333-333333333333"),
            Name = "Pasta bake",
            Notes = "Bake 20 min",
            UpdatedAt = DateTime.SpecifyKind(new DateTime(2026, 10, 8, 9, 30, 0), DateTimeKind.Utc),
            IsDeleted = false,
            Version = 7,
            Ingredients =
            [
                new MealIngredient(new Guid("44444444-4444-4444-4444-444444444444"), 200.5m),
                new MealIngredient(new Guid("55555555-5555-5555-5555-555555555555"), null),
            ],
        };

        string json = EntityTypes.Serialize(meal);
        var back = Assert.IsType<Meal>(EntityTypes.Deserialize(EntityTypes.Meal, json));

        Assert.Equal(meal.Id, back.Id);
        Assert.Equal(meal.Name, back.Name);
        Assert.Equal(meal.Notes, back.Notes);
        Assert.Equal(meal.UpdatedAt, back.UpdatedAt);
        Assert.Equal(meal.IsDeleted, back.IsDeleted);
        Assert.Equal(meal.Version, back.Version);
        Assert.Equal(meal.Ingredients, back.Ingredients);
    }

    [Fact]
    public void Deserialize_unknown_type_returns_null()
    {
        Assert.Null(EntityTypes.Deserialize("Recipe", "{}"));
    }

    [Fact]
    public void Deserialize_malformed_json_returns_null()
    {
        Assert.Null(EntityTypes.Deserialize(EntityTypes.Meal, "{not json"));
    }
}
