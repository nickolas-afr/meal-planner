using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Tests;

public class ValidationTests
{
    private static Meal MakeMeal(string name, Guid? id = null, bool deleted = false) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, IsDeleted = deleted };

    private static Ingredient MakeIngredient(string name, Guid? id = null, bool deleted = false) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, IsDeleted = deleted };

    private static Market MakeMarket(string name, Guid? id = null, bool deleted = false) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, IsDeleted = deleted };

    private static string Repeat(char c, int count) => new(c, count);

    // ---- MealName ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MealName_blank_is_an_error(string? name)
    {
        Assert.NotNull(Validation.MealName(name, [], null));
    }

    [Fact]
    public void MealName_80_characters_is_valid()
    {
        Assert.Null(Validation.MealName(Repeat('a', 80), [], null));
    }

    [Fact]
    public void MealName_81_characters_is_an_error()
    {
        Assert.NotNull(Validation.MealName(Repeat('a', 81), [], null));
    }

    [Fact]
    public void MealName_length_is_measured_after_trimming()
    {
        Assert.Null(Validation.MealName(" " + Repeat('a', 80) + " ", [], null));
        Assert.Null(Validation.MealName(" x ", [], null));
    }

    [Fact]
    public void MealName_duplicate_of_a_live_meal_is_an_error_ignoring_case_and_spaces()
    {
        var meals = new[] { MakeMeal("Pasta") };

        Assert.Equal("A meal with this name already exists.", Validation.MealName("pasta", meals, null));
        Assert.NotNull(Validation.MealName("  PASTA ", meals, null));
    }

    [Fact]
    public void MealName_same_name_as_a_deleted_meal_is_valid()
    {
        var meals = new[] { MakeMeal("Pasta", deleted: true) };

        Assert.Null(Validation.MealName("Pasta", meals, null));
    }

    [Fact]
    public void MealName_same_name_as_the_meal_being_edited_is_valid()
    {
        var id = Guid.NewGuid();
        var meals = new[] { MakeMeal("Pasta", id) };

        Assert.Null(Validation.MealName("Pasta", meals, id));
    }

    // ---- IngredientName ----

    [Fact]
    public void IngredientName_blank_is_an_error()
    {
        Assert.Equal("Enter a name.", Validation.IngredientName(" ", [], null));
    }

    [Fact]
    public void IngredientName_60_is_valid_and_61_is_an_error()
    {
        Assert.Null(Validation.IngredientName(Repeat('a', 60), [], null));
        Assert.Equal("Use at most 60 characters.", Validation.IngredientName(Repeat('a', 61), [], null));
    }

    [Fact]
    public void IngredientName_duplicate_is_an_error_unless_deleted_or_being_edited()
    {
        var id = Guid.NewGuid();
        var live = new[] { MakeIngredient("Onion", Guid.NewGuid()) };
        var deleted = new[] { MakeIngredient("Onion", deleted: true) };
        var editing = new[] { MakeIngredient("Onion", id) };

        Assert.Equal("An ingredient with this name already exists.",
            Validation.IngredientName("onion", live, null));
        Assert.Null(Validation.IngredientName("Onion", deleted, null));
        Assert.Null(Validation.IngredientName("Onion", editing, id));
    }

    // ---- MarketName ----

    [Fact]
    public void MarketName_blank_is_an_error()
    {
        Assert.Equal("Enter a name.", Validation.MarketName("", [], null));
    }

    [Fact]
    public void MarketName_40_is_valid_and_41_is_an_error()
    {
        Assert.Null(Validation.MarketName(Repeat('a', 40), [], null));
        Assert.Equal("Use at most 40 characters.", Validation.MarketName(Repeat('a', 41), [], null));
    }

    [Fact]
    public void MarketName_duplicate_is_an_error_unless_deleted_or_being_edited()
    {
        var id = Guid.NewGuid();
        var live = new[] { MakeMarket("Aldi", Guid.NewGuid()) };
        var deleted = new[] { MakeMarket("Aldi", deleted: true) };
        var editing = new[] { MakeMarket("Aldi", id) };

        Assert.Equal("A market with this name already exists.", Validation.MarketName("ALDI", live, null));
        Assert.Null(Validation.MarketName("Aldi", deleted, null));
        Assert.Null(Validation.MarketName("Aldi", editing, id));
    }

    // ---- Quantity ----

    [Theory]
    [InlineData("1.5")]
    [InlineData("2,25")]
    [InlineData("")]
    public void Quantity_valid_input_gives_no_error(string input)
    {
        Assert.Null(Validation.Quantity(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("1.234")]
    public void Quantity_invalid_input_gives_an_error(string input)
    {
        Assert.NotNull(Validation.Quantity(input));
    }

    // ---- Notes ----

    [Fact]
    public void Notes_2000_is_valid_and_2001_is_an_error()
    {
        Assert.Null(Validation.Notes(Repeat('a', 2000)));
        Assert.Equal("Notes can be up to 2000 characters.", Validation.Notes(Repeat('a', 2001)));
    }

    [Fact]
    public void Notes_null_is_valid()
    {
        Assert.Null(Validation.Notes(null));
    }

    // ---- DisplayName ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DisplayName_blank_is_an_error(string? name)
    {
        Assert.Equal("Enter your name.", Validation.DisplayName(name));
    }

    [Fact]
    public void DisplayName_30_is_valid_and_31_is_an_error()
    {
        Assert.Null(Validation.DisplayName(Repeat('a', 30)));
        Assert.Equal("Use at most 30 characters.", Validation.DisplayName(Repeat('a', 31)));
    }

    [Fact]
    public void DisplayName_length_is_measured_after_trimming()
    {
        Assert.Null(Validation.DisplayName("  " + Repeat('a', 30) + "  "));
    }

    // ---- InviteCode ----

    [Theory]
    [InlineData("abcd-efgh")]
    [InlineData("ABCD EFGH")]
    [InlineData("ABCDEFGH")]
    [InlineData("2345-6789")]
    public void InviteCode_valid_codes_have_no_error(string code)
    {
        Assert.Null(Validation.InviteCode(code));
    }

    [Theory]
    [InlineData("ABCDEFG")]     // 7 characters
    [InlineData("ABCDEFGHJ")]   // 9 characters
    [InlineData("ABCDEFGI")]    // contains I
    [InlineData("ABCDEFGL")]    // contains L
    [InlineData("ABCDEFGO")]    // contains O
    [InlineData("ABCDEFGU")]    // contains U
    public void InviteCode_wrong_length_or_forbidden_letter_is_an_error(string code)
    {
        Assert.Equal("Enter the 8-character code.", Validation.InviteCode(code));
    }

    [Fact]
    public void NormalizeInviteCode_uppercases_and_removes_spaces_and_dashes()
    {
        Assert.Equal("ABCDEFGH", Validation.NormalizeInviteCode("ab-cd ef gh"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ItemName_requires_a_name(string? name) => Assert.NotNull(Validation.ItemName(name));

    [Fact]
    public void ItemName_allows_1_to_60_trimmed_characters()
    {
        Assert.Null(Validation.ItemName("a"));
        Assert.Null(Validation.ItemName(new string('a', 60)));
        Assert.Null(Validation.ItemName("  " + new string('a', 60) + "  "));
        Assert.NotNull(Validation.ItemName(new string('a', 61)));
    }
}
