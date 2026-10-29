using System.Security.Cryptography;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Bounded, detached Core-reported nursery metadata. Raw parent bytes are
/// hashed, never returned. A digest is not a decoded species, authenticated
/// layout, or permission to edit a save.
/// </summary>
public sealed record DaycareSlotReadOnly(
    int SlotIndex, bool Occupied, int RawLength, string RawSha256, uint? ExperienceGained);
public sealed record DaycareFacilityReadOnly(
    int FacilityIndex, bool? EggAvailable, string? SeedHex,
    IReadOnlyList<DaycareSlotReadOnly> Slots);
/// <summary>Generation 2 has a separate egg buffer, not a third deposited parent.</summary>
public sealed record DaycareEggReadOnly(int RawLength, string RawSha256);
public sealed record DaycareReadOnly(
    bool Available, IReadOnlyList<DaycareFacilityReadOnly> Facilities)
{
    public DaycareEggReadOnly? Gen2Egg { get; init; }
    public const int MaxFacilities = 2;
    public const int SlotsPerFacility = 2; // SWSH always has two; Gen1 has one.
    public const int MaxParentBytes = 512;

    /// <summary>
    /// Only recognized Core interfaces and the special SWSH two-nursery block
    /// are projected. No occupancy setters, PKM decoding, serialization,
    /// or implicit normalization occurs.
    /// </summary>
    public static DaycareReadOnly Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save is SAV8SWSH swsh)
            return new DaycareReadOnly(true, Array.AsReadOnly(new[]
            {
                ProjectSwordShield(swsh, 0),
                ProjectSwordShield(swsh, 1),
            }));
        if (save is IDaycareMulti multi)
        {
            if (multi.DaycareCount is < 1 or > MaxFacilities)
                throw new InvalidDataException("Unreviewed number of daycare facilities.");
            var facilities = new DaycareFacilityReadOnly[multi.DaycareCount];
            for (int i = 0; i < facilities.Length; i++)
                facilities[i] = ProjectStorage(multi[i], i);
            return new DaycareReadOnly(true, Array.AsReadOnly(facilities));
        }
        if (save is SAV2 gen2)
            return new DaycareReadOnly(true,
                Array.AsReadOnly(new[] { ProjectStorage(gen2, 0) }))
            {
                // GetDaycareEgg returns a distinct mutable Reserved buffer. Hash a
                // bounded copy, never infer an egg from the two parent slots.
                Gen2Egg = ProjectEgg(gen2.GetDaycareEgg()),
            };
        if (save is IDaycareStorage storage)
            return new DaycareReadOnly(true,
                Array.AsReadOnly(new[] { ProjectStorage(storage, 0, save is SAV5BW) }));
        return new DaycareReadOnly(false, Array.Empty<DaycareFacilityReadOnly>());
    }

    /// <summary>Testable boundary for a single Core daycare interface.</summary>
    public static DaycareFacilityReadOnly ProjectStorage(
        IDaycareStorage storage, int facilityIndex, bool seedUnsupported = false)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (facilityIndex is < 0 or >= MaxFacilities ||
            storage.DaycareSlotCount is < 1 or > SlotsPerFacility)
            throw new InvalidDataException("Unreviewed daycare slot layout.");
        var slots = new DaycareSlotReadOnly[storage.DaycareSlotCount];
        for (int slot = 0; slot < slots.Length; slot++)
        {
            var raw = storage.GetDaycareSlot(slot);
            slots[slot] = ProjectSlot(slot, storage.IsDaycareOccupied(slot), raw,
                storage is IDaycareExperience exp ? exp.GetDaycareEXP(slot) : null);
        }
        string? seed = seedUnsupported ? null : storage switch
        {
            IDaycareRandomState<UInt128> wide => wide.Seed.ToString("X32"),
            IDaycareRandomState<ulong> normal => normal.Seed.ToString("X16"),
            IDaycareRandomState<uint> small => small.Seed.ToString("X8"),
            _ => null,
        };
        bool? egg = storage is IDaycareEggState state ? state.IsEggAvailable : null;
        return new DaycareFacilityReadOnly(facilityIndex, egg, seed, Array.AsReadOnly(slots));
    }

    private static DaycareFacilityReadOnly ProjectSwordShield(SAV8SWSH save, int facility)
    {
        var daycare = save.Daycare;
        var slots = new DaycareSlotReadOnly[SlotsPerFacility];
        for (int slot = 0; slot < slots.Length; slot++)
        {
            int index = (facility * SlotsPerFacility) + slot;
            // Daycare8's indexer returns a tail slice, not one PK8. Bound it
            // explicitly before hashing; never expose the mutable tail.
            var bounded = daycare[index].Slice(0, save.SIZE_STORED);
            bool occupied = facility == 0
                ? daycare.GetDaycare1SlotOccupied(slot)
                : daycare.GetDaycare2SlotOccupied(slot);
            slots[slot] = ProjectSlot(slot, occupied, bounded, null);
        }
        return new DaycareFacilityReadOnly(facility, null,
            daycare.GetDaycareSeed(facility).ToString("X16"), Array.AsReadOnly(slots));
    }

    private static DaycareEggReadOnly ProjectEgg(Memory<byte> raw)
    {
        if (raw.Length is < 1 or > MaxParentBytes)
            throw new InvalidDataException("Unreviewed Gen2 egg length.");
        var detached = raw.ToArray();
        return new DaycareEggReadOnly(detached.Length,
            Convert.ToHexString(SHA256.HashData(detached)));
    }

    private static DaycareSlotReadOnly ProjectSlot(
        int slot, bool occupied, Memory<byte> raw, uint? experience)
    {
        if (raw.Length is < 1 or > MaxParentBytes)
            throw new InvalidDataException("Unreviewed daycare parent length.");
        // Detach from Core's writable Memory<byte> before computing a digest.
        var detached = raw.ToArray();
        return new DaycareSlotReadOnly(slot, occupied, detached.Length,
            Convert.ToHexString(SHA256.HashData(detached)), experience);
    }
}

