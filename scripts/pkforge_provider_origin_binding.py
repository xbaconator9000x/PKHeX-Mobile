#!/usr/bin/env python3
"""Bind pinned PKForge sessions to immutable provider-origin bytes, not Serialize().

Temporary checkout only. Requires the previously installed bounded two-open
read adapter and read-only write barriers. No provider identity or write grant.
"""
import argparse
import json
from pathlib import Path
from xml.sax.saxutils import escape

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

SERVICE = "src/PKForge.Infrastructure/SaveSessionService.cs"
PROJECT = "src/PKForge.Infrastructure/PKForge.Infrastructure.csproj"


def once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one exact anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_service(source: str) -> str:
    if "IProviderOriginSession" in source or "ProviderOriginBinding" in source:
        raise ProbeRefusal("Provider-origin adapter already installed")
    source = once(source, "using PKForge.Domain;\n",
        "using PKForge.Domain;\nusing PKHeXMobile.Domain;\n"
        "using PKHeXMobile.PortConsolidation;\n", "imports")
    source = once(source, ": ISaveSessionService\n{",
        ": ISaveSessionService, IProviderOriginSession\n{", "interface")
    source = once(source, "    public SaveSession? Current { get; private set; }",
        """    private long _openingGeneration;
    public ProviderOriginBinding? ProviderOrigin { get; private set; }
    public SaveSession? Current { get; private set; }""", "binding state")
    source = once(source,
        """        var bytes = await access.ReadAsync(document.DocumentId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var format = SaveIdentityRules.FormatOfChoice(identities?.Get(document.DocumentId)?.GameChoiceId);
        var engineSession = engine.OpenSession(bytes, document.DisplayName, format);
        engineSession.MakeActive();""",
        """        // Invalidate an earlier preview as soon as another open starts.
        var request = Interlocked.Increment(ref _openingGeneration);
        ProviderOrigin = null;
        var bytes = await access.ReadAsync(document.DocumentId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // This copy is from the provider, before the engine can normalize anything.
        var providerOriginal = SaveInputSnapshot.FromBytes(bytes.Span);
        var format = SaveIdentityRules.FormatOfChoice(identities?.Get(document.DocumentId)?.GameChoiceId);
        var engineSession = engine.OpenSession(providerOriginal.CopyBytes(), document.DisplayName, format);
        if (request != Interlocked.Read(ref _openingGeneration) || cancellationToken.IsCancellationRequested)
        {
            engineSession.Dispose();
            throw new OperationCanceledException("A newer document selection replaced this open.");
        }
        engineSession.MakeActive();""", "open baseline")
    source = once(source,
        """        Current = session;
        return session;""",
        """        Current = session;
        ProviderOrigin = new ProviderOriginBinding(document.DocumentId, engineSession, providerOriginal, request);
        return session;""", "commit binding")
    source = once(source,
        """        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (Current is not { } current || current.Document.DocumentId != documentId) return;""",
        """        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ProviderOrigin = null; // No write can preserve the old provider baseline.
        if (Current is not { } current || current.Document.DocumentId != documentId) return;""", "write invalidation")
    source = once(source,
        """    public void RevertToBaseline()
    {
        if (Current is not { } current) return;""",
        """    public void RevertToBaseline()
    {
        ProviderOrigin = null; // A reopened engine is not the original provider session.
        if (Current is not { } current) return;""", "revert invalidation")
    source = once(source,
        """    public void Close()
    {
        CurrentSession?.Dispose();""",
        """    public void Close()
    {
        Interlocked.Increment(ref _openingGeneration);
        ProviderOrigin = null;
        CurrentSession?.Dispose();""", "close invalidation")
    return source


def adapt_project(source: str, mobile: Path) -> str:
    if "PKHeXMobile.PortConsolidation.csproj" in source:
        raise ProbeRefusal("Infrastructure already references adapter")
    project = mobile / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj"
    if not project.is_file() or project.is_symlink():
        raise ProbeRefusal("Mobile adapter project missing or symlinked")
    anchor = '    <ProjectReference Include="..\\PKForge.Domain\\PKForge.Domain.csproj" />'
    reference = f'    <ProjectReference Include="{escape(str(project))}" />'
    return once(source, anchor, anchor + "\n" + reference, "infrastructure project")


def prepare(root: Path, mobile: Path) -> dict:
    root = root.resolve(strict=True)
    mobile = mobile.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    paths = [root / SERVICE, root / PROJECT]
    if any(not p.is_file() or p.is_symlink() or p.parent.is_symlink() for p in paths):
        raise ProbeRefusal("Missing or symlinked pinned infrastructure sources")
    # Preflight all transformations before any write.
    updated = [adapt_service(paths[0].read_text(encoding="utf-8")),
               adapt_project(paths[1].read_text(encoding="utf-8"), mobile)]
    for path, text in zip(paths, updated):
        path.write_text(text, encoding="utf-8")
    return {"mode": "provider-origin preview baseline, read-only",
            "changed_files": [SERVICE, PROJECT],
            "writes_enabled": False, "identity_authenticated": False,
            "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--mobile", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.repo, args.mobile), indent=2))
