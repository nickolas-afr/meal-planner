namespace MealPlanner.App.Components;

public enum BadgeVariant { Planned, Staple, Count, Neutral }

public enum ButtonVariant { Primary, Secondary, Danger }

public record ActionSheetOption(string Label, bool Destructive = false, bool Checked = false);

public record PickerOption(string Value, string Text);
