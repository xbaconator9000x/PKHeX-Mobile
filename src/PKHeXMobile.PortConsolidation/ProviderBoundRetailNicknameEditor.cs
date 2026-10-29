// GPL-3.0-only. Non-publishing provider-origin preview adapter for pinned PKForge.
// No Android write permissions, source identity certification, or Save As authority.
using PKForge.Domain;
using PKHeXMobile.Domain;

namespace PKHeXMobile.PortConsolidation;

/// <summary>
/// Implemented by the adapted PKForge SaveSessionService. The snapshot is taken
/// from the bounded provider read BEFORE the mutable PKForge engine is opened.
/// A document ID is an opaque routing hint, not authenticated provider identity.
/// </summary>
public interface IProviderOriginSession
{
    ProviderOriginBinding? ProviderOrigin { get; }
}

/// <summary>A session-specific immutable baseline. Never holds a mutable engine buffer.</summary>
public sealed class ProviderOriginBinding
{
    public ProviderOriginBinding(
        string documentId, ISaveEngineSession engineSession,
        SaveInputSnapshot original, long generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(engineSession);
        ArgumentNullException.ThrowIfNull(original);
        DocumentId = documentId;
        EngineSession = engineSession;
        Original = SaveInputSnapshot.FromBytes(original.CopyBytes());
        Generation = generation;
    }

    public string DocumentId { get; }
    public ISaveEngineSession EngineSession { get; }
    public SaveInputSnapshot Original { get; }
    public long Generation { get; }
}

/// <summary>
/// Reuses the existing detached PKForge editor against the immutable provider
/// baseline, never against CurrentSession.Serialize(). Rechecks the source both
/// before and after the CPU-bound preview and refuses a changed session.
/// Requires the hardened, bounded, two-open ISaveFileAccess registration.
/// </summary>
public static class ProviderBoundRetailNicknameEditor
{
    public static async Task<RetailNicknamePreview> PreviewAsync(
        ISaveSessionService sessions, ISaveFileAccess access,
        int box, int slot, string nickname, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(nickname);
        cancellationToken.ThrowIfCancellationRequested();
        if (sessions is not IProviderOriginSession { ProviderOrigin: { } binding })
            throw new InvalidOperationException("No provider-origin save baseline is bound.");

        void RequireCurrent()
        {
            var current = sessions.Current;
            if (current is null || !ReferenceEquals(sessions.CurrentSession, binding.EngineSession)
                || !string.Equals(current.Document.DocumentId, binding.DocumentId, StringComparison.Ordinal)
                || sessions is not IProviderOriginSession { ProviderOrigin: { } now }
                || !ReferenceEquals(now, binding) || now.Generation != binding.Generation)
                throw new InvalidOperationException("The source session changed during preview.");
        }

        async Task VerifyProviderAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireCurrent();
            var bytes = await access.ReadAsync(binding.DocumentId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            // Copy before hashing: the provider may reuse its backing array.
            var current = SaveInputSnapshot.FromBytes(bytes.Span);
            binding.Original.RequireUnchanged(current);
            RequireCurrent();
        }

        RequireCurrent();
        await VerifyProviderAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrent();
        var preview = DetachedRetailNicknameEditor.Preview(binding.Original, box, slot, nickname);
        RequireCurrent();
        await VerifyProviderAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrent();
        preview.RequireMatchingSource(binding.Original);
        return preview;
    }
}
