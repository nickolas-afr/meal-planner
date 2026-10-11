using MealPlanner.Core;
using MealPlanner.Core.Entities;

namespace MealPlanner.App.Services;

public sealed class MarketService(AppState state, EntityWriter writer, Clock clock)
{
    /// <summary>Live markets in SortOrder.</summary>
    public IReadOnlyList<Market> All() => state.Markets.ToList();

    public string? ValidateName(string? name, Guid? editingId) => Validation.MarketName(name, state.Markets, editingId);

    /// <summary>Adds a market at the end of the order. Throws InvalidOperationException when the name is invalid.</summary>
    public async Task<Market> Add(string name)
    {
        if (ValidateName(name, null) is { } error)
            throw new InvalidOperationException(error);

        var markets = state.Markets.ToList();
        var market = new Market
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            SortOrder = markets.Count == 0 ? 0 : markets.Max(m => m.SortOrder) + 1,
            UpdatedAt = clock.UtcNow,
        };
        await writer.SaveAsync(market);
        return market;
    }

    public async Task Rename(Guid id, string name)
    {
        if (ValidateName(name, id) is { } error)
            throw new InvalidOperationException(error);
        if (state.Market(id) is not { } market)
            return;
        await writer.SaveAsync(market with { Name = name.Trim(), UpdatedAt = clock.UtcNow });
    }

    /// <summary>Soft-deletes the market and removes it from its ingredients' markets.</summary>
    public async Task Delete(Guid id)
    {
        if (state.Market(id) is not { } market)
            return;

        var now = clock.UtcNow;
        var changed = new List<Entity> { market with { IsDeleted = true, UpdatedAt = now } };
        changed.AddRange(state.Ingredients.Where(i => IngredientMarkets.Of(i).Contains(id))
            .Select(i => IngredientMarkets.With(i, [.. IngredientMarkets.Of(i).Where(m => m != id)]) with { UpdatedAt = now }));
        await writer.SaveAsync(changed);
    }

    /// <summary>Sets SortOrder from the position in <paramref name="ids"/>.</summary>
    public async Task Reorder(IReadOnlyList<Guid> ids)
    {
        var now = clock.UtcNow;
        var changed = new List<Entity>();
        for (int index = 0; index < ids.Count; index++)
        {
            if (state.Market(ids[index]) is { } market && market.SortOrder != index)
                changed.Add(market with { SortOrder = index, UpdatedAt = now });
        }
        await writer.SaveAsync(changed);
    }

    /// <summary>Stores the manual item order (ingredient ids) for a market.</summary>
    public async Task SetItemOrder(Guid marketId, IReadOnlyList<Guid> ingredientIds)
    {
        if (state.Market(marketId) is not { } market || market.ItemOrder.SequenceEqual(ingredientIds))
            return;
        await writer.SaveAsync(market with { ItemOrder = ingredientIds.ToList(), UpdatedAt = clock.UtcNow });
    }
}
