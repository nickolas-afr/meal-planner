using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MealPlanner.Core;

/// <summary>Deterministic ids, so both phones compute the same list and item ids for a week.</summary>
public static class Ids
{
    public static readonly Guid Namespace = new("b467da48-ba3e-4e52-887b-aa40431aa188");

    public static Guid ForList(DateOnly weekStart) =>
        NameBased(Namespace, $"list:{Day(weekStart)}");

    public static Guid ForItem(DateOnly weekStart, Guid ingredientId) =>
        NameBased(Namespace, $"item:{Day(weekStart)}:{ingredientId:D}");

    /// <summary>Name-based UUIDv5 (RFC 9562, SHA-1).</summary>
    public static Guid NameBased(Guid namespaceId, string name)
    {
        Span<byte> nsBytes = stackalloc byte[16];
        namespaceId.TryWriteBytes(nsBytes, bigEndian: true, out _);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);

        byte[] input = new byte[16 + nameBytes.Length];
        nsBytes.CopyTo(input);
        nameBytes.CopyTo(input, 16);

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(input, hash);

        Span<byte> bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);   // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);   // RFC variant
        return new Guid(bytes, bigEndian: true);
    }

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
