using MealPlanner.Core.Entities;

namespace MealPlanner.Core;

/// <summary>One method per rule in spec › Validation. Each returns an error message, or null when valid.</summary>
public static class Validation
{
    public const int MealNameMax = 80;
    public const int IngredientNameMax = 60;
    public const int MarketNameMax = 40;
    public const int ItemNameMax = 60;
    public const int NotesMax = 2000;
    public const int DisplayNameMax = 30;
    public const int InviteCodeLength = 8;

    // Crockford base32: digits and letters without I, L, O and U.
    private const string InviteAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <param name="editingId">The meal being edited, so it doesn't clash with itself; null for a new meal.</param>
    public static string? MealName(string? name, IEnumerable<Meal> meals, Guid? editingId) =>
        Name(name, MealNameMax, "A meal", meals.Where(m => !m.IsDeleted && m.Id != editingId).Select(m => m.Name));

    public static string? IngredientName(string? name, IEnumerable<Ingredient> ingredients, Guid? editingId) =>
        Name(name, IngredientNameMax, "An ingredient",
            ingredients.Where(i => !i.IsDeleted && i.Id != editingId).Select(i => i.Name));

    public static string? MarketName(string? name, IEnumerable<Market> markets, Guid? editingId) =>
        Name(name, MarketNameMax, "A market", markets.Where(m => !m.IsDeleted && m.Id != editingId).Select(m => m.Name));

    /// <summary>Manual shopping item name: required, no uniqueness rule.</summary>
    public static string? ItemName(string? name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            return "Enter a name.";
        return trimmed.Length > ItemNameMax ? $"Use at most {ItemNameMax} characters." : null;
    }

    public static string? Quantity(string? input) =>
        QuantityFormat.TryParse(input, out _)
            ? null
            : "Enter a number above 0, up to 100000, with at most 2 decimals.";

    public static string? Notes(string? notes) =>
        (notes?.Length ?? 0) > NotesMax ? $"Notes can be up to {NotesMax} characters." : null;

    public static string? DisplayName(string? name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            return "Enter your name.";
        return trimmed.Length > DisplayNameMax ? $"Use at most {DisplayNameMax} characters." : null;
    }

    public static string? InviteCode(string? code)
    {
        string normalized = NormalizeInviteCode(code);
        return normalized.Length == InviteCodeLength && normalized.All(InviteAlphabet.Contains)
            ? null
            : "Enter the 8-character code.";
    }

    /// <summary>Uppercase, with spaces and dashes removed.</summary>
    public static string NormalizeInviteCode(string? code) =>
        new((code ?? "").Where(c => c != ' ' && c != '-').Select(char.ToUpperInvariant).ToArray());

    private static string? Name(string? name, int max, string what, IEnumerable<string> otherNames)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            return "Enter a name.";
        if (trimmed.Length > max)
            return $"Use at most {max} characters.";
        if (otherNames.Any(n => string.Equals(n.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
            return $"{what} with this name already exists.";
        return null;
    }
}
