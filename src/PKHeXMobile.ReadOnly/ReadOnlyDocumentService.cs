using PKHeXMobile.Domain;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// A bounded, read-only result from a document provider. The returned projection
/// never contains a mutable PKHeX.Core SaveFile or the underlying input bytes.
/// </summary>
public sealed record ReadOnlyDocumentInspection(
    SaveFingerprint Fingerprint,
    SaveReadOnlyResult? Result,
    string? RejectionReason)
{
    public bool IsRecognized => Result is not null;
}

/// <summary>
/// Platform-neutral boundary for Android SAF and desktop diagnostics.
/// A provider supplies a NEW read stream each time; no file path or seek support
/// is assumed. Both opens must see identical bytes before publishing a result.
/// This does not authenticate provider identity or enable any save-writing route.
/// </summary>
public static class ReadOnlyDocumentService
{
    public static async Task<ReadOnlyDocumentInspection> InspectAsync(
        Func<CancellationToken, ValueTask<Stream>> openRead,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await SaveDocumentSession.OpenAsync(openRead, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        bool recognized = ReadOnlySaveInspector.TryInspect(session.Original, out var result, out var reason);

        // Reopen even after a parser refusal: the provider may have changed or revoked
        // access while parsing. Do not publish a stale projection or stale rejection.
        await session.VerifySourceUnchangedAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new ReadOnlyDocumentInspection(
            session.Original.Fingerprint,
            recognized ? result : null,
            recognized ? null : reason);
    }
}
