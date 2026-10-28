namespace PKHeXMobile.Domain;

/// <summary>
/// Two independent, byte-exact reads of a previously staged artifact. This evidence
/// is neither a durability guarantee nor permission to publish or overwrite a save.
/// </summary>
public sealed record ArtifactReadbackEvidence(
    SaveFingerprint Fingerprint,
    DocumentSourceIdentity Identity,
    int VerifiedOpens);

/// <summary>
/// Verifies an already-created backup or candidate staging document through fresh,
/// identity-bearing provider opens. No creation, mutation, deletion or publication.
/// The provider's opaque identity is only a consistency hint, not a trusted revision.
/// </summary>
public static class ArtifactReadbackVerifier
{
    public static async Task<ArtifactReadbackEvidence> VerifyAsync(
        SaveInputSnapshot expected,
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openRead,
        IEnumerable<DocumentSourceIdentity> forbiddenIdentities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(openRead);
        ArgumentNullException.ThrowIfNull(forbiddenIdentities);

        var forbidden = new HashSet<DocumentSourceIdentity>(forbiddenIdentities);
        if (forbidden.Contains(null!))
            throw new ArgumentException("Forbidden identities must be non-null.", nameof(forbiddenIdentities));

        DocumentSourceIdentity? stableIdentity = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = await openRead(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Artifact provider returned no read handle.");
            await using var stream = handle.Stream
                ?? throw new InvalidDataException("Artifact provider returned no stream.");
            var identity = handle.Identity
                ?? throw new InvalidOperationException("Artifact provider identity is unavailable.");
            if (forbidden.Contains(identity))
                throw new InvalidOperationException("Artifact aliases a protected document.");
            if (stableIdentity is not null && stableIdentity != identity)
                throw new InvalidOperationException("Artifact identity changed between reads.");

            var snapshot = await SaveInputSnapshot.ReadAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            expected.RequireUnchanged(snapshot);
            stableIdentity = identity;
        }

        return new ArtifactReadbackEvidence(expected.Fingerprint, stableIdentity!, 2);
    }
}
