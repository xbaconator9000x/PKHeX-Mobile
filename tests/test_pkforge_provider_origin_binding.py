"""Fail-closed pinned PKForge provider-origin session transformation tests."""
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.pkforge_official_core_probe import ProbeRefusal
from scripts.pkforge_provider_origin_binding import adapt_service, adapt_project


PINNED_SERVICE = """using PKForge.Domain;

namespace PKForge.Infrastructure;
public sealed class SaveSessionService(ISaveFileAccess access, ISaveEngine engine, ISaveIdentityStore? identities = null) : ISaveSessionService
{
    public SaveSession? Current { get; private set; }
    public ISaveEngineSession? CurrentSession { get; private set; }
    public async ValueTask<SaveSession> OpenAsync(PickedDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bytes = await access.ReadAsync(document.DocumentId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var format = SaveIdentityRules.FormatOfChoice(identities?.Get(document.DocumentId)?.GameChoiceId);
        var engineSession = engine.OpenSession(bytes, document.DisplayName, format);
        engineSession.MakeActive();
        CurrentSession?.Dispose();
        var session = new SaveSession(document, engineSession.Snapshot);
        CurrentSession = engineSession;
        Current = session;
        return session;
    }
    public void MarkWritten(string documentId, ReadOnlyMemory<byte> written)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (Current is not { } current || current.Document.DocumentId != documentId) return;
    }
    public void RevertToBaseline()
    {
        if (Current is not { } current) return;
    }
    public void Close()
    {
        CurrentSession?.Dispose();
    }
}
"""


class ProviderOriginTransformTests(unittest.TestCase):
    def test_pinned_service_binds_before_engine_and_revokes_on_lifecycle(self):
        result = adapt_service(PINNED_SERVICE)
        self.assertIn("IProviderOriginSession", result)
        self.assertIn("SaveInputSnapshot.FromBytes(bytes.Span)", result)
        self.assertIn("engine.OpenSession(providerOriginal.CopyBytes()", result)
        self.assertIn("ProviderOrigin = new ProviderOriginBinding", result)
        self.assertLess(result.index("var providerOriginal"), result.index("engine.OpenSession("))
        self.assertIn("Interlocked.Read(ref _openingGeneration)", result)
        self.assertGreaterEqual(result.count("ProviderOrigin = null"), 4)

    def test_unexpected_upstream_anchor_fails_closed(self):
        with self.assertRaises(ProbeRefusal):
            adapt_service(PINNED_SERVICE.replace("engineSession.MakeActive();", "engineSession.Activate();"))

    def test_duplicate_or_repeated_adaptation_fails(self):
        with self.assertRaises(ProbeRefusal):
            adapt_service(PINNED_SERVICE + PINNED_SERVICE)
        with self.assertRaises(ProbeRefusal):
            adapt_service(adapt_service(PINNED_SERVICE))

    def test_project_reference_and_reapply_refusal(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj"
            project.parent.mkdir(parents=True)
            project.write_text("<Project />", encoding="utf-8")
            source = '<Project>\n    <ProjectReference Include="..\\PKForge.Domain\\PKForge.Domain.csproj" />\n</Project>'
            result = adapt_project(source, root)
            self.assertIn("PKHeXMobile.PortConsolidation.csproj", result)
            with self.assertRaises(ProbeRefusal):
                adapt_project(result, root)


if __name__ == "__main__":
    unittest.main()
