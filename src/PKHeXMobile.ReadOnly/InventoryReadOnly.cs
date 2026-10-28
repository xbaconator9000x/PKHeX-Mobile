using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>Immutable Core-enumerated pouch subset, NOT a complete raw-block
/// inventory. Unknown item IDs and custom/hack slots may be omitted by Core.</summary>
public sealed record InventoryEntryReadOnly(
    int SlotIndex, int ItemId, int Count, string CoreItemType,
    uint? PouchCode, uint? Flags, uint? Padding);

public sealed record InventoryPouchReadOnly(
    string Type, int MaxCount, IReadOnlyList<InventoryEntryReadOnly> Entries)
{
    public int NonzeroCount => Entries.Count(entry => entry.Count != 0);
}

public sealed record InventoryReadOnly(IReadOnlyList<InventoryPouchReadOnly> Pouches)
{
    public const int MaxPouches = 64;
    public const int MaxEntriesPerPouch = 4096;
    public const int MaxTotalEntries = 8192;
    public int EnumeratedCount => Pouches.Sum(p => p.Entries.Count);
    public int NonzeroCount => Pouches.Sum(p => p.NonzeroCount);

    public static InventoryReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return Project(save.Inventory);
    }

    /// <summary>Never calls CopyTo, SetPouch, Sanitize, ClearCount0 or sorting.
    /// Preserves zero-count records and all exposed Gen9 raw flags/padding.</summary>
    public static InventoryReadOnly? Project(PlayerBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var pouches = bag.Pouches;
        if (pouches is null || pouches.Count == 0) return null;
        if (pouches.Count > MaxPouches)
            throw new InvalidDataException("Core inventory exceeds the pouch limit.");
        var projected = new List<InventoryPouchReadOnly>(pouches.Count);
        int total = 0;
        foreach (var pouch in pouches)
        {
            if (pouch is null)
                throw new InvalidDataException("Core inventory returned a null pouch.");
            var items = pouch.Items;
            if (items is null || items.Length > MaxEntriesPerPouch ||
                total + items.Length > MaxTotalEntries)
                throw new InvalidDataException("Core inventory exceeds the item limit.");
            total += items.Length;
            var entries = new List<InventoryEntryReadOnly>(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i] ??
                    throw new InvalidDataException("Core inventory returned a null item.");
                uint? code = null, flags = null, padding = null;
                if (item is InventoryItem9 sv)
                {
                    code = sv.Pouch; flags = sv.Flags; padding = sv.Padding;
                }
                else if (item is InventoryItem9a za)
                {
                    code = za.Pouch; flags = za.Flags; padding = za.Padding;
                }
                entries.Add(new InventoryEntryReadOnly(
                    i, item.Index, item.Count, item.GetType().Name, code, flags, padding));
            }
            projected.Add(new InventoryPouchReadOnly(
                pouch.Type.ToString(), pouch.MaxCount, entries.AsReadOnly()));
        }
        return new InventoryReadOnly(projected.AsReadOnly());
    }
}
