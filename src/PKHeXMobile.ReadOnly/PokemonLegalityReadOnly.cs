using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Party-only experimental legality diagnostics from official PKHeX.Core.
/// A "Valid" result is relative to the supplied RETAIL Core personal table;
/// it does not authenticate a save, ROM hack, trade or write-safe profile.
/// </summary>
public sealed record PokemonLegalityReadOnly(
    bool Available, bool Valid, IReadOnlyList<LegalityTopic> Topics)
{
    public static PokemonLegalityReadOnly Unavailable { get; } =
        new(false, false, Array.Empty<LegalityTopic>());

    public static PokemonLegalityReadOnly Project(PKM pokemon, IPersonalTable table)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(table);
        if (pokemon.Species == 0) return Unavailable;
        try
        {
            // PKForge's preview-first approach: analyze a detached entity, never
            // expose or mutate the original Core slot returned by the save.
            var detached = pokemon.Clone();
            var original = detached.Data.ToArray();
            var analysis = new LegalityAnalysis(detached, table);
            if (!detached.Data.SequenceEqual(original) || !analysis.Parsed)
                return Unavailable;
            return new PokemonLegalityReadOnly(true, analysis.Valid,
                PortedLegalityChecks.Group(analysis));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            // Malformed/unsupported entities must never be labelled legal.
            return Unavailable;
        }
    }
}
