namespace MealPlanner.Core.Tests;

public class QuantityFormatTests
{
    [Fact]
    public void Format_whole_number_with_unit()
    {
        Assert.Equal("500 g", QuantityFormat.Format(500m, false, "g"));
    }

    [Fact]
    public void Format_keeps_one_decimal()
    {
        Assert.Equal("1.5 pc", QuantityFormat.Format(1.5m, false, "pc"));
    }

    [Fact]
    public void Format_adds_plus_when_some_sources_had_no_quantity()
    {
        Assert.Equal("2 pc +", QuantityFormat.Format(2m, true, "pc"));
    }

    [Fact]
    public void Format_is_empty_for_a_null_quantity()
    {
        Assert.Equal("", QuantityFormat.Format(null, true, ""));
    }

    [Fact]
    public void Format_rounds_to_two_decimals()
    {
        Assert.Equal("0.33", QuantityFormat.Format(0.333m, false, ""));
    }

    [Fact]
    public void Format_rounds_midpoints_away_from_zero()
    {
        Assert.Equal("0.13", QuantityFormat.Format(0.125m, false, ""));
    }

    [Fact]
    public void Format_without_unit_has_no_space()
    {
        Assert.Equal("1000", QuantityFormat.Format(1000m, false, ""));
        Assert.Equal("2 +", QuantityFormat.Format(2m, true, ""));
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("1.5")]
    public void TryParse_accepts_comma_or_dot_as_decimal_separator(string input)
    {
        Assert.True(QuantityFormat.TryParse(input, out decimal? value));
        Assert.Equal<decimal?>(1.5m, value);
    }

    [Fact]
    public void TryParse_trims_spaces()
    {
        Assert.True(QuantityFormat.TryParse(" 2 ", out decimal? value));
        Assert.Equal<decimal?>(2m, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void TryParse_blank_is_valid_with_null_value(string input)
    {
        Assert.True(QuantityFormat.TryParse(input, out decimal? value));
        Assert.Null(value);
    }

    [Fact]
    public void TryParse_null_is_valid_with_null_value()
    {
        Assert.True(QuantityFormat.TryParse(null, out decimal? value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.234")]
    [InlineData("abc")]
    [InlineData("100000.01")]
    public void TryParse_rejects_invalid_input(string input)
    {
        Assert.False(QuantityFormat.TryParse(input, out decimal? value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("100000")]
    [InlineData("0.01")]
    public void TryParse_accepts_the_bounds(string input)
    {
        Assert.True(QuantityFormat.TryParse(input, out decimal? value));
        Assert.Equal<decimal?>(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture), value);
    }
}
