using System.Security.Cryptography;

namespace PKHeXMobile.Domain;

/// <summary>
/// An immutable, bounded copy of bytes supplied by an untrusted document provider.
/// Fingerprints detect changed content; they never authenticate a game or authorize writes.
/// </summary>
public sealed class SaveInputSnapshot
{
    public const int MaxInputBytes = 32 * 1024 * 1024;
    private const int ReadBufferBytes = 64 * 1024;
    private readonly byte[] _bytes;
    private readonly byte[] _digest;

    private SaveInputSnapshot(byte[] bytes)
    {
        _bytes = bytes;
        _digest = SHA256.HashData(bytes);
        Fingerprint = new SaveFingerprint(bytes.Length, Convert.ToHexString(_digest));
    }

    public SaveFingerprint Fingerprint { get; }
    public int Length => _bytes.Length;

    /// <summary>Never expose the internal mutable array to parsers or callers.</summary>
    public byte[] CopyBytes() => (byte[])_bytes.Clone();

    public static SaveInputSnapshot FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > MaxInputBytes)
            throw new InvalidDataException("Save input must be nonempty and within the configured size limit.");
        return new SaveInputSnapshot(data.ToArray());
    }

    /// <summary>Read a potentially non-seekable stream, without trusting Length or Position.</summary>
    public static async Task<SaveInputSnapshot> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new InvalidDataException("The document provider did not supply a readable stream.");

        using var staging = new MemoryStream();
        var chunk = new byte[ReadBufferBytes];
        while (true)
        {
            int count = await source.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;
            if (staging.Length + count > MaxInputBytes)
                throw new InvalidDataException("The document exceeds the configured save input limit.");
            await staging.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        return FromBytes(staging.GetBuffer().AsSpan(0, checked((int)staging.Length)));
    }

    public bool Matches(ReadOnlySpan<byte> candidate)
    {
        if (candidate.Length != _bytes.Length)
            return false;
        var digest = SHA256.HashData(candidate);
        return CryptographicOperations.FixedTimeEquals(_digest, digest);
    }

    public void RequireUnchanged(SaveInputSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Length != Length || !CryptographicOperations.FixedTimeEquals(_digest, current._digest))
            throw new InvalidOperationException("The source document has changed since it was opened.");
    }
}

/// <summary>Content fingerprint, not a trusted file identity or compatibility profile.</summary>
public sealed record SaveFingerprint(int Length, string Sha256Hex);
