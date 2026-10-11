using MealPlanner.Core.Entities;

namespace MealPlanner.Core.Shopping;

/// <summary>Picks the markets for a week so the household visits as few as possible (spec: Choosing markets).</summary>
public static class MarketChooser
{
    private const int ExhaustiveLimit = 12;

    /// <summary>Live item whose market is fixed: manual, moved, needed staple or ticked.</summary>
    public static bool IsPinned(ShoppingItem i) =>
        !i.IsDeleted && (i.IngredientId is null || i.MarketOverridden || i.IsNeeded || i.CheckedAt is not null);

    public static IReadOnlyList<Guid> Choose(
        IEnumerable<ShoppingItem> items,
        IReadOnlyDictionary<Guid, Ingredient> ingredients,
        IEnumerable<Market> markets)
    {
        var live = markets.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var itemList = items.Where(i => !i.IsDeleted).ToList();

        var pinned = new HashSet<Guid>();
        foreach (var i in itemList)
            if (IsPinned(i) && i.MarketId is { } m && live.ContainsKey(m))
                pinned.Add(m);

        var flexible = new List<IReadOnlyList<Guid>>();
        foreach (var i in itemList)
        {
            if (IsPinned(i) || (i.IsStaple && !i.IsNeeded)) continue;
            var c = Candidates(i, ingredients, live);
            if (c.Count > 0) flexible.Add(c);
        }

        var pool = flexible.SelectMany(c => c).Where(m => !pinned.Contains(m)).Distinct()
            .OrderBy(m => Key(live[m])).ThenBy(m => m.ToString()).ToList();

        var chosen = pool.Count <= ExhaustiveLimit
            ? Exhaustive(pinned, pool, flexible, live)
            : Greedy(pinned, pool, flexible, live);

        return chosen.OrderBy(m => live[m].SortOrder).ThenBy(m => live[m].Name, StringComparer.Ordinal)
            .ThenBy(m => m.ToString()).ToList();
    }

    public static Guid? Assign(
        ShoppingItem item,
        IReadOnlyList<Guid> chosen,
        IReadOnlyDictionary<Guid, Ingredient> ingredients,
        IEnumerable<Market> markets)
    {
        if (IsPinned(item) || item.IngredientId is null) return item.MarketId;
        var live = markets.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var cands = Candidates(item, ingredients, live);
        foreach (var c in cands)
            if (chosen.Contains(c)) return c;
        return cands.Count > 0 ? cands[0] : null;
    }

    private static (int, string) Key(Market m) => (m.SortOrder, m.Name);

    private static IReadOnlyList<Guid> Candidates(
        ShoppingItem item, IReadOnlyDictionary<Guid, Ingredient> ingredients, IReadOnlyDictionary<Guid, Market> live)
    {
        if (item.IngredientId is not { } id
            || !ingredients.TryGetValue(id, out var ing) || ing.IsDeleted) return [];
        return IngredientMarkets.Of(ing).Where(live.ContainsKey).Distinct().ToList();
    }

    private static int Rank(IReadOnlyList<Guid> cands, ISet<Guid> s)
    {
        for (var i = 0; i < cands.Count; i++)
            if (s.Contains(cands[i])) return i;
        return -1;
    }

    private static HashSet<Guid> Exhaustive(
        HashSet<Guid> pinned, List<Guid> pool, List<IReadOnlyList<Guid>> flexible, IReadOnlyDictionary<Guid, Market> live)
    {
        HashSet<Guid>? best = null;
        int bestSize = 0, bestRank = 0;
        int[]? bestOrders = null;
        string[]? bestIds = null;

        for (var mask = 0; mask < 1 << pool.Count; mask++)
        {
            var s = new HashSet<Guid>(pinned);
            for (var b = 0; b < pool.Count; b++)
                if ((mask & (1 << b)) != 0) s.Add(pool[b]);
            if (best is not null && s.Count > bestSize) continue;

            var rank = 0;
            var ok = true;
            foreach (var c in flexible)
            {
                var r = Rank(c, s);
                if (r < 0) { ok = false; break; }
                rank += r;
            }
            if (!ok) continue;

            var orders = s.Select(m => live[m].SortOrder).Order().ToArray();
            var ids = s.Select(m => m.ToString()).Order(StringComparer.Ordinal).ToArray();
            if (best is null || Better(s.Count, rank, orders, ids, bestSize, bestRank, bestOrders!, bestIds!))
            {
                best = s; bestSize = s.Count; bestRank = rank; bestOrders = orders; bestIds = ids;
            }
        }
        return best ?? pinned;
    }

    private static bool Better(int size, int rank, int[] orders, string[] ids,
        int bSize, int bRank, int[] bOrders, string[] bIds)
    {
        if (size != bSize) return size < bSize;
        if (rank != bRank) return rank < bRank;
        var c = Compare(orders, bOrders);
        if (c != 0) return c < 0;
        return Compare(ids, bIds) < 0;
    }

    private static int Compare<T>(T[] a, T[] b) where T : IComparable<T>
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    private static HashSet<Guid> Greedy(
        HashSet<Guid> pinned, List<Guid> pool, List<IReadOnlyList<Guid>> flexible, IReadOnlyDictionary<Guid, Market> live)
    {
        var s = new HashSet<Guid>(pinned);
        while (true)
        {
            var uncovered = flexible.Where(c => Rank(c, s) < 0).ToList();
            if (uncovered.Count == 0) return s;
            var pick = pool.Where(m => !s.Contains(m))
                .Select(m =>
                {
                    var covered = uncovered.Where(c => c.Contains(m)).ToList();
                    return (Market: m, Count: covered.Count, Rank: covered.Sum(c => c.IndexOf(m)));
                })
                .Where(x => x.Count > 0)
                .OrderByDescending(x => x.Count).ThenBy(x => x.Rank)
                .ThenBy(x => live[x.Market].SortOrder).ThenBy(x => x.Market.ToString(), StringComparer.Ordinal)
                .First();
            s.Add(pick.Market);
        }
    }

    private static int IndexOf(this IReadOnlyList<Guid> list, Guid id)
    {
        for (var i = 0; i < list.Count; i++) if (list[i] == id) return i;
        return -1;
    }
}
