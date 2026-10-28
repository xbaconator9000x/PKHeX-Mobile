using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Four physical move positions with stored PP and PP Ups from pinned Core.
/// Never calculate maximum PP, heal a move, normalize a slot, or infer legality.
/// Relearn getters can return zero on formats without relearn storage.
/// </summary>
public sealed record MoveSlotReadOnly(int SlotIndex, ushort MoveId, int CurrentPp, int PpUps);

public sealed record PokemonMoveStateReadOnly(
    int CoreFormat,
    IReadOnlyList<MoveSlotReadOnly> Slots,
    PokemonMoveIds RelearnMoveIds)
{
    public static PokemonMoveStateReadOnly Project(PKM pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var slots = new List<MoveSlotReadOnly>(4)
        {
            new(0, pokemon.Move1, pokemon.Move1_PP, pokemon.Move1_PPUps),
            new(1, pokemon.Move2, pokemon.Move2_PP, pokemon.Move2_PPUps),
            new(2, pokemon.Move3, pokemon.Move3_PP, pokemon.Move3_PPUps),
            new(3, pokemon.Move4, pokemon.Move4_PP, pokemon.Move4_PPUps),
        };
        return new PokemonMoveStateReadOnly(pokemon.Format, slots.AsReadOnly(),
            new PokemonMoveIds(pokemon.RelearnMove1, pokemon.RelearnMove2,
                pokemon.RelearnMove3, pokemon.RelearnMove4));
    }
}

/// <summary>Schema v9: exact four physical positions, including empty slots.</summary>
public sealed record ExpectedMoveSlotReadOnly(
    [property: JsonRequired] int SlotIndex,
    [property: JsonRequired] ushort MoveId,
    [property: JsonRequired] int CurrentPp,
    [property: JsonRequired] int PpUps);

public sealed record ExpectedPokemonMoveStateReadOnly(
    [property: JsonRequired] int CoreFormat,
    [property: JsonRequired] IReadOnlyList<ExpectedMoveSlotReadOnly> Slots,
    [property: JsonRequired] ExpectedPokemonMoveIds RelearnMoveIds);

/// <summary>
/// Compares raw Core getters against a separately authored fixture. Both the
/// observation and oracle are validated; matching malformed data is not parity.
/// </summary>
public static class PokemonMoveStateProjectionVerifier
{
    public static void Compare(PokemonMoveStateReadOnly? actual,
        ExpectedPokemonMoveStateReadOnly? expected, PokemonMoveIds detailMoves,
        ExpectedPokemonMoveIds? expectedDetailMoves, string path, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual is null || expected is null)
        {
            errors.Add(path + (actual is null
                ? " missing from observed projection" : " missing from expectation"));
            return;
        }
        Validate(actual.CoreFormat, actual.Slots, actual.RelearnMoveIds,
            detailMoves, path + " observed", errors);
        if (expectedDetailMoves is null)
            errors.Add(path + " expected detail move IDs missing");
        else
            ValidateExpected(expected, expectedDetailMoves, path + " expected", errors);

        if (actual.CoreFormat != expected.CoreFormat)
            errors.Add(path + ".CoreFormat mismatch");
        if (actual.Slots is null || expected.Slots is null)
            return;
        if (actual.Slots.Count != expected.Slots.Count)
            errors.Add(path + ".Slots.Count mismatch");
        for (int i = 0; i < Math.Min(actual.Slots.Count, expected.Slots.Count); i++)
        {
            var a = actual.Slots[i];
            var e = expected.Slots[i];
            if (a is null || e is null)
                continue;
            if (a.SlotIndex != e.SlotIndex) errors.Add(path + $".Slots[{i}].SlotIndex mismatch");
            if (a.MoveId != e.MoveId) errors.Add(path + $".Slots[{i}].MoveId mismatch");
            if (a.CurrentPp != e.CurrentPp) errors.Add(path + $".Slots[{i}].CurrentPp mismatch");
            if (a.PpUps != e.PpUps) errors.Add(path + $".Slots[{i}].PpUps mismatch");
        }
        if (expected.RelearnMoveIds is null)
            return;
        if (actual.RelearnMoveIds.First != expected.RelearnMoveIds.First)
            errors.Add(path + ".RelearnMoveIds.First mismatch");
        if (actual.RelearnMoveIds.Second != expected.RelearnMoveIds.Second)
            errors.Add(path + ".RelearnMoveIds.Second mismatch");
        if (actual.RelearnMoveIds.Third != expected.RelearnMoveIds.Third)
            errors.Add(path + ".RelearnMoveIds.Third mismatch");
        if (actual.RelearnMoveIds.Fourth != expected.RelearnMoveIds.Fourth)
            errors.Add(path + ".RelearnMoveIds.Fourth mismatch");
    }

    private static void Validate(int format, IReadOnlyList<MoveSlotReadOnly>? slots,
        PokemonMoveIds relearn, PokemonMoveIds moves, string path, List<string> errors)
    {
        if (format is < 1 or > 9)
            errors.Add(path + " unsupported CoreFormat");
        if (slots is null || slots.Count != 4)
        {
            errors.Add(path + " requires exactly four physical slots");
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var s = slots[i];
            if (s is null)
            {
                errors.Add(path + $" null slot {i}");
                continue;
            }
            if (s.SlotIndex != i) errors.Add(path + $" slot {i} out of order");
            if (s.MoveId != Get(moves, i)) errors.Add(path + $" slot {i} inconsistent move ID");
            if (s.CurrentPp is < 0 or > 255) errors.Add(path + $" slot {i} PP outside raw bound");
            if (s.PpUps is < 0 or > 255) errors.Add(path + $" slot {i} PP Ups outside raw byte bound");
        }
        // Modern Core formats expose a full byte for PP Ups, even when game\n        // mechanics conventionally use 0..3. Preserve unusual raw values.\n        // Relearn getters are preserved exactly, including zeros on unsupported
        // formats. They must not be interpreted as proof of relearn capability.
        _ = relearn;
    }

    private static void ValidateExpected(ExpectedPokemonMoveStateReadOnly value,
        ExpectedPokemonMoveIds moves, string path, List<string> errors)
    {
        if (value.CoreFormat is < 1 or > 9)
            errors.Add(path + " unsupported CoreFormat");
        if (value.RelearnMoveIds is null)
            errors.Add(path + " missing relearn move IDs");
        if (value.Slots is null || value.Slots.Count != 4)
        {
            errors.Add(path + " requires exactly four physical slots");
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var s = value.Slots[i];
            if (s is null)
            {
                errors.Add(path + $" null slot {i}");
                continue;
            }
            if (s.SlotIndex != i) errors.Add(path + $" slot {i} out of order");
            if (s.MoveId != Get(moves, i)) errors.Add(path + $" slot {i} inconsistent move ID");
            if (s.CurrentPp is < 0 or > 255) errors.Add(path + $" slot {i} PP outside raw bound");
            if (s.PpUps is < 0 or > 255) errors.Add(path + $" slot {i} PP Ups outside raw byte bound");
        }
    }

    private static ushort Get(PokemonMoveIds moves, int index) => index switch
    {
        0 => moves.First, 1 => moves.Second, 2 => moves.Third, _ => moves.Fourth,
    };
    private static ushort Get(ExpectedPokemonMoveIds moves, int index) => index switch
    {
        0 => moves.First, 1 => moves.Second, 2 => moves.Third, _ => moves.Fourth,
    };
}
