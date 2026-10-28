using PKHeX.Core;
using PKHeXMobile.Domain;

namespace PKHeXMobile.ReadOnly;

/// <summary>A displayed occupied slot. Indexes are zero-based and refer to the original save.</summary>
public sealed record PokemonSlot(int SlotIndex, ushort Species, string Nickname, int Level, bool IsEgg, bool IsShiny, PokemonReadOnlyDetails? Details = null);

/// <summary>Occupied slots only; missing indexes are empty or unreported.</summary>
public sealed record BoxSnapshot(int BoxIndex, IReadOnlyList<PokemonSlot> OccupiedSlots);

/// <summary>
/// Experimental read-only projection. A successful Core parse does NOT prove vanilla layout,
/// establish a verified hack profile, or grant any permission to export a save.
/// </summary>
public sealed record SaveReadOnlyResult(
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
    IReadOnlyList<BoxSnapshot> Boxes,
    SaveFingerprint OriginalFingerprint)
{
    public SaveCompatibility Compatibility => SaveCompatibility.SupportedReadOnly;
    public string? VerifiedProfileId => null;

    // Core national flags only, never regional/DLC Pokédex certification.
    public PokedexReadOnly? Pokedex { get; init; }
}

public static class ReadOnlySaveInspector
{
    /// <summary>
    /// Inspect an isolated copy of bounded bytes with pinned PKHeX.Core.
    /// Never calls SaveFile.Write(), changes a slot, or exposes the parsed mutable SaveFile.
    /// All results are provisional until positive/negative fixture comparison is complete.
    /// </summary>
    public static bool TryInspect(
        SaveInputSnapshot snapshot,
        out SaveReadOnlyResult? result,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        result = null;
        var bytes = snapshot.CopyBytes();

        // Recognized high-risk hack markers must never be passed through a retail
        // parser and shown as though its species/party/box interpretation is reliable.
        if (SaveInputRiskAnalyzer.Assess(bytes) is { } risk)
        {
            reason = risk.Explanation;
            return false;
        }

        try
        {
            if (!SaveUtil.TryGetSaveFile(bytes, out var save) || save is null)
            {
                reason = "PKHeX.Core did not recognize a save.";
                return false;
            }

            if (!save.ChecksumsValid)
            {
                reason = "PKHeX.Core reported invalid save checksums.";
                return false;
            }

            // Defensive limits for unverified formats and future Core changes.
            if (save.PartyCount is < 0 or > 6 ||
                save.BoxCount is < 0 or > 64 ||
                save.BoxSlotCount is < 0 or > 64 ||
                (!save.HasParty && !save.HasBox))
            {
                reason = "The detected save exceeds the provisional reader's supported layout limits.";
                return false;
            }

            var party = new List<PokemonSlot>(save.PartyCount);
            if (save.HasParty)
            {
                for (int i = 0; i < save.PartyCount; i++)
                {
                    var pokemon = save.GetPartySlotAtIndex(i);
                    if (pokemon.Species != 0)
                        party.Add(ToSlot(i, pokemon));
                }
            }

            var boxes = new List<BoxSnapshot>(save.BoxCount);
            if (save.HasBox)
            {
                for (int box = 0; box < save.BoxCount; box++)
                {
                    var occupied = new List<PokemonSlot>();
                    for (int slot = 0; slot < save.BoxSlotCount; slot++)
                    {
                        var pokemon = save.GetBoxSlotAtIndex(box, slot);
                        if (pokemon.Species != 0)
                            occupied.Add(ToSlot(slot, pokemon));
                    }
                    // Do not expose a mutable array behind IReadOnlyList.
                    boxes.Add(new BoxSnapshot(box, occupied.AsReadOnly()));
                }
            }

            result = new SaveReadOnlyResult(
                save.GetType().Name,
                save.Version.ToString(),
                save.Generation,
                save.OT,
                save.TID16,
                save.SID16,
                save.PartyCount,
                save.BoxCount,
                save.BoxSlotCount,
                party.AsReadOnly(),
                boxes.AsReadOnly(),
                snapshot.Fingerprint)
            {
                Pokedex = PokedexReadOnly.Project(save),
            };
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            // Parsing arbitrary bytes can fail at any format-specific field. Fail closed.
            reason = "The save could not be safely inspected by the pinned reader.";
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Extract only immutable scalar data from a Core entity. Does not mutate the
    /// PKM, retain it, or infer game legality or a write-safe save layout.
    /// Numeric move/ability/item IDs are Core IDs, not localized names.
    /// </summary>
    public static PokemonSlot ProjectPokemon(int slot, PKM pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new PokemonSlot(
            slot, pokemon.Species, pokemon.Nickname, pokemon.CurrentLevel,
            pokemon.IsEgg, pokemon.IsShiny,
            new PokemonReadOnlyDetails(
                pokemon.Nature.ToString(), pokemon.Ability, pokemon.HeldItem,
                pokemon.Gender, pokemon.Form,
                new PokemonMoveIds(pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4),
                new PokemonStatSpread(pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF,
                    pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE),
                new PokemonStatSpread(pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF,
                    pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE))
            {
                MechanicsGeneration = pokemon is PK1 ? 1 : pokemon is PK2 ? 2 : 0,
                Awards = PokemonAwardsReadOnly.Project(pokemon),
            });
    }

    private static PokemonSlot ToSlot(int slot, PKM pokemon) => ProjectPokemon(slot, pokemon);
}
