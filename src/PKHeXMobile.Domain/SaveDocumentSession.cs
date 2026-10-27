namespace PKHeXMobile.Domain;

/// <summary>
/// Read-only document session. A fresh provider open is required to detect stale content.
/// This does not implement or authorize in-place saves, backup, rollback, or atomic publish.
/// </summary>
public sealed class SaveDocumentSession
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _openRead;

    private SaveDocumentSession(
        Func<CancellationToken, ValueTask<Stream>> openRead, SaveInputSnapshot original)
    {
        _openRead = openRead;
        Original = original;
    }

    public SaveInputSnapshot Original { get; }

    public static async Task<SaveDocumentSession> OpenAsync(
        Func<CancellationToken, ValueTask<Stream>> openRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);
        await using var stream = await openRead(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The document provider returned no stream.");
        var snapshot = await SaveInputSnapshot.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        return new SaveDocumentSession(openRead, snapshot);
    }

    public async Task VerifySourceUnchangedAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await _openRead(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The document provider returned no stream.");
        var current = await SaveInputSnapshot.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        Original.RequireUnchanged(current);
    }
}
