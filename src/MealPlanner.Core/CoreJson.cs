using System.Text.Json;

namespace MealPlanner.Core;

/// <summary>The one JSON setup shared by storage, sync and backup.</summary>
public static class CoreJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
