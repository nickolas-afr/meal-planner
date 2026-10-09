using System.Globalization;

namespace MealPlanner.Core;

public static class QuantityFormat
{
    public const decimal Max = 100000m;
    public const int MaxDecimals = 2;

    /// <summary>"500 g", "1.5 pc", "2 pc +"; "" when the quantity is null.</summary>
    public static string Format(decimal? quantity, bool hasUnspecified, string unit)
    {
        if (quantity is not { } q)
            return "";

        string text = Math.Round(q, MaxDecimals, MidpointRounding.AwayFromZero)
            .ToString("0.##", CultureInfo.InvariantCulture);
        if (unit.Length > 0)
            text += " " + unit;
        if (hasUnspecified)
            text += " +";
        return text;
    }

    /// <summary>
    /// Parses a quantity typed by the user. Blank gives true with a null value.
    /// Otherwise the input must be a number &gt; 0 and ≤ 100000 with at most 2 decimals,
    /// using "," or "." as the decimal separator.
    /// </summary>
    public static bool TryParse(string? input, out decimal? value)
    {
        value = null;
        string text = (input ?? "").Trim();
        if (text.Length == 0)
            return true;

        text = text.Replace(',', '.');
        int dot = text.IndexOf('.');
        if (dot >= 0 && text.Length - dot - 1 > MaxDecimals)
            return false;

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed))
            return false;
        if (parsed <= 0 || parsed > Max)
            return false;

        value = parsed;
        return true;
    }
}
