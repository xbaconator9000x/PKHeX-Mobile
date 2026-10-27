namespace PKHeXMobile.Domain;

/// <summary>
/// Read-only document session. A fresh provider open is required to detect stale content.
/// A provider identity can additionally detect a same-byte document replacement, but
/// does not authenticate a game or authorize in-place writes.
/// </summary>
public sealed class SaveDocumentSession
{
    private readonly Func<CancellationToken, ValueTask<DocumentReadHandle>> _openRead;

    private SaveDocumentSession(
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openRead,
        SaveInputSnapshot original,
        DocumentSourceIdentity? identity)
    {
        _openRead = openRead;
        Original = original;
        SourceIdentity = identity;
    }

    public SaveInputSnapshot Original { get; }
    public DocumentSourceIdentity? SourceIdentity { get; }
    public bool SourceIdentityConfirmed { get; private set; }

    /// <summary>
    /// Legacy stream-only boundary. Byte equality can be checked, but document
    /// identity remains unverified. Never promote this result to write permission.
    /// </summary>
    public static Task<SaveDocumentSession> OpenAsync(
        Func<CancellationToken, ValueTask<Stream>> openRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);
        return OpenWithIdentityAsync(async token =>
            new DocumentReadHandle(await openRead(token).ConfigureAwait(false), null), cancellationToken);
    }

    /// <summary>Open through a provider capable of reporting an opaque identity.</summary>
    public static async Task<SaveDocumentSession> OpenWithIdentityAsync(
        Func<CancellationToken, ValueTask<DocumentReadHandle>> openRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);
        cancellationToken.ThrowIfCancellationRequested();
        var handle = await openRead(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The document provider returned no read handle.");
        await using var stream = handle.Stream
            ?? throw new InvalidDataException("The document provider returned no stream.");
        var snapshot = await SaveInputSnapshot.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        return new SaveDocumentSession(openRead, snapshot, handle.Identity);
    }

    public async Task VerifySourceUnchangedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var handle = await _openRead(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The document provider returned no read handle.");
        await using var stream = handle.Stream
            ?? throw new InvalidDataException("The document provider returned no stream.");
        // A changed identity is not made safe by identical bytes. Also reject
        // identities disappearing or appearing between opens (ambiguous source).
        if (SourceIdentity != handle.Identity)
            throw new InvalidOperationException("The document provider changed its source identity.");
        var current = await SaveInputSnapshot.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        Original.RequireUnchanged(current);
        SourceIdentityConfirmed = SourceIdentity is not null;
    }
}
