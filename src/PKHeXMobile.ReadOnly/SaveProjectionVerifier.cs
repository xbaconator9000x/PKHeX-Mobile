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
    [property: JsonRequired] ExpectedPokemonStatSpread EffortValues)
{
    // Optional for schema v2; required for every occupied slot in schema v3.
    public ExpectedPokemonAwardsReadOnly? Awards { get; init; }
    // Schema v8: derived, format-aware raw training diagnostics.
    public ExpectedPokemonTrainingReadOnly? Training { get; init; }
    // Schema v9: stored move PP, PP Ups and raw relearn getters.
    public ExpectedPokemonMoveStateReadOnly? MoveState { get; init; }
}

/// <summary>Schema v4: Core national-species seen/caught flags only.</summary>
public sealed record ExpectedPokedexReadOnly(
    [property: JsonRequired] ushort MaxSpeciesId,
    [property: JsonRequired] IReadOnlyList<ushort> SeenSpecies,
    [property: JsonRequired] IReadOnlyList<ushort> CaughtSpecies);

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
    [property: JsonRequired] IReadOnlyList<ExpectedBoxSnapshot> Boxes)
{
    // Optional in v1–v3, mandatory non-null for v4 dex-enabled fixtures.
    public ExpectedPokedexReadOnly? Pokedex { get; init; }
    // Required for schema v5 fixtures with Core-enumerated inventory.
    public ExpectedInventoryReadOnly? Inventory { get; init; }
    // Required for schema v6: every box slot, including empty/locked slots.
    public ExpectedStorageFlagsReadOnly? Storage { get; init; }
    // Schema v7: raw Core trainer progress scalars, not full trainer records.
    public ExpectedTrainerProgressReadOnly? TrainerProgress { get; init; }
}

/// <summary>
/// Field-by-field read-only comparison. Reports only field paths, never trainer
/// names or Pokémon data, to avoid accidentally publishing private fixture values.
/// A match proves agreement with the supplied expectation, not its independence.
/// </summary>
public static class SaveProjectionVerifier
{
    public static IReadOnlyList<string> Compare(SaveReadOnlyResult actual, ExpectedSaveProjection expected,
        bool requireDetails = false, bool requireAwards = false, bool requirePokedex = false,
        bool requireInventory = false, bool requireStorage = false,
        bool requireTrainerProgress = false, bool requireTraining = false,
        bool requireMoveState = false)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        if (requireAwards || requireTraining || requireMoveState) requireDetails = true;
        var errors = new List<string>();

        // Required JSON members can explicitly be null. A malformed expected
        // OR observed projection must not crash or yield a false parity PASS.
        if (actual.Party is null) errors.Add("Party missing from observed projection");
        if (actual.Boxes is null) errors.Add("Boxes missing from observed projection");
        if (expected.Party is null) errors.Add("Party missing from expectation");
        if (expected.Boxes is null) errors.Add("Boxes missing from expectation");
        if (errors.Count != 0) return errors;
        if (actual.Party.Count > 6 || expected.Party.Count > 6 ||
            actual.Boxes.Count > 64 || expected.Boxes.Count > 64)
        {
            errors.Add("Projection collection exceeds bounded read-only limits");
            return errors;
        }
        if (actual.Boxes.Any(box => box is null || box.OccupiedSlots is null) ||
            expected.Boxes.Any(box => box is null || box.OccupiedSlots is null))
        {
            errors.Add("Projection contains a null box or occupied slot list");
            return errors;
        }

        Check(!string.IsNullOrWhiteSpace(actual.CoreSaveType) &&
            !string.IsNullOrWhiteSpace(expected.CoreSaveType), "CoreSaveType missing");
        Check(!string.IsNullOrWhiteSpace(actual.GameVersion) &&
            !string.IsNullOrWhiteSpace(expected.GameVersion), "GameVersion missing");
        Check(actual.TrainerName is not null && expected.TrainerName is not null, "TrainerName null");

