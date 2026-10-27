namespace PKHeXMobile.Domain;

/// <summary>Compatibility decision made by a future verified format reader.</summary>
public enum SaveCompatibility
{
    Unknown,
    SupportedReadOnly,
    SupportedWrite
}

/// <summary>
/// A description of a validated compatibility decision, not evidence by itself.
/// Callers must actually verify exact file identity and save format/layout.
/// </summary>
public sealed record SaveInspection(
    string Format,
    SaveCompatibility Compatibility,
    string? VerifiedProfileId);

/// <summary>
/// An initial fail-closed authorization contract. It is NOT a save parser,
/// security boundary, checksum validator, backup system, or safe file writer.
/// </summary>
public static class SaveWriteGuard
{
    public static void RequireAuthorized(SaveInspection inspection, int originalLength, int outputLength)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        if (inspection.Compatibility != SaveCompatibility.SupportedWrite)
            throw new InvalidOperationException("Save format/profile is not approved for writing.");
        if (string.IsNullOrWhiteSpace(inspection.VerifiedProfileId))
            throw new InvalidOperationException("A verified save profile is required for writing.");
        if (originalLength <= 0 || outputLength <= 0)
            throw new InvalidOperationException("Save files must not be empty.");
        if (originalLength != outputLength)
            throw new InvalidOperationException("Unapproved save size change was blocked.");
    }
}
