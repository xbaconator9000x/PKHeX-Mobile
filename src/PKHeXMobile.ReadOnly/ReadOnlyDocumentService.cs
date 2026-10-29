using PKHeXMobile.Domain;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// A bounded, read-only result from a document provider. No mutable SaveFile
/// or underlying save bytes are returned. A verified provider identity means
/// only that the two opens reported the same opaque identity; it is NOT
/// permission to overwrite, nor independent proof of a supported save format.
/// </summary>
public sealed record ReadOnlyDocumentInspection(
    SaveFingerprint Fingerprint,
    SaveReadOnlyResult? Result,
    string? RejectionReason)
{
    public bool IsRecognized => Result is not null;
    public DocumentSourceIdentity? SourceIdentity { get; init; }
    public bool SourceIdentityConfirmed { get; init; }
}

/// <summary>
/// Platform-neutral boundary for Android SAF and desktop diagnostics.
/// A provider supplies a NEW read stream each time; no file path or seek support
/// is assumed. Both opens must see identical bytes before publishing a result.
/// This does not enable any save-writing route.
/// </summary>
public static class ReadOnlyDocumentService
{
    public static async Task<ReadOnlyDocumentInspection> InspectAsync(
        Func<CancellationToken, ValueTask<Stream>> openRead,
        CancellationToken cancellationToken = default)
    {
        var session = await SaveDocumentSession.OpenAsync(openRead, cancellationToken).ConfigureAwait(false);
        return await InspectSessionAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Identity-aware path for providers such as Android SAF. The provider
    /// supplies the same authority/document ID on both independent opens.
    /// A changed identity is rejected even if the bytes happen to be identical.
    /// </summary>
    public static async Task<ReadOnlyDocumentInspection> InspectWithIdentityAsync(
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openRead,
        CancellationToken cancellationToken = default)
    {
        var session = await SaveDocumentSession.OpenWithIdentityAsync(openRead, cancellationToken)
            .ConfigureAwait(false);
        return await InspectSessionAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ReadOnlyDocumentInspection> InspectSessionAsync(
        SaveDocumentSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool recognized = ReadOnlySaveInspector.TryInspect(session.Original, out var result, out var reason);

        // Reopen even after parser refusal: do not publish stale success OR refusal.
        await session.VerifySourceUnchangedAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new ReadOnlyDocumentInspection(
            session.Original.Fingerprint,
            recognized ? result : null,
            recognized ? null : reason)
        {
            SourceIdentity = session.SourceIdentity,
            SourceIdentityConfirmed = session.SourceIdentityConfirmed,
        };
    }
}
