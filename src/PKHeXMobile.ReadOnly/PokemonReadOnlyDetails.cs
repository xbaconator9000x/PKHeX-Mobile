namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Immutable scalar projection only. The Core's IV/EV properties have
/// generation-specific meanings (for example, Gen 1 DVs and stat experience).
/// IDs are raw Core identifiers, not localized names or legality verdicts.
/// </summary>
public readonly record struct PokemonMoveIds(ushort First, ushort Second, ushort Third, ushort Fourth);

public readonly record struct PokemonStatSpread(
    int HP, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed);

public sealed record PokemonReadOnlyDetails(
    string Nature,
    int AbilityId,
    int HeldItemId,
    byte Gender,
    byte Form,
    PokemonMoveIds Moves,
    PokemonStatSpread IndividualValues,
    PokemonStatSpread EffortValues);
