using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Source-indexed Core slot flags. This is diagnostic metadata, NOT an
/// authenticated layout, lock enforcement policy or authorization to edit.
/// Empty and occupied positions are both included.
/// </summary>
public sealed record StorageSlotFlagsReadOnly(int SlotIndex, int RawFlags, bool OverwriteProtected);
public sealed record StorageBoxFlagsReadOnly(int BoxIndex, IReadOnlyList<StorageSlotFlagsReadOnly> Slots);
public sealed record StorageFlagsReadOnly(IReadOnlyList<StorageBoxFlagsReadOnly> Boxes)
{
    public const int MaxBoxes = 64;
    public const int MaxSlotsPerBox = 64;
    public const int KnownFlagMask = 0x60000FFF; // Party 0..5, battle teams 0..5, Starter, Locked.
    public const int ProtectionMask = 0x60000FC0; // Battle teams, Starter, Locked.
    public int ProtectedCount => Boxes.Sum(b => b.Slots.Count(s => s.OverwriteProtected));

    /// <summary>
    /// Reads flags from pinned Core without calling any setter, pointer remap,
    /// box movement, serialization or data-mutation API. Does not retain Core.
    /// </summary>
    public static StorageFlagsReadOnly Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.BoxCount is < 0 or > MaxBoxes ||
            save.BoxSlotCount is < 0 or > MaxSlotsPerBox)
            throw new InvalidDataException("Storage layout exceeds provisional bounds.");
        var boxes = new List<StorageBoxFlagsReadOnly>(save.BoxCount);
        for (int box = 0; box < save.BoxCount; box++)
        {
            var slots = new List<StorageSlotFlagsReadOnly>(save.BoxSlotCount);
            for (int slot = 0; slot < save.BoxSlotCount; slot++)
            {
                int raw = (int)save.GetBoxSlotFlags(box, slot);
                if ((raw & ~KnownFlagMask) != 0)
                    throw new InvalidDataException("Core reported unreviewed storage slot flags.");
                bool protectedByCore = save.IsBoxSlotOverwriteProtected(box, slot);
                if (protectedByCore != ((raw & ProtectionMask) != 0))
                    throw new InvalidDataException("Core storage protection semantics are inconsistent.");
                slots.Add(new StorageSlotFlagsReadOnly(slot, raw, protectedByCore));
            }
            boxes.Add(new StorageBoxFlagsReadOnly(box, slots.AsReadOnly()));
        }
        return new StorageFlagsReadOnly(boxes.AsReadOnly());
    }
}

/// <summary>Schema v6: every physical box slot, including empty positions.</summary>
public sealed record ExpectedStorageSlotFlagsReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] int SlotIndex,
    [property: System.Text.Json.Serialization.JsonRequired] int RawFlags,
    [property: System.Text.Json.Serialization.JsonRequired] bool OverwriteProtected);
public sealed record ExpectedStorageBoxFlagsReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] int BoxIndex,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<ExpectedStorageSlotFlagsReadOnly> Slots);
public sealed record ExpectedStorageFlagsReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<ExpectedStorageBoxFlagsReadOnly> Boxes);

public static class StorageFlagsProjectionVerifier
{
    /// <summary>Compare every position, not only occupied or protected slots.</summary>
    public static void Compare(StorageFlagsReadOnly? actual, ExpectedStorageFlagsReadOnly? expected,
        int boxCount, int slotCount, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual is null || expected is null)
        {
            errors.Add(actual is null ? "Storage missing from observed projection" :
                "Storage missing from expectation");
            return;
        }
        if (boxCount is < 0 or > StorageFlagsReadOnly.MaxBoxes ||
            slotCount is < 0 or > StorageFlagsReadOnly.MaxSlotsPerBox ||
            actual.Boxes is null || expected.Boxes is null ||
            actual.Boxes.Count != boxCount || expected.Boxes.Count != boxCount)
        {
            errors.Add("Storage box count out of range or inconsistent");
            return;
        }
        for (int box = 0; box < boxCount; box++)
        {
            var a = actual.Boxes[box];
            var e = expected.Boxes[box];
            string path = $"Storage.Boxes[{box}]";
            if (a is null || e is null || a.Slots is null || e.Slots is null ||
                a.Slots.Count != slotCount || e.Slots.Count != slotCount)
            {
                errors.Add(path + " missing or inconsistent slots");
                continue;
            }
            if (a.BoxIndex != box || e.BoxIndex != box)
                errors.Add(path + ".BoxIndex invalid");
            for (int slot = 0; slot < slotCount; slot++)
            {
                var av = a.Slots[slot];
                var ev = e.Slots[slot];
                string field = path + $".Slots[{slot}]";
                if (av is null || ev is null)
                {
                    errors.Add(field + " missing");
                    continue;
                }
                if (av.SlotIndex != slot || ev.SlotIndex != slot)
                    errors.Add(field + ".SlotIndex invalid");
                Validate(av.RawFlags, av.OverwriteProtected, field + " observed");
                Validate(ev.RawFlags, ev.OverwriteProtected, field + " expected");
                if (av.RawFlags != ev.RawFlags)
                    errors.Add(field + ".RawFlags mismatch");
                if (av.OverwriteProtected != ev.OverwriteProtected)
                    errors.Add(field + ".OverwriteProtected mismatch");
            }
        }

        void Validate(int flags, bool protectedValue, string path)
        {
            if ((flags & ~StorageFlagsReadOnly.KnownFlagMask) != 0 ||
                protectedValue != ((flags & StorageFlagsReadOnly.ProtectionMask) != 0))
                errors.Add(path + " invalid flags or protection semantics");
        }
    }
}
