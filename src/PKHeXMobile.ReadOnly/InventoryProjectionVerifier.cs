using System.Text.Json.Serialization;

namespace PKHeXMobile.ReadOnly;

/// <summary>Schema v5: exact ordered Core-enumerated records, including zero counts.</summary>
public sealed record ExpectedInventoryEntryReadOnly(
    [property: JsonRequired] int SlotIndex,
    [property: JsonRequired] int ItemId,
    [property: JsonRequired] int Count,
    [property: JsonRequired] string CoreItemType,
    [property: JsonRequired] uint? PouchCode,
    [property: JsonRequired] uint? Flags,
    [property: JsonRequired] uint? Padding);
public sealed record ExpectedInventoryPouchReadOnly(
    [property: JsonRequired] string Type,
    [property: JsonRequired] int MaxCount,
    [property: JsonRequired] IReadOnlyList<ExpectedInventoryEntryReadOnly> Entries);
public sealed record ExpectedInventoryReadOnly(
    [property: JsonRequired] IReadOnlyList<ExpectedInventoryPouchReadOnly> Pouches);

public static class InventoryProjectionVerifier
{
    public static void Compare(InventoryReadOnly? actual, ExpectedInventoryReadOnly? expected,
        ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual is null || expected is null)
        {
            errors.Add(actual is null ? "Inventory missing from observed projection" :
                "Inventory missing from expectation");
            return;
        }
        if (actual.Pouches is null || expected.Pouches is null)
        {
            errors.Add("Inventory.Pouches missing");
            return;
        }
        if (actual.Pouches.Count is 0 or > InventoryReadOnly.MaxPouches ||
            expected.Pouches.Count is 0 or > InventoryReadOnly.MaxPouches)
        {
            errors.Add("Inventory.Pouches count out of range");
            return;
        }
        Validate(actual.Pouches.Select(p => p is null ? null : new ViewPouch(
            p.Type, p.MaxCount, p.Entries?.Select(e => e is null ? null : new ViewEntry(
                e.SlotIndex, e.ItemId, e.CoreItemType, e.PouchCode, e.Flags, e.Padding))
                .ToArray())).ToArray(), "observed");
        Validate(expected.Pouches.Select(p => p is null ? null : new ViewPouch(
            p.Type, p.MaxCount, p.Entries?.Select(e => e is null ? null : new ViewEntry(
                e.SlotIndex, e.ItemId, e.CoreItemType, e.PouchCode, e.Flags, e.Padding))
                .ToArray())).ToArray(), "expected");

        Check(actual.Pouches.Count == expected.Pouches.Count, "Inventory.Pouches.Count");
        for (int p = 0; p < Math.Min(actual.Pouches.Count, expected.Pouches.Count); p++)
        {
            var a = actual.Pouches[p];
            var e = expected.Pouches[p];
            if (a is null || e is null || a.Entries is null || e.Entries is null) continue;
            string path = "Inventory.Pouches[" + p + "]";
            Check(a.Type == e.Type, path + ".Type");
            Check(a.MaxCount == e.MaxCount, path + ".MaxCount");
            Check(a.Entries.Count == e.Entries.Count, path + ".Entries.Count");
            for (int i = 0; i < Math.Min(a.Entries.Count, e.Entries.Count); i++)
            {
                var ai = a.Entries[i];
                var ei = e.Entries[i];
                if (ai is null || ei is null) continue;
                string field = path + ".Entries[" + i + "]";
                Check(ai.SlotIndex == ei.SlotIndex, field + ".SlotIndex");
                Check(ai.ItemId == ei.ItemId, field + ".ItemId");
                Check(ai.Count == ei.Count, field + ".Count");
                Check(ai.CoreItemType == ei.CoreItemType, field + ".CoreItemType");
                Check(ai.PouchCode == ei.PouchCode, field + ".PouchCode");
                Check(ai.Flags == ei.Flags, field + ".Flags");
                Check(ai.Padding == ei.Padding, field + ".Padding");
            }
        }
        void Check(bool same, string field)
        {
            if (!same) errors.Add(field + " mismatch");
        }
        void Validate(IReadOnlyList<ViewPouch?> pouches, string side)
        {
            int total = 0;
            var types = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pouch in pouches)
            {
                if (pouch is null)
                {
                    errors.Add("Inventory " + side + " null pouch");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(pouch.Type) || !types.Add(pouch.Type))
                    errors.Add("Inventory " + side + " missing or duplicate pouch type");
                if (pouch.MaxCount < 0)
                    errors.Add("Inventory " + side + " invalid pouch maximum");
                if (pouch.Entries is null)
                {
                    errors.Add("Inventory " + side + " missing entries");
                    continue;
                }
                total += pouch.Entries.Count;
                if (pouch.Entries.Count > InventoryReadOnly.MaxEntriesPerPouch ||
                    total > InventoryReadOnly.MaxTotalEntries)
                {
                    errors.Add("Inventory " + side + " exceeds item limit");
                    return;
                }
                var ids = new HashSet<int>();
                for (int i = 0; i < pouch.Entries.Count; i++)
                {
                    var entry = pouch.Entries[i];
                    if (entry is null)
                    {
                        errors.Add("Inventory " + side + " null entry");
                        continue;
                    }
                    if (entry.SlotIndex != i || entry.ItemId < 0 || !ids.Add(entry.ItemId))
                        errors.Add("Inventory " + side + " invalid slot order or repeated item ID");
                    if (string.IsNullOrWhiteSpace(entry.CoreItemType))
                        errors.Add("Inventory " + side + " missing item type");
                    bool allNull = entry.PouchCode is null && entry.Flags is null && entry.Padding is null;
                    bool allPresent = entry.PouchCode is not null && entry.Flags is not null && entry.Padding is not null;
                    if ((!allNull && !allPresent) ||
                        ((entry.CoreItemType is "InventoryItem9" or "InventoryItem9a") && !allPresent))
                        errors.Add("Inventory " + side + " incomplete raw item fields");
                }
            }
        }
    }

    private sealed record ViewEntry(int SlotIndex, int ItemId, string CoreItemType,
        uint? PouchCode, uint? Flags, uint? Padding);
    private sealed record ViewPouch(string Type, int MaxCount, IReadOnlyList<ViewEntry?>? Entries);
}