/// <summary>Schema v11: every reported nursery, slot and raw-byte digest.</summary>
public sealed record ExpectedDaycareSlotReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] int SlotIndex,
    [property: System.Text.Json.Serialization.JsonRequired] bool Occupied,
    [property: System.Text.Json.Serialization.JsonRequired] int RawLength,
    [property: System.Text.Json.Serialization.JsonRequired] string RawSha256,
    [property: System.Text.Json.Serialization.JsonRequired] uint? ExperienceGained);
public sealed record ExpectedDaycareFacilityReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] int FacilityIndex,
    [property: System.Text.Json.Serialization.JsonRequired] bool? EggAvailable,
    [property: System.Text.Json.Serialization.JsonRequired] string? SeedHex,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<ExpectedDaycareSlotReadOnly> Slots);
public sealed record ExpectedDaycareEggReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] int RawLength,
    [property: System.Text.Json.Serialization.JsonRequired] string RawSha256);
public sealed record ExpectedDaycareReadOnly(
    [property: System.Text.Json.Serialization.JsonRequired] bool Available,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<ExpectedDaycareFacilityReadOnly> Facilities)
{
    // Optional in v11, required for Gen2 in v12. No third-parent conflation.
    public ExpectedDaycareEggReadOnly? Gen2Egg { get; init; }
}

