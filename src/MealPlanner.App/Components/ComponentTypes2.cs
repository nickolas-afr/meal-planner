namespace MealPlanner.App.Components;

public enum BannerVariant { Offline, UpdateAvailable, OutOfDate }

public record SegmentOption<TValue>(TValue Value, string Text);
