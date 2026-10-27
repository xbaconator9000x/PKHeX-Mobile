namespace PKHeXMobile.Domain;

/// <summary>
/// Provider-supplied identity for a read-only document open. These are opaque,
/// stable-for-the-provider identifiers (for Android SAF, authority and document ID),
/// not filesystem paths, content hashes, or proof of permission to overwrite.
/// </summary>
public sealed record DocumentSourceIdentity
{
    public string ProviderKey { get; }
    public string DocumentKey { get; }

    public DocumentSourceIdentity(string providerKey, string documentKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("A provider key is required.", nameof(providerKey));
        if (string.IsNullOrWhiteSpace(documentKey))
            throw new ArgumentException("A document key is required.", nameof(documentKey));
        ProviderKey = providerKey;
        DocumentKey = documentKey;
    }
}

/// <summary>
/// One newly opened read stream and its provider-reported identity, if available.
/// The caller transfers ownership of the stream to the read-only service.
/// A null identity means identity is unverified, even if bytes match on re-open.
/// </summary>
public sealed record DocumentReadHandle(Stream Stream, DocumentSourceIdentity? Identity);
