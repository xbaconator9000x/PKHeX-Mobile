using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Immutable typed award/memory subset. No reflection, legality evaluation,
/// inferred game identity, or modification of the source entity. The Gen 9
/// flags are NOT a complete ribbon/mark inventory for any game.
/// </summary>
public sealed record PokemonAwardFlag(string Name, bool Owned);
public sealed record PokemonMemoryReadOnly(byte Memory, byte Intensity, byte Feeling, ushort Variable);

public sealed record PokemonAwardsReadOnly(
    IReadOnlyList<PokemonAwardFlag> Gen9Flags,
    byte? ContestMemoryCount,
    byte? BattleMemoryCount,
    bool? HasContestMemoryRibbon,
    bool? HasBattleMemoryRibbon,
    sbyte? AffixedRibbon,
    PokemonMemoryReadOnly? OriginalTrainerMemory,
    PokemonMemoryReadOnly? HandlingTrainerMemory)
{
    public static PokemonAwardsReadOnly Empty { get; } = new(
        Array.Empty<PokemonAwardFlag>(), null, null, null, null, null, null, null);

    public static PokemonAwardsReadOnly Project(PKM pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var flags = new List<PokemonAwardFlag>(11);
        if (pokemon is IRibbonSetCommon9 ribbons)
        {
            flags.Add(new(nameof(ribbons.RibbonChampionPaldea), ribbons.RibbonChampionPaldea));
            flags.Add(new(nameof(ribbons.RibbonOnceInALifetime), ribbons.RibbonOnceInALifetime));
            flags.Add(new(nameof(ribbons.RibbonPartner), ribbons.RibbonPartner));
        }
        if (pokemon is IRibbonSetMark9 marks)
        {
            flags.Add(new(nameof(marks.RibbonMarkJumbo), marks.RibbonMarkJumbo));
            flags.Add(new(nameof(marks.RibbonMarkMini), marks.RibbonMarkMini));
            flags.Add(new(nameof(marks.RibbonMarkItemfinder), marks.RibbonMarkItemfinder));
            flags.Add(new(nameof(marks.RibbonMarkPartner), marks.RibbonMarkPartner));
            flags.Add(new(nameof(marks.RibbonMarkGourmand), marks.RibbonMarkGourmand));
            flags.Add(new(nameof(marks.RibbonMarkAlpha), marks.RibbonMarkAlpha));
            flags.Add(new(nameof(marks.RibbonMarkMightiest), marks.RibbonMarkMightiest));
            flags.Add(new(nameof(marks.RibbonMarkTitan), marks.RibbonMarkTitan));
        }

        var memoryRibbons = pokemon as IRibbonSetMemory6;
        var affixed = pokemon as IRibbonSetAffixed;
        var ot = pokemon as IMemoryOT;
        var ht = pokemon as IMemoryHT;
        return new PokemonAwardsReadOnly(
            flags.AsReadOnly(),
            memoryRibbons?.RibbonCountMemoryContest,
            memoryRibbons?.RibbonCountMemoryBattle,
            memoryRibbons?.HasContestMemoryRibbon,
            memoryRibbons?.HasBattleMemoryRibbon,
            affixed?.AffixedRibbon,
            ot is null ? null : new(ot.OriginalTrainerMemory,
                ot.OriginalTrainerMemoryIntensity, ot.OriginalTrainerMemoryFeeling,
                ot.OriginalTrainerMemoryVariable),
            ht is null ? null : new(ht.HandlingTrainerMemory,
                ht.HandlingTrainerMemoryIntensity, ht.HandlingTrainerMemoryFeeling,
                ht.HandlingTrainerMemoryVariable));
    }
}
