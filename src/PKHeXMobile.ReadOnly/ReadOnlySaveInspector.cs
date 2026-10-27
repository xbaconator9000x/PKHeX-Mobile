using PKHeX.Core;
using PKHeXMobile.Domain;

namespace PKHeXMobile.ReadOnly;

/// <summary>A displayed occupied slot. Indexes are zero-based and refer to the original save.</summary>
public sealed record PokemonSlot(int SlotIndex, ushort Species, string Nickname, int Level, bool IsEgg, bool IsShiny);

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

        // Do not allow the Core ZIP handler to expand an untrusted archive in this initial reader.
        if (IsCompressedContainer(bytes))
        {
            reason = "Compressed containers are not enabled for inspection.";
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
                    boxes.Add(new BoxSnapshot(box, occupied.ToArray()));
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
                party.ToArray(),
                boxes.ToArray(),
                snapshot.Fingerprint);
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

    private static PokemonSlot ToSlot(int slot, PKM pokemon) =>
        new(slot, pokemon.Species, pokemon.Nickname, pokemon.CurrentLevel, pokemon.IsEgg, pokemon.IsShiny);

    private static bool IsCompressedContainer(ReadOnlySpan<byte> data) =>
        (data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B &&
         data[2] is 0x03 or 0x05 or 0x07 && data[3] is 0x04 or 0x06 or 0x08) ||
        (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B);
}
