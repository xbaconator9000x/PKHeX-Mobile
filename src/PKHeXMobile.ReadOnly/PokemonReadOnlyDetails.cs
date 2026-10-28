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
    PokemonStatSpread EffortValues)
{
    // 1/2 identifies original Game Boy mechanics; 0 means unknown/other.
    // This is presentation context, NEVER format identity or write authority.
    public int MechanicsGeneration { get; init; }

    // Explicitly partial, typed, read-only subset. Never a legality verdict.
    public PokemonAwardsReadOnly Awards { get; init; } = PokemonAwardsReadOnly.Empty;

    // Derived diagnostic scalars, not legality or edit authorization.
    public PokemonTrainingReadOnly? Training { get; init; }

    // Schema v9: physical move positions and stored PP; never auto-heal on view.
    public PokemonMoveStateReadOnly? MoveState { get; init; }

    // Schema v10: detached raw PID/EC, shiny, ability-slot, egg and date diagnostics.
    public PokemonIdentityReadOnly? Identity { get; init; }
}
