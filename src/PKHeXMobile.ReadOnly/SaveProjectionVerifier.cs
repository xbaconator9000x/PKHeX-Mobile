using System.Text.Json.Serialization;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Strict manifest-only types: every expected field must be present in JSON,
/// including zero/false and empty arrays. A missing field must never silently
/// become a matching default value.
/// </summary>
public sealed record ExpectedPokemonSlot(
    [property: JsonRequired] int SlotIndex,
    [property: JsonRequired] ushort Species,
    [property: JsonRequired] string Nickname,
    [property: JsonRequired] int Level,
    [property: JsonRequired] bool IsEgg,
    [property: JsonRequired] bool IsShiny)
{
    // Optional in schema v1; required and fully compared in schema v2.
    public ExpectedPokemonReadOnlyDetails? Details { get; init; }
}

public sealed record ExpectedPokemonMoveIds(
    [property: JsonRequired] ushort First,
    [property: JsonRequired] ushort Second,
    [property: JsonRequired] ushort Third,
    [property: JsonRequired] ushort Fourth);

public sealed record ExpectedPokemonStatSpread(
    [property: JsonRequired] int HP,
    [property: JsonRequired] int Attack,
    [property: JsonRequired] int Defense,
    [property: JsonRequired] int SpecialAttack,
    [property: JsonRequired] int SpecialDefense,
    [property: JsonRequired] int Speed);

public sealed record ExpectedPokemonReadOnlyDetails(
    [property: JsonRequired] string Nature,
    [property: JsonRequired] int AbilityId,
    [property: JsonRequired] int HeldItemId,
    [property: JsonRequired] byte Gender,
    [property: JsonRequired] byte Form,
    [property: JsonRequired] ExpectedPokemonMoveIds Moves,
    [property: JsonRequired] ExpectedPokemonStatSpread IndividualValues,
    [property: JsonRequired] ExpectedPokemonStatSpread EffortValues);

public sealed record ExpectedBoxSnapshot(
    [property: JsonRequired] int BoxIndex,
    [property: JsonRequired] IReadOnlyList<ExpectedPokemonSlot> OccupiedSlots);

/// <summary>
/// A full expected read-only projection supplied independently of this adapter,
/// ideally captured from desktop PKHeX using the EXACT matching Core version.
/// </summary>
public sealed record ExpectedSaveProjection(
    [property: JsonRequired] string CoreSaveType,
    [property: JsonRequired] string GameVersion,
    [property: JsonRequired] int Generation,
    [property: JsonRequired] string TrainerName,
    [property: JsonRequired] ushort TrainerId,
    [property: JsonRequired] ushort SecretId,
    [property: JsonRequired] int PartyCount,
    [property: JsonRequired] int BoxCount,
    [property: JsonRequired] int BoxSlotCount,
    [property: JsonRequired] IReadOnlyList<ExpectedPokemonSlot> Party,
    [property: JsonRequired] IReadOnlyList<ExpectedBoxSnapshot> Boxes);

/// <summary>
/// Field-by-field read-only comparison. Reports only field paths, never trainer
/// names or Pokémon data, to avoid accidentally publishing private fixture values.
/// A match proves agreement with the supplied expectation, not its independence.
/// </summary>
public static class SaveProjectionVerifier
{
    public static IReadOnlyList<string> Compare(SaveReadOnlyResult actual, ExpectedSaveProjection expected, bool requireDetails = false)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        var errors = new List<string>();

        Check(actual.CoreSaveType == expected.CoreSaveType, "CoreSaveType");
        Check(actual.GameVersion == expected.GameVersion, "GameVersion");
        Check(actual.Generation == expected.Generation, "Generation");
        Check(actual.TrainerName == expected.TrainerName, "TrainerName");
        Check(actual.TrainerId == expected.TrainerId, "TrainerId");
        Check(actual.SecretId == expected.SecretId, "SecretId");
        Check(actual.PartyCount == expected.PartyCount, "PartyCount");
        Check(actual.BoxCount == expected.BoxCount, "BoxCount");
        Check(actual.BoxSlotCount == expected.BoxSlotCount, "BoxSlotCount");

        if (expected.Party is null)
            errors.Add("Party missing from expectation");
        else
            CompareSlots(actual.Party, expected.Party, "Party");