        Check(actual.CoreSaveType == expected.CoreSaveType, "CoreSaveType");
        Check(actual.GameVersion == expected.GameVersion, "GameVersion");
        Check(actual.Generation == expected.Generation, "Generation");
        Check(actual.TrainerName == expected.TrainerName, "TrainerName");
        Check(actual.TrainerId == expected.TrainerId, "TrainerId");
        Check(actual.SecretId == expected.SecretId, "SecretId");
        Check(actual.PartyCount == expected.PartyCount, "PartyCount");
        Check(actual.BoxCount == expected.BoxCount, "BoxCount");
        Check(actual.BoxSlotCount == expected.BoxSlotCount, "BoxSlotCount");

        Check(actual.PartyCount is >= 0 and <= 6, "PartyCount observed out of range");
        Check(expected.PartyCount is >= 0 and <= 6, "PartyCount expected out of range");
        ValidateIndices(actual.Party, actual.PartyCount, "Party", "observed");
        if (expected.Party is null)
            errors.Add("Party missing from expectation");
        else
        {
            ValidateIndices(expected.Party, expected.PartyCount, "Party", "expected");
            CompareSlots(actual.Party, expected.Party, "Party");
        }

        Check(actual.BoxCount == actual.Boxes.Count, "Boxes observed count inconsistent");
        Check(actual.BoxCount is >= 0 and <= 64, "BoxCount observed out of range");
        Check(actual.BoxSlotCount is >= 0 and <= 64, "BoxSlotCount observed out of range");
        Check(expected.BoxCount is >= 0 and <= 64, "BoxCount expected out of range");
        Check(expected.BoxSlotCount is >= 0 and <= 64, "BoxSlotCount expected out of range");
        for (int i = 0; i < actual.Boxes.Count; i++)
        {
            Check(actual.Boxes[i].BoxIndex == i, $"Boxes[{i}].BoxIndex observed out of order");
            ValidateIndices(actual.Boxes[i].OccupiedSlots, actual.BoxSlotCount,
                $"Boxes[{i}].OccupiedSlots", "observed");
        }
        if (expected.Boxes is null)
            errors.Add("Boxes missing from expectation");
        else
        {
            Check(expected.BoxCount == expected.Boxes.Count, "Boxes expected count inconsistent");
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
                Check(e.BoxIndex == i, $"Boxes[{i}].BoxIndex expected out of order");
                if (e.OccupiedSlots is null)
                    errors.Add($"Boxes[{i}].OccupiedSlots missing");
                else
                {
                    ValidateIndices(e.OccupiedSlots, expected.BoxSlotCount,
                        $"Boxes[{i}].OccupiedSlots", "expected");
                    CompareSlots(a.OccupiedSlots, e.OccupiedSlots, $"Boxes[{i}].OccupiedSlots");
                }
            }
        }

        if (requirePokedex || expected.Pokedex is not null)
            ComparePokedex(actual.Pokedex, expected.Pokedex);
        if (requireInventory || expected.Inventory is not null)
            InventoryProjectionVerifier.Compare(actual.Inventory, expected.Inventory, errors);
        if (requireStorage || expected.Storage is not null)
            StorageFlagsProjectionVerifier.Compare(actual.Storage, expected.Storage,
                actual.BoxCount, actual.BoxSlotCount, errors);
        if (requireTrainerProgress || expected.TrainerProgress is not null)
            TrainerProgressProjectionVerifier.Compare(actual.TrainerProgress,
                expected.TrainerProgress, actual.BoxCount, errors);

        return errors;

        void Check(bool equal, string path)
        {
            if (!equal)
                errors.Add($"{path} mismatch");
        }

        // A matching but malformed oracle must not produce a false parity PASS.
        // This validation is not an independent source or save-format proof.
        void ValidateIndices<T>(IReadOnlyList<T> slots, int capacity, string path, string side)
            where T : class
        {
            var seen = new HashSet<int>();
            foreach (var slot in slots)
            {
                if (slot is null)
                {
                    errors.Add($"{path} {side} contains null slot");
                    continue;
                }
                int index = slot switch
                {
                    PokemonSlot p => p.SlotIndex,
                    ExpectedPokemonSlot p => p.SlotIndex,
                    _ => throw new InvalidOperationException("Unexpected slot type"),
                };
                if (index < 0 || index >= capacity)
                    errors.Add($"{path} {side} slot index out of range");
                if (!seen.Add(index))
                    errors.Add($"{path} {side} duplicate slot index {index}");
            }
        }

        void CompareSlots(IReadOnlyList<PokemonSlot> observed, IReadOnlyList<ExpectedPokemonSlot> reference, string path)
        {
            if (observed.Any(slot => slot is null) || reference.Any(slot => slot is null))
            {
                errors.Add($"{path} contains a null occupied slot");
                return;
            }
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
                Check(a.Nickname is not null && e.Nickname is not null,
                    $"{path}[{i}].Nickname null");
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
            Check(a.Nature is not null && e.Nature is not null, $"{path}.Nature null");
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
            if (requireTraining || e.Training is not null)
                PokemonTrainingProjectionVerifier.Compare(a.Training, e.Training,
                    a.IndividualValues, a.EffortValues, $"{path}.Training", errors);
            if (requireMoveState || e.MoveState is not null)
                PokemonMoveStateProjectionVerifier.Compare(a.MoveState, e.MoveState,
                    a.Moves, e.Moves, $"{path}.MoveState", errors);
            if (e.Awards is null)
            {
                if (requireAwards) errors.Add($"{path}.Awards missing");
            }
            else if (a.Awards is null)
                errors.Add($"{path}.Awards missing from observed projection");
            else
                AwardProjectionVerifier.Compare(a.Awards, e.Awards, $"{path}.Awards", errors);
        }

        void ComparePokedex(PokedexReadOnly? a, ExpectedPokedexReadOnly? e)
        {
            if (a is null || e is null)
            {
                errors.Add(a is null ? "Pokedex missing from observed projection" :
                    "Pokedex missing from expectation");
                return;
            }
            Check(a.MaxSpeciesId is > 0 and <= 2048, "Pokedex observed MaxSpeciesId out of range");
            Check(e.MaxSpeciesId is > 0 and <= 2048, "Pokedex expected MaxSpeciesId out of range");
            Check(a.MaxSpeciesId == e.MaxSpeciesId, "Pokedex.MaxSpeciesId");
            ValidateSpecies(a.SeenSpecies, a.MaxSpeciesId, "Pokedex observed SeenSpecies");
            ValidateSpecies(a.CaughtSpecies, a.MaxSpeciesId, "Pokedex observed CaughtSpecies");
            ValidateSpecies(e.SeenSpecies, e.MaxSpeciesId, "Pokedex expected SeenSpecies");
            ValidateSpecies(e.CaughtSpecies, e.MaxSpeciesId, "Pokedex expected CaughtSpecies");
            CompareSpecies(a.SeenSpecies, e.SeenSpecies, "Pokedex.SeenSpecies");
            CompareSpecies(a.CaughtSpecies, e.CaughtSpecies, "Pokedex.CaughtSpecies");
        }

        void ValidateSpecies(IReadOnlyList<ushort>? species, ushort max, string path)
        {
            if (species is null)
            {
                errors.Add(path + " missing");
                return;
            }
            if (species.Count > 2048 || species.Count > max)
            {
                errors.Add(path + " exceeds scan bound");
                return;
            }
            ushort previous = 0;
            foreach (ushort id in species)
            {
                if (id == 0 || id > max || id <= previous)
                {
                    errors.Add(path + " invalid, duplicated or unordered species");
                    return;
                }
                previous = id;
            }
        }

        void CompareSpecies(IReadOnlyList<ushort>? a, IReadOnlyList<ushort>? e, string path)
        {
            if (a is null || e is null || a.Count > 2048 || e.Count > 2048)
                return;
            Check(a.Count == e.Count, path + ".Count");
            for (int i = 0; i < Math.Min(a.Count, e.Count); i++)
                Check(a[i] == e[i], path + "[" + i + "]");
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
    public static bool TryClonePreservingIdentity(PKHeX.Core.DataMysteryGift original,
        out PKHeX.Core.DataMysteryGift? clone)
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
