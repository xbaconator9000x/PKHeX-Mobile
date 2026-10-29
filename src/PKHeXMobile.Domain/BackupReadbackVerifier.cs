namespace PKHeXMobile.Domain;

/// <summary>
/// Evidence of two successful byte-exact backup reads. It does NOT prove durable
/// storage, restore ability after process death, or safe in-place overwrite.
/// </summary>
public sealed record BackupReadbackEvidence(SaveFingerprint OriginalFingerprint, int VerifiedOpens);

/// <summary>
/// Read-only backup integrity primitive inspired by upstream backup risk analysis.
/// Never creates, overwrites, deletes or publishes a file. The caller must use
/// independent provider opens and separately prove durability/recovery on device.
/// </summary>
public static class BackupReadbackVerifier
{
    public static async Task<BackupReadbackEvidence> VerifyAsync(
        SaveInputSnapshot expectedOriginal,
        Func<CancellationToken, ValueTask<Stream>> openBackupRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedOriginal);
        ArgumentNullException.ThrowIfNull(openBackupRead);

        for (int i = 0; i < 2; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = await openBackupRead(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The backup provider returned no stream.");
            await using (stream.ConfigureAwait(false))
            {
                var readback = await SaveInputSnapshot.ReadAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                expectedOriginal.RequireUnchanged(readback);
            }
        }

        return new BackupReadbackEvidence(expectedOriginal.Fingerprint, 2);
    }
}
