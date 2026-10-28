using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Immutable Core national-species seen/caught subset. NOT regional/DLC Pokédex
/// parity: forms, language, display, research, gender and shiny variants are
/// deliberately omitted. MaxSpeciesID is a scan bound, not a regional count.
/// This class never mutates, serializes or authorizes writing a save.
/// </summary>
public sealed record PokedexReadOnly(
    ushort MaxSpeciesId,
    IReadOnlyList<ushort> SeenSpecies,
    IReadOnlyList<ushort> CaughtSpecies)
{
    public int SeenCount => SeenSpecies.Count;
    public int CaughtCount => CaughtSpecies.Count;

    public static PokedexReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!save.HasPokeDex)
            return null;

        ushort max = save.MaxSpeciesID;
        if (max is 0 or > 2048)
            throw new InvalidDataException("Core reported an unsupported Pokédex scan bound.");

        var seen = new List<ushort>();
        var caught = new List<ushort>();
        for (int i = 1; i <= max; i++)
        {
            ushort species = checked((ushort)i);
            if (save.GetSeen(species))
                seen.Add(species);
            if (save.GetCaught(species))
                caught.Add(species);
        }
        return new PokedexReadOnly(max, seen.AsReadOnly(), caught.AsReadOnly());
    }
}
