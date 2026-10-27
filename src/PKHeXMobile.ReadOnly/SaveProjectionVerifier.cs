namespace PKHeXMobile.ReadOnly;

/// <summary>
/// A full expected read-only projection supplied independently of this adapter,
/// ideally captured from desktop PKHeX using the EXACT matching Core version.
/// No expected fields are optional; missing fields must fail deserialization or comparison.
/// </summary>
public sealed record ExpectedSaveProjection(
    string CoreSaveType,
    string GameVersion,
    int Generation,
    string TrainerName,
    ushort TrainerId,
    ushort SecretId,
    int PartyCount,
    int BoxCount,
    int BoxSlotCount,
    IReadOnlyList<PokemonSlot> Party,
    IReadOnlyList<BoxSnapshot> Boxes);

/// <summary>
/// Field-by-field read-only comparison. Reports only field paths, never trainer
/// names or Pokémon data, to avoid accidentally publishing private fixture values.
/// A match proves agreement with the supplied expectation, not its independence.
/// </summary>
public static class SaveProjectionVerifier
{
    public static IReadOnlyList<string> Compare(SaveReadOnlyResult actual, ExpectedSaveProjection expected)
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

        void CompareSlots(IReadOnlyList<PokemonSlot> observed, IReadOnlyList<PokemonSlot> reference, string path)
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
            }
        }
    }
}
