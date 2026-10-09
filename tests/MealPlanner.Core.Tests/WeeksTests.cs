namespace MealPlanner.Core.Tests;

public class WeeksTests
{
    // 2026-10-05 is a Monday.
    [Theory]
    [InlineData(2026, 10, 5)]
    [InlineData(2026, 10, 6)]
    [InlineData(2026, 10, 7)]
    [InlineData(2026, 10, 8)]
    [InlineData(2026, 10, 9)]
    [InlineData(2026, 10, 10)]
    [InlineData(2026, 10, 11)]
    public void StartOf_returns_the_monday_of_every_weekday(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(2026, 10, 5), Weeks.StartOf(new DateOnly(year, month, day)));
    }

    [Fact]
    public void Active_on_Thursday_is_this_weeks_monday()
    {
        Assert.Equal(new DateOnly(2026, 10, 5), Weeks.Active(new DateTime(2026, 10, 8, 12, 0, 0)));
    }

    [Theory]
    [InlineData(2026, 10, 9)]   // Friday
    [InlineData(2026, 10, 10)]  // Saturday
    [InlineData(2026, 10, 11)]  // Sunday
    public void Active_from_Friday_to_Sunday_is_next_monday(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(2026, 10, 12), Weeks.Active(new DateTime(year, month, day, 12, 0, 0)));
    }

    [Theory]
    [InlineData(2026, 10, 5)]   // Monday
    [InlineData(2026, 10, 7)]   // Wednesday
    public void Active_from_Monday_to_Wednesday_is_this_weeks_monday(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(2026, 10, 5), Weeks.Active(new DateTime(year, month, day, 8, 0, 0)));
    }

    [Fact]
    public void RetentionCutoff_is_fourteen_days_before_the_start_of_this_week()
    {
        // StartOf(2026-10-09) = 2026-10-05, minus 14 days = 2026-09-21.
        Assert.Equal(new DateOnly(2026, 9, 21), Weeks.RetentionCutoff(new DateTime(2026, 10, 9, 18, 0, 0)));
    }
}
