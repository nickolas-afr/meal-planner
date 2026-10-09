using Microsoft.AspNetCore.Components;

namespace MealPlanner.App.Components.Icons;

/// <summary>
/// Base for the inline SVG icons (ui.md › Icons): 24 × 24 viewBox, stroke currentColor, width 1.8,
/// round caps and joins. Icons are decorative (aria-hidden); the button around them carries the label.
/// </summary>
public abstract class IconBase : ComponentBase
{
    /// <summary>Rendered width and height in px.</summary>
    [Parameter] public int Size { get; set; } = 24;

    [Parameter] public string? Class { get; set; }
}