public static class DaycareProjectionVerifier
{
    public static void Compare(DaycareReadOnly? actual, ExpectedDaycareReadOnly? expected,
        List<string> errors, bool requireGen2Egg = false)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual is null || expected is null)
        {
            errors.Add(actual is null ? "Daycare missing from observed projection" :
                "Daycare missing from expectation");
            return;
        }
        if (!Validate(actual.Available, actual.Facilities, "observed") ||
            !Validate(expected.Available, expected.Facilities, "expected"))
            return;
        if (requireGen2Egg || expected.Gen2Egg is not null)
        {
            if (actual.Gen2Egg is null || expected.Gen2Egg is null)
                errors.Add("Daycare.Gen2Egg missing");
            else
            {
                ValidateEgg(actual.Gen2Egg.RawLength, actual.Gen2Egg.RawSha256, "observed");
                ValidateEgg(expected.Gen2Egg.RawLength, expected.Gen2Egg.RawSha256, "expected");
                Check(actual.Gen2Egg.RawLength == expected.Gen2Egg.RawLength, "Daycare.Gen2Egg.RawLength");
                Check(string.Equals(actual.Gen2Egg.RawSha256, expected.Gen2Egg.RawSha256,
                    StringComparison.OrdinalIgnoreCase), "Daycare.Gen2Egg.RawSha256");
            }
        }
        Check(actual.Available == expected.Available, "Daycare.Available");
        Check(actual.Facilities.Count == expected.Facilities.Count, "Daycare.Facilities.Count");
        for (int i = 0; i < Math.Min(actual.Facilities.Count, expected.Facilities.Count); i++)
        {
            var a = actual.Facilities[i];
            var e = expected.Facilities[i];
            string path = $"Daycare.Facilities[{i}]";
            Check(a.FacilityIndex == e.FacilityIndex, path + ".FacilityIndex");
            Check(a.EggAvailable == e.EggAvailable, path + ".EggAvailable");
            Check(string.Equals(a.SeedHex, e.SeedHex, StringComparison.OrdinalIgnoreCase),
                path + ".SeedHex");
            Check(a.Slots.Count == e.Slots.Count, path + ".Slots.Count");
            for (int slot = 0; slot < Math.Min(a.Slots.Count, e.Slots.Count); slot++)
            {
                var av = a.Slots[slot];
                var ev = e.Slots[slot];
                string field = path + $".Slots[{slot}]";
                Check(av.SlotIndex == ev.SlotIndex, field + ".SlotIndex");
                Check(av.Occupied == ev.Occupied, field + ".Occupied");
                Check(av.RawLength == ev.RawLength, field + ".RawLength");
                Check(string.Equals(av.RawSha256, ev.RawSha256, StringComparison.OrdinalIgnoreCase),
                    field + ".RawSha256");
                Check(av.ExperienceGained == ev.ExperienceGained, field + ".ExperienceGained");
            }
        }

        void Check(bool condition, string field)
        {
            if (!condition) errors.Add(field + " mismatch");
        }

        void ValidateEgg(int length, string? digest, string side)
        {
            if (length is < 1 or > DaycareReadOnly.MaxParentBytes ||
                digest is null || digest.Length != 64 || !digest.All(Uri.IsHexDigit))
                errors.Add("Daycare.Gen2Egg " + side + " invalid length/digest");
        }

        bool Validate<T>(bool available, IReadOnlyList<T>? facilities, string side)
        {
            if (facilities is null ||
                (available && facilities.Count is < 1 or > DaycareReadOnly.MaxFacilities) ||
                (!available && facilities.Count != 0))
            {
                errors.Add("Daycare " + side + " invalid facility count or availability");
                return false;
            }
            for (int i = 0; i < facilities.Count; i++)
            {
                var facility = facilities[i];
                int index;
                string? seed;
                IReadOnlyList<DaycareSlotReadOnly>? observedSlots = null;
                IReadOnlyList<ExpectedDaycareSlotReadOnly>? expectedSlots = null;
                if (facility is DaycareFacilityReadOnly a)
                {
                    index = a.FacilityIndex;
                    seed = a.SeedHex;
                    observedSlots = a.Slots;
                }
                else if (facility is ExpectedDaycareFacilityReadOnly e)
                {
                    index = e.FacilityIndex;
                    seed = e.SeedHex;
                    expectedSlots = e.Slots;
                }
                else
                {
                    errors.Add("Daycare " + side + " contains null/unknown facility");
                    return false;
                }
                if (index != i || (seed is not null &&
                    (seed.Length is not (8 or 16 or 32) || !seed.All(Uri.IsHexDigit))) ||
                    (observedSlots is null && expectedSlots is null) ||
                    (observedSlots?.Count ?? expectedSlots?.Count) is < 1 or > DaycareReadOnly.SlotsPerFacility)
                {
                    errors.Add($"Daycare {side} facility {i} has invalid shape or seed");
                    return false;
                }
                for (int j = 0; j < (observedSlots?.Count ?? expectedSlots!.Count); j++)
                {
                    int slotIndex;
                    int length;
                    string? digest;
                    if (observedSlots is not null)
                    {
                        var slot = observedSlots[j];
                        if (slot is null) { errors.Add("Daycare " + side + " null slot"); return false; }
                        slotIndex = slot.SlotIndex;
                        length = slot.RawLength;
                        digest = slot.RawSha256;
                    }
                    else
                    {
                        var slot = expectedSlots![j];
                        if (slot is null) { errors.Add("Daycare " + side + " null slot"); return false; }
                        slotIndex = slot.SlotIndex;
                        length = slot.RawLength;
                        digest = slot.RawSha256;
                    }
                    if (slotIndex != j || length is < 1 or > DaycareReadOnly.MaxParentBytes ||
                        digest is null || digest.Length != 64 || !digest.All(Uri.IsHexDigit))
                    {
                        errors.Add($"Daycare {side} facility {i} slot {j} invalid index/length/digest");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
