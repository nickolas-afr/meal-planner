using MealPlanner.Core.Backup;
using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Tests;

public class BackupFileTests
{
    private static readonly DateTime Exported = DateTime.SpecifyKind(new DateTime(2026, 10, 9, 18, 0, 0), DateTimeKind.Utc);

    [Fact]
    public void Create_drops_deleted_entities()
    {
        var file = BackupFile.Create(
            Exported,
            markets: [new Market { Id = Guid.NewGuid(), Name = "Aldi" }, new Market { Id = Guid.NewGuid(), Name = "Old", IsDeleted = true }],
            ingredients: [new Ingredient { Id = Guid.NewGuid(), Name = "Rice" }, new Ingredient { Id = Guid.NewGuid(), Name = "Gone", IsDeleted = true }],
            meals: [new Meal { Id = Guid.NewGuid(), Name = "Risotto" }, new Meal { Id = Guid.NewGuid(), Name = "Gone", IsDeleted = true }],
            plannedMeals: [new PlannedMeal { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), MealId = Guid.NewGuid(), AddedAt = Exported, IsDeleted = true }],
            shoppingLists: [new ShoppingList { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), GeneratedAt = Exported }],
            shoppingItems: [new ShoppingItem { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), Name = "Gone", IsDeleted = true }]);

        Assert.Single(file.Markets);
        Assert.Single(file.Ingredients);
        Assert.Single(file.Meals);
        Assert.Empty(file.PlannedMeals);
        Assert.Single(file.ShoppingLists);
        Assert.Empty(file.ShoppingItems);
        Assert.Equal("Aldi", file.Markets[0].Name);
        Assert.Equal(BackupFile.CurrentSchemaVersion, file.SchemaVersion);
        Assert.Equal(Exported, file.ExportedAt);
    }

    [Fact]
    public void Serialize_then_Deserialize_keeps_every_entity()
    {
        var mealId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var file = BackupFile.Create(
            Exported,
            markets: [new Market { Id = Guid.NewGuid(), Name = "Aldi", SortOrder = 2 }],
            ingredients: [new Ingredient { Id = ingredientId, Name = "Flour", Unit = "g" }, new Ingredient { Id = Guid.NewGuid(), Name = "Eggs", Unit = "pc" }],
            meals: [new Meal { Id = mealId, Name = "Pancakes", Ingredients = [new MealIngredient(ingredientId, 250.25m)] }],
            plannedMeals:
            [
                new PlannedMeal { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), MealId = mealId, AddedAt = Exported },
                new PlannedMeal { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 12), MealId = mealId, AddedAt = Exported },
            ],
            shoppingLists: [new ShoppingList { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), GeneratedAt = Exported }],
            shoppingItems: [new ShoppingItem { Id = Guid.NewGuid(), WeekStart = new DateOnly(2026, 10, 5), Name = "Flour", Quantity = 1.5m, Unit = "kg" }]);

        string json = file.Serialize();
        var back = BackupFile.Deserialize(json);

        Assert.NotNull(back);
        Assert.Single(back.Markets);
        Assert.Equal(2, back.Ingredients.Count);
        Assert.Single(back.Meals);
        Assert.Equal(2, back.PlannedMeals.Count);
        Assert.Single(back.ShoppingLists);
        Assert.Single(back.ShoppingItems);

        Assert.Equal("Aldi", back.Markets[0].Name);
        Assert.Equal(2, back.Markets[0].SortOrder);
        Assert.Equal("Pancakes", back.Meals[0].Name);
        Assert.Equal(new[] { new MealIngredient(ingredientId, 250.25m) }, back.Meals[0].Ingredients);
        Assert.Equal(new DateOnly(2026, 10, 12), back.PlannedMeals[1].WeekStart);
        Assert.Equal(new DateOnly(2026, 10, 5), back.ShoppingItems[0].WeekStart);
        Assert.Equal<decimal?>(1.5m, back.ShoppingItems[0].Quantity);
        Assert.Equal("kg", back.ShoppingItems[0].Unit);
        Assert.Equal(Exported, back.ExportedAt);
    }

    [Fact]
    public void Deserialize_garbage_returns_null()
    {
        Assert.Null(BackupFile.Deserialize("this is not a backup"));
        Assert.Null(BackupFile.Deserialize("{"));
    }

    [Fact]
    public void Deserialize_schema_version_2_returns_null()
    {
        var file = new BackupFile { SchemaVersion = 2, ExportedAt = Exported };

        Assert.Null(BackupFile.Deserialize(file.Serialize()));
    }

    [Fact]
    public void FileName_includes_the_date()
    {
        Assert.Equal("meal-planner-backup-2026-10-09.json", BackupFile.FileName(new DateOnly(2026, 10, 9)));
    }
}