        if (expected.Boxes is null)
            errors.Add("Boxes missing from expectation");
        else
        {
            Check(actual.Boxes.Count == expected.Boxes.Count, "Boxes.Count");
            for (int i = 0; i < Math.Min(actual.Boxes.Count, expected.Boxes.Count); i++)
            {
                var a = actual.Boxes[i];
                var e = expected.Boxes[i];
                if (e is null)
                {
                    errors.Add($"Boxes[{i}] missing");
                    continue;
                }
                Check(a.BoxIndex == e.BoxIndex, $"Boxes[{i}].BoxIndex");
                if (e.OccupiedSlots is null)
                    errors.Add($"Boxes[{i}].OccupiedSlots missing");
                else
                    CompareSlots(a.OccupiedSlots, e.OccupiedSlots, $"Boxes[{i}].OccupiedSlots");
            }
        }

        return errors;

        void Check(bool equal, string path)
        {
            if (!equal)
                errors.Add($"{path} mismatch");
        }

        void CompareSlots(IReadOnlyList<PokemonSlot> observed, IReadOnlyList<ExpectedPokemonSlot> reference, string path)
        {
            Check(observed.Count == reference.Count, $"{path}.Count");
            for (int i = 0; i < Math.Min(observed.Count, reference.Count); i++)
            {
                var a = observed[i];
                var e = reference[i];
                if (e is null)
                {
                    errors.Add($"{path}[{i}] missing");
                    continue;
                }
                Check(a.SlotIndex == e.SlotIndex, $"{path}[{i}].SlotIndex");
                Check(a.Species == e.Species, $"{path}[{i}].Species");
                Check(a.Nickname == e.Nickname, $"{path}[{i}].Nickname");
                Check(a.Level == e.Level, $"{path}[{i}].Level");
                Check(a.IsEgg == e.IsEgg, $"{path}[{i}].IsEgg");
                Check(a.IsShiny == e.IsShiny, $"{path}[{i}].IsShiny");
                var detailPath = $"{path}[{i}].Details";
                if (e.Details is null)
                {
                    if (requireDetails)
                        errors.Add($"{detailPath} missing");
                }
                else if (a.Details is null)
                    errors.Add($"{detailPath} missing from observed projection");
                else
                    CompareDetails(a.Details, e.Details, detailPath);
            }
        }

        void CompareDetails(PokemonReadOnlyDetails a, ExpectedPokemonReadOnlyDetails e, string path)
        {
            Check(a.Nature == e.Nature, $"{path}.Nature");
            Check(a.AbilityId == e.AbilityId, $"{path}.AbilityId");
            Check(a.HeldItemId == e.HeldItemId, $"{path}.HeldItemId");
            Check(a.Gender == e.Gender, $"{path}.Gender");
            Check(a.Form == e.Form, $"{path}.Form");
            if (e.Moves is null)
                errors.Add($"{path}.Moves missing");
            else
            {
                Check(a.Moves.First == e.Moves.First, $"{path}.Moves.First");
                Check(a.Moves.Second == e.Moves.Second, $"{path}.Moves.Second");
                Check(a.Moves.Third == e.Moves.Third, $"{path}.Moves.Third");
                Check(a.Moves.Fourth == e.Moves.Fourth, $"{path}.Moves.Fourth");
            }
            CompareSpread(a.IndividualValues, e.IndividualValues, $"{path}.IndividualValues");
            CompareSpread(a.EffortValues, e.EffortValues, $"{path}.EffortValues");
        }

        void CompareSpread(PokemonStatSpread a, ExpectedPokemonStatSpread? e, string path)
        {
            if (e is null)
            {
                errors.Add($"{path} missing");
                return;
            }
            Check(a.HP == e.HP, $"{path}.HP");
            Check(a.Attack == e.Attack, $"{path}.Attack");
            Check(a.Defense == e.Defense, $"{path}.Defense");
            Check(a.SpecialAttack == e.SpecialAttack, $"{path}.SpecialAttack");
            Check(a.SpecialDefense == e.SpecialDefense, $"{path}.SpecialDefense");
            Check(a.Speed == e.Speed, $"{path}.Speed");
        }
    }
}

/// <summary>
/// Fail-closed guard for future read-only gift inspection. Core's clone method
/// is not assumed to preserve the template type or game context (notably WA9).
/// Never authorize a gift edit, import or export based on a successful clone.
/// </summary>
public static class MysteryGiftCloneIdentity
{
    public static bool TryClonePreservingIdentity(PKHeX.Core.MysteryGift original,
        out PKHeX.Core.MysteryGift? clone)
    {
        ArgumentNullException.ThrowIfNull(original);
        clone = null;
        var candidate = original.Clone();
        if (candidate.GetType() != original.GetType() ||
            candidate.Context != original.Context ||
            !candidate.Data.SequenceEqual(original.Data))
            return false;
        clone = candidate;
        return true;
    }
}
