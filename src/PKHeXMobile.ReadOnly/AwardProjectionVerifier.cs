using System.Text.Json.Serialization;

namespace PKHeXMobile.ReadOnly;

/// <summary>Schema v3 expected typed subset; nullable members are still required JSON keys.</summary>
public sealed record ExpectedPokemonAwardFlag(
    [property: JsonRequired] string Name,
    [property: JsonRequired] bool Owned);

public sealed record ExpectedPokemonMemoryReadOnly(
    [property: JsonRequired] byte Memory,
    [property: JsonRequired] byte Intensity,
    [property: JsonRequired] byte Feeling,
    [property: JsonRequired] ushort Variable);

public sealed record ExpectedPokemonAwardsReadOnly(
    [property: JsonRequired] IReadOnlyList<ExpectedPokemonAwardFlag> Gen9Flags,
    [property: JsonRequired] byte? ContestMemoryCount,
    [property: JsonRequired] byte? BattleMemoryCount,
    [property: JsonRequired] bool? HasContestMemoryRibbon,
    [property: JsonRequired] bool? HasBattleMemoryRibbon,
    [property: JsonRequired] sbyte? AffixedRibbon,
    [property: JsonRequired] ExpectedPokemonMemoryReadOnly? OriginalTrainerMemory,
    [property: JsonRequired] ExpectedPokemonMemoryReadOnly? HandlingTrainerMemory);

/// <summary>
/// Strictly compare only the explicitly implemented typed subset. All output
/// is field paths, never personal fixture values. A match does not establish
/// independent desktop parity, a valid save layout or any permission to write.
/// </summary>
public static class AwardProjectionVerifier
{
    private static readonly string[] Names =
    [
        "RibbonChampionPaldea", "RibbonOnceInALifetime", "RibbonPartner",
        "RibbonMarkJumbo", "RibbonMarkMini", "RibbonMarkItemfinder",
        "RibbonMarkPartner", "RibbonMarkGourmand", "RibbonMarkAlpha",
        "RibbonMarkMightiest", "RibbonMarkTitan",
    ];

    public static void Compare(PokemonAwardsReadOnly actual,
        ExpectedPokemonAwardsReadOnly expected, string path, ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(errors);
        if (actual.Gen9Flags is null || expected.Gen9Flags is null)
        {
            errors.Add(path + ".Gen9Flags missing");
            return;
        }

        Validate(actual.Gen9Flags.Select(f => f?.Name).ToArray(), "observed");
        Validate(expected.Gen9Flags.Select(f => f?.Name).ToArray(), "expected");
        Check(actual.Gen9Flags.Count == expected.Gen9Flags.Count, ".Gen9Flags.Count");
        for (int i = 0; i < Math.Min(actual.Gen9Flags.Count, expected.Gen9Flags.Count); i++)
        {
            var a = actual.Gen9Flags[i];
            var e = expected.Gen9Flags[i];
            if (a is null || e is null)
                continue;
            Check(a.Name == e.Name, $".Gen9Flags[{i}].Name");
            Check(a.Owned == e.Owned, $".Gen9Flags[{i}].Owned");
        }
        Check(actual.ContestMemoryCount == expected.ContestMemoryCount, ".ContestMemoryCount");
        Check(actual.BattleMemoryCount == expected.BattleMemoryCount, ".BattleMemoryCount");
        Check(actual.HasContestMemoryRibbon == expected.HasContestMemoryRibbon, ".HasContestMemoryRibbon");
        Check(actual.HasBattleMemoryRibbon == expected.HasBattleMemoryRibbon, ".HasBattleMemoryRibbon");
        Check(actual.AffixedRibbon == expected.AffixedRibbon, ".AffixedRibbon");
        CompareMemory(actual.OriginalTrainerMemory, expected.OriginalTrainerMemory, ".OriginalTrainerMemory");
        CompareMemory(actual.HandlingTrainerMemory, expected.HandlingTrainerMemory, ".HandlingTrainerMemory");

        void Check(bool condition, string suffix)
        {
            if (!condition) errors.Add(path + suffix + " mismatch");
        }

        void Validate(string?[] names, string side)
        {
            // A partial list is legal when the Core type exposes only one of
            // the two typed interfaces, but duplicates/reordering/unknown
            // names can never silently match an invalid baseline.
            int previous = -1;
            foreach (var name in names)
            {
                int index = Array.IndexOf(Names, name);
                if (index <= previous || index < 0)
                {
                    errors.Add(path + ".Gen9Flags " + side + " invalid typed subset");
                    return;
                }
                previous = index;
            }
        }

        void CompareMemory(PokemonMemoryReadOnly? a, ExpectedPokemonMemoryReadOnly? e, string suffix)
        {
            if (a is null || e is null)
            {
                Check(a is null && e is null, suffix);
                return;
            }
            Check(a.Memory == e.Memory, suffix + ".Memory");
            Check(a.Intensity == e.Intensity, suffix + ".Intensity");
            Check(a.Feeling == e.Feeling, suffix + ".Feeling");
            Check(a.Variable == e.Variable, suffix + ".Variable");
        }
    }
}
