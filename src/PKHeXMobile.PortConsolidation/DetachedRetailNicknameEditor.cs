// GPL-3.0-only. Adapter to PKForge's existing EntityEdit/SaveEngineSession contracts,
// pinned PKForge 2df04430e532b78c247f45b936f3068d8f824987.
// Built only with the retail-only official PKHeX.Core 26.10.18 adaptation.
// This component is a NON-PUBLISHING edit preview, not a save writer or format oracle.
using PKForge.Domain;
using PKForge.Engine;
using PKHeXMobile.Domain;

namespace PKHeXMobile.PortConsolidation;

/// <summary>
/// Detached, verified nickname preview using the existing PKForge editor engine.
/// No live session, Android document URI, writable stream or publisher is accepted.
/// Candidate bytes are never evidence of safe export without independently
/// authenticated game layout, desktop parity, scoped byte audit and provider tests.
/// </summary>
public static class DetachedRetailNicknameEditor
{
    public static RetailNicknamePreview Preview(
        SaveInputSnapshot source, int box, int slot, string nickname)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(nickname);
        if (nickname.Length == 0 || nickname.Length > 64 || string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname must be nonempty and at most 64 characters.", nameof(nickname));

        // The source is immutable. Two independent byte arrays prevent PKHeX parser
        // normalization and PKForge's mutable session from reaching the caller's data.
        var original = source.CopyBytes();
        var engine = new SaveEngine();
        if (!engine.Validate(original) || engine.AssessLayoutRisk(original) is not null)
            throw new InvalidDataException("Input is not an independently acceptable retail preview layout.");

        using var baseline = engine.OpenSession(original.ToArray(), null, SaveFormat.Standard);
        if (baseline is not SaveEngineSession retail || !retail.ValidateUnchangedRoundTrip())
            throw new InvalidDataException("Retail source does not round-trip byte-exactly without edits.");

        var before = baseline.ReadEntity(box, slot);
        if (before.IsEmpty)
            throw new InvalidOperationException("Cannot preview an edit to an empty slot.");
        if (string.Equals(before.Nickname, nickname, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested edit does not change the nickname.");
        var beforeEntity = baseline.ExportSlot(box, slot).Data.ToArray();

        using var scratch = engine.OpenSession(original.ToArray(), null, SaveFormat.Standard);
        scratch.ApplyEdit(box, slot, new EntityEdit(Nickname: nickname));
        var candidate = scratch.Serialize().ToArray();
        if (candidate.AsSpan().SequenceEqual(original))
            throw new InvalidOperationException("The editor produced no serialized change.");
        if (!engine.Validate(candidate) || engine.AssessLayoutRisk(candidate) is not null)
            throw new InvalidDataException("Edited bytes are not a supported retail preview layout.");

        // PKForge's structural slot-scope check is reused, but it is NOT a substitute
        // for the independently authenticated byte ranges needed by Save As.
        var scope = WriteScope.Only(new SlotRef(box, slot));
        if (engine.CheckWriteSafety(original, candidate, scope) is not null)
            throw new InvalidDataException("Preview changed data outside the selected Pokémon slot.");

        using var reopened = engine.OpenSession(candidate.ToArray(), null, SaveFormat.Standard);
        if (!reopened.Serialize().Span.SequenceEqual(candidate))
            throw new InvalidDataException("Candidate changed during an unchanged reopen/serialize cycle.");
        var after = reopened.ReadEntity(box, slot);
        if (after.IsEmpty || !string.Equals(after.Nickname, nickname, StringComparison.Ordinal))
            throw new InvalidDataException("Requested nickname did not survive serialization and reopen.");
        if (beforeEntity.AsSpan().SequenceEqual(reopened.ExportSlot(box, slot).Data))
            throw new InvalidDataException("Selected Pokémon bytes did not change.");

        // No bytes are written. The original remains the only authoritative input.
        return new RetailNicknamePreview(source.Fingerprint, SaveInputSnapshot.FromBytes(candidate));
    }
}

/// <summary>Non-publishing preview; never treat this as an export authorization.</summary>
public sealed class RetailNicknamePreview
{
    private readonly SaveInputSnapshot _candidate;

    internal RetailNicknamePreview(SaveFingerprint sourceFingerprint, SaveInputSnapshot candidate)
    {
        SourceFingerprint = sourceFingerprint;
        _candidate = candidate;
    }

    public SaveFingerprint SourceFingerprint { get; }
    public SaveFingerprint CandidateFingerprint => _candidate.Fingerprint;
    public byte[] CopyCandidateBytes() => _candidate.CopyBytes();
}
