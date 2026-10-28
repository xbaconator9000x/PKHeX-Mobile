#!/usr/bin/env python3
"""Serialize pinned PKForge session activation with close/revert/write invalidation.

Applies only after pkforge_provider_origin_binding. All save writers remain
denied. This adapter does not certify provider identity or Android execution.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha
from scripts.pkforge_provider_origin_binding import SERVICE, once


def wrap_sync_method(source: str, signature: str) -> str:
    """Lock one exact synchronous method body, refusing missing/ambiguous anchors."""
    if source.count(signature) != 1:
        raise ProbeRefusal(f"Expected one method: {signature}")
    start = source.index(signature) + len(signature)
    opening = source.find("{", start)
    if opening < 0 or source[start:opening].strip():
        raise ProbeRefusal(f"Unexpected method header: {signature}")
    depth = 0
    closing = -1
    for pos in range(opening, len(source)):
        if source[pos] == "{":
            depth += 1
        elif source[pos] == "}":
            depth -= 1
            if depth == 0:
                closing = pos
                break
    if closing < 0:
        raise ProbeRefusal(f"Unbalanced method: {signature}")
    body = source[opening + 1:closing].strip("\n")
    indented = "\n".join("    " + line if line.strip() else line for line in body.splitlines())
    return (source[:opening + 1] + "\n        lock (_activationGate)\n        {\n"
            + indented + "\n        }\n    " + source[closing:])


def adapt_transaction(source: str) -> str:
    if "_activationGate" in source or "_activationTestHook" in source:
        raise ProbeRefusal("Activation transaction already installed")
    if "IProviderOriginSession" not in source:
        raise ProbeRefusal("Provider-origin binding must be installed first")
    source = once(source, "    private long _openingGeneration;",
        """    // All activation, close, revert and write-notification commits share this gate.
    private readonly object _activationGate = new();
    // Private deterministic interleaving hook, exercised only by synthetic smoke tests.
    private Action? _activationTestHook;
    private long _openingGeneration;""", "transaction state")
    source = once(source,
        """        var request = Interlocked.Increment(ref _openingGeneration);
        ProviderOrigin = null;""",
        """        long request;
        lock (_activationGate)
        {
            request = Interlocked.Increment(ref _openingGeneration);
            ProviderOrigin = null;
        }""", "open request epoch")
    source = once(source,
        """        if (request != Interlocked.Read(ref _openingGeneration) || cancellationToken.IsCancellationRequested)
        {
            engineSession.Dispose();
            throw new OperationCanceledException("A newer document selection replaced this open.");
        }
        engineSession.MakeActive();
        CurrentSession?.Dispose();
        var session = new SaveSession(document, engineSession.Snapshot);
        CurrentSession = engineSession;
        Current = session;
        ProviderOrigin = new ProviderOriginBinding(document.DocumentId, engineSession, providerOriginal, request);
        return session;""",
        """        // No await is permitted between epoch validation and publishing the session.
        lock (_activationGate)
        {
            if (request != _openingGeneration || cancellationToken.IsCancellationRequested)
            {
                try { engineSession.Dispose(); } catch { /* preserve stale-open refusal */ }
                throw new OperationCanceledException("A newer lifecycle operation replaced this open.");
            }
            var previous = CurrentSession;
            try
            {
                _activationTestHook?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                if (request != _openingGeneration)
                    throw new OperationCanceledException("Open invalidated before activation.");
                var session = new SaveSession(document, engineSession.Snapshot);
                var binding = new ProviderOriginBinding(document.DocumentId, engineSession, providerOriginal, request);
                engineSession.MakeActive();
                CurrentSession = engineSession;
                Current = session;
                ProviderOrigin = binding;
                previous?.Dispose();
                return session;
            }
            catch
            {
                // Activation failure cannot leave an apparently trusted session.
                ProviderOrigin = null;
                Current = null;
                CurrentSession = null;
                try { engineSession.Dispose(); } catch { /* fail closed */ }
                if (!ReferenceEquals(previous, engineSession))
                    try { previous?.Dispose(); } catch { /* fail closed */ }
                Interlocked.Increment(ref _openingGeneration);
                throw;
            }
        }""", "atomic activation")
    source = once(source,
        """        ProviderOrigin = null; // No write can preserve the old provider baseline.""",
        """        Interlocked.Increment(ref _openingGeneration);
        ProviderOrigin = null; // No write can preserve the old provider baseline.""",
        "write epoch")
    source = once(source,
        """        ProviderOrigin = null; // A reopened engine is not the original provider session.""",
        """        Interlocked.Increment(ref _openingGeneration);
        ProviderOrigin = null; // A reopened engine is not the original provider session.""",
        "revert epoch")
    for signature in ("    public void MarkWritten(string documentId, ReadOnlyMemory<byte> written)",
                      "    public void RevertToBaseline()",
                      "    public void Close()"):
        source = wrap_sync_method(source, signature)
    return source


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    path = root / SERVICE
    if not path.is_file() or path.is_symlink() or path.parent.is_symlink():
        raise ProbeRefusal("Missing or symlinked pinned service")
    # Preflight full transformation before any write.
    transformed = adapt_transaction(path.read_text(encoding="utf-8"))
    path.write_text(transformed, encoding="utf-8")
    return {"mode": "read-only session activation transaction",
            "changed_files": [SERVICE], "writes_enabled": False,
            "device_tested": False, "identity_authenticated": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.repo), indent=2))
