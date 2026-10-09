namespace MealPlanner.Core.Tests;

public class IdsTests
{
    private static readonly Guid IngredientA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IngredientB = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void NameBased_matches_the_RFC_test_vector()
    {
        // RFC 4122 DNS namespace, name "www.example.com" (SHA-1 based UUIDv5 example).
        Guid id = Ids.NameBased(new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8"), "www.example.com");

        Assert.Equal(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"), id);
    }

    [Fact]
    public void NameBased_sets_version_5_and_RFC_variant()
    {
        string text = Ids.NameBased(Ids.Namespace, "anything").ToString();

        Assert.Equal('5', text[14]);
        Assert.Contains(text[19], "89ab");
    }

    [Fact]
    public void ForItem_matches_the_expected_value()
    {
        Assert.Equal(new Guid("52be2672-774c-5aa4-90bf-558473eb6842"),
            Ids.ForItem(new DateOnly(2026, 10, 5), IngredientA));
    }

    [Fact]
    public void ForList_matches_the_expected_value()
    {
        Assert.Equal(new Guid("7a48f365-9197-519f-8e21-f02b49e6b7eb"),
            Ids.ForList(new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void ForItem_is_deterministic()
    {
        var week = new DateOnly(2026, 10, 5);

        Assert.Equal(Ids.ForItem(week, IngredientA), Ids.ForItem(week, IngredientA));
    }

    [Fact]
    public void ForItem_differs_by_week()
    {
        Assert.NotEqual(
            Ids.ForItem(new DateOnly(2026, 10, 5), IngredientA),
            Ids.ForItem(new DateOnly(2026, 10, 12), IngredientA));
    }

    [Fact]
    public void ForItem_differs_by_ingredient()
    {
        var week = new DateOnly(2026, 10, 5);

        Assert.NotEqual(Ids.ForItem(week, IngredientA), Ids.ForItem(week, IngredientB));
    }

    [Fact]
    public void ForList_differs_by_week()
    {
        Assert.NotEqual(Ids.ForList(new DateOnly(2026, 10, 5)), Ids.ForList(new DateOnly(2026, 10, 12)));
    }
}
