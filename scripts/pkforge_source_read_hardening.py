#!/usr/bin/env python3
"""Bound and independently verify PKForge source reads in the temporary MAUI checkout.

Run AFTER the official-Core, retail-only, read-only containment and picker
hardening adaptations. Reuses PKHeXMobile.Domain.SaveInputSnapshot, not a new
parser or writer. This is NOT a provider-generation or retail-layout oracle.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

SAF = "src/PKForge.App/Platforms/Android/AndroidSafFileAccess.cs"
WRAPPER = "src/PKForge.App/Services/ReadOnlySaveFileAccess.cs"

OLD_SAF_READ = """    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var uri = Parse(documentId);
        await using var input = Resolver.OpenInputStream(uri)
            ?? throw new IOException($"The document provider could not open {uri} for reading.");
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
"""
NEW_SAF_READ = """    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = Parse(documentId);
        // OpenInputStream is synchronous; providers may ignore cancellation during open.
        // Subsequent reads are bounded to 32 MiB and dispose the provider stream.
        await using var input = Resolver.OpenInputStream(uri)
            ?? throw new IOException($"The document provider could not open {uri} for reading.");
        var snapshot = await SaveInputSnapshot.ReadAsync(input, cancellationToken).ConfigureAwait(false);
        return snapshot.CopyBytes();
    }
"""
OLD_WRAPPER_READ = """    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
        => reader.ReadAsync(documentId, cancellationToken);
"""
NEW_WRAPPER_READ = """    // Reopen independently before promoting bytes to PKForge's mutable session.
    // Matching bytes do not authenticate a retail layout or authorize writing.
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();
        var first = await reader.ReadAsync(documentId, cancellationToken).ConfigureAwait(false);
        var snapshot = SaveInputSnapshot.FromBytes(first.Span);
        cancellationToken.ThrowIfCancellationRequested();
        var second = await reader.ReadAsync(documentId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!snapshot.Matches(second.Span))
            throw new InvalidDataException("Document bytes changed between independent provider reads.");
        return snapshot.CopyBytes();
    }
"""


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one pinned anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_saf(source: str) -> str:
    if "UnsafeSaveWriteException(ReadOnlySaveWriter.Reason)" not in source:
        raise ProbeRefusal("Direct Android SAF write denial is missing")
    if 'OpenFileDescriptor(uri, "rwt")' in source or "GrantWriteUriPermission" in source:
        raise ProbeRefusal("Writable Android source route detected")
    source = replace_once(source, "using PKForge.Domain;\n",
                          "using PKForge.Domain;\nusing PKHeXMobile.Domain;\n",
                          "SAF Domain dependency")
    source = replace_once(source, OLD_SAF_READ, NEW_SAF_READ, "bounded SAF read")
    if "CopyToAsync(buffer" in source:
        raise ProbeRefusal("Unbounded source read remains")
    return source


def adapt_wrapper(source: str) -> str:
    if "UnsafeSaveWriteException(ReadOnlySaveWriter.Reason)" not in source:
        raise ProbeRefusal("Provider write barrier is missing")
    source = replace_once(source, "using PKForge.Domain;\n",
                          "using PKForge.Domain;\nusing PKHeXMobile.Domain;\n",
                          "wrapper Domain dependency")
    return replace_once(source, OLD_WRAPPER_READ, NEW_WRAPPER_READ,
                        "independent source read")


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    paths = [root / SAF, root / WRAPPER]
    if any(not p.is_file() or p.is_symlink() or p.parent.is_symlink() for p in paths):
        raise ProbeRefusal("Missing or symlinked pinned read source")
    originals = [p.read_text(encoding="utf-8") for p in paths]
    updated = [adapt_saf(originals[0]), adapt_wrapper(originals[1])]
    try:
        for path, content in zip(paths, updated):
            path.write_text(content, encoding="utf-8")
    except OSError:
        for path, content in zip(paths, originals):
            path.write_text(content, encoding="utf-8")
        raise
    return {"mode": "temporary bounded two-open read-only PKForge integration",
            "changed_files": [SAF, WRAPPER],
            "max_input_bytes": 32 * 1024 * 1024,
            "independent_source_reads": 2, "writes_enabled": False,
            "source_identity_authenticated": False, "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
