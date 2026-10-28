namespace PKHeXMobile.Domain;

/// <summary>
/// Non-publishing evidence for a staged candidate, its separate original-byte backup,
/// and source consistency. It is NOT a write authorization, desktop parity verdict,
/// format authentication, durability guarantee or rollback certification.
/// </summary>
public sealed record StagedCandidatePreflightEvidence(
    SaveCandidateScopeAudit ChangeAudit,
    ArtifactReadbackEvidence Backup,
    ArtifactReadbackEvidence StagedCandidate,
    DocumentSourceIdentity SourceIdentity);

/// <summary>
/// Read-only rehearsal for a future Save As transaction. Callers must create the
/// candidate and backup elsewhere, and supply independently reopened READ handles.
/// No method here accepts a writable stream or invokes a save serializer/publisher.
/// </summary>
public static class StagedCandidatePreflight
{
    public static async Task<StagedCandidatePreflightEvidence> VerifyAsync(
        SaveDocumentSession source,
        ReadOnlyMemory<byte> candidate,
        IEnumerable<SaveByteRange> approvedRanges,
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openBackupRead,
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openStagedRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(approvedRanges);
        ArgumentNullException.ThrowIfNull(openBackupRead);
        ArgumentNullException.ThrowIfNull(openStagedRead);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceIdentity = source.SourceIdentity
            ?? throw new InvalidOperationException("An identity-less source cannot enter staged preflight.");
        await source.VerifySourceUnchangedAsync(cancellationToken).ConfigureAwait(false);
        if (!source.SourceIdentityConfirmed)
            throw new InvalidOperationException("Source identity could not be confirmed.");

        // Byte scope is a diagnostic, NOT a substitute for an independently verified
        // game/layout and semantic PKHeX desktop oracle.
        var audit = SaveCandidateScopeAudit.Analyze(source.Original, candidate.Span, approvedRanges);
        var stagedSnapshot = SaveInputSnapshot.FromBytes(candidate.Span);

        var backup = await ArtifactReadbackVerifier.VerifyAsync(
            source.Original, openBackupRead, [sourceIdentity], cancellationToken)
            .ConfigureAwait(false);
        var staged = await ArtifactReadbackVerifier.VerifyAsync(
            stagedSnapshot, openStagedRead, [sourceIdentity, backup.Identity], cancellationToken)
            .ConfigureAwait(false);

        // A source replacement while checking backup/staging must invalidate the
        // result even if the candidate and backup themselves are byte-exact.
        await source.VerifySourceUnchangedAsync(cancellationToken).ConfigureAwait(false);
        if (!source.SourceIdentityConfirmed)
            throw new InvalidOperationException("Source identity was lost before preflight completed.");

        return new StagedCandidatePreflightEvidence(audit, backup, staged, sourceIdentity);
    }
}
