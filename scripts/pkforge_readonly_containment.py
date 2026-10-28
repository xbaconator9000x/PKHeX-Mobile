#!/usr/bin/env python3
"""Install fail-closed read-only services into an exact pinned PKForge MAUI checkout.

The temporary app retains PKForge's reader but denies both legacy write paths.
This is not Save As, device certification, or a released application.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

PROGRAM = "src/PKForge.App/MauiProgram.cs"
WRITER = "src/PKForge.App/Services/ReadOnlySaveWriter.cs"
ACCESS = "src/PKForge.App/Services/ReadOnlySaveFileAccess.cs"

WRITER_SOURCE = """// PKHeX Mobile adaptation of pinned PKForge 2df04430; GPL-3.0-only.
using PKForge.Domain;

namespace PKForge.App;

/// <summary>Denies all original-document writes, including unrestricted restores.</summary>
public sealed class ReadOnlySaveWriter : ISafeSaveWriter
{
    public const string Reason = "Read-only integration build: save writing is disabled. No document was changed.";

    public ValueTask<SaveWriteReceipt> WriteAsync(
        string documentId, SaveSnapshot original, ReadOnlyMemory<byte> candidate,
        string? changeDescription = null, CancellationToken cancellationToken = default)
        => ValueTask.FromException<SaveWriteReceipt>(new UnsafeSaveWriteException(Reason));

    public ValueTask<SaveWriteReceipt> WriteScopedAsync(
        string documentId, SaveSnapshot original, ReadOnlyMemory<byte> candidate,
        WriteScope scope, string? changeDescription = null,
        CancellationToken cancellationToken = default)
        => ValueTask.FromException<SaveWriteReceipt>(new UnsafeSaveWriteException(Reason));

    public string? WhyWritesAreRefused(string documentId, SaveSnapshot original) => Reason;

    // Explicit risk acceptance cannot enable an unsupported write.
    public void ConfirmLayoutRisk(string documentId) { }
    public void RevokeLayoutRisk(string documentId) { }
}
"""

ACCESS_SOURCE = """// PKHeX Mobile adaptation of pinned PKForge 2df04430; GPL-3.0-only.
using PKForge.Domain;

namespace PKForge.App;

/// <summary>Independent file-access write barrier for callers bypassing the writer.</summary>
public sealed class ReadOnlySaveFileAccess(ISaveFileAccess reader) : ISaveFileAccess
{
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
        => reader.ReadAsync(documentId, cancellationToken);

    public ValueTask WriteAtomicallyAsync(
        string documentId, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
        => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));
}
"""


def adapt_program(source: str) -> str:
    if "ReadOnlySaveWriter" in source or "ReadOnlySaveFileAccess" in source:
        raise ProbeRefusal("Containment already installed")
    changes = (
        ("        builder.Services.AddSingleton<ISafeSaveWriter, SafeSaveWriter>();",
         "        builder.Services.AddSingleton<ISafeSaveWriter, ReadOnlySaveWriter>();"),
        ("        builder.Services.AddSingleton<ISaveFileAccess, AndroidSafFileAccess>();",
         "        builder.Services.AddSingleton<ISaveFileAccess>(_ => new ReadOnlySaveFileAccess(new AndroidSafFileAccess()));"),
    )
    for old, new in changes:
        count = source.count(old)
        if count != 1:
            raise ProbeRefusal(f"Expected one pinned DI registration, got {count}: {old}")
        source = source.replace(old, new, 1)
    return source


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected PKForge HEAD")
    program = root / PROGRAM
    destinations = [root / WRITER, root / ACCESS]
    if program.is_symlink() or not program.is_file():
        raise ProbeRefusal("Missing or symlinked MauiProgram")
    if any(p.exists() or p.is_symlink() or p.parent.is_symlink() or
           not p.parent.is_dir() for p in destinations):
        raise ProbeRefusal("Containment source target already exists or is unsafe")
    transformed = adapt_program(program.read_text(encoding="utf-8"))
    # Validate all inputs before changing any upstream file.
    program.write_text(transformed, encoding="utf-8")
    destinations[0].write_text(WRITER_SOURCE, encoding="utf-8")
    destinations[1].write_text(ACCESS_SOURCE, encoding="utf-8")
    return {"mode": "temporary read-only MAUI", "changed_files": [PROGRAM, WRITER, ACCESS],
            "writes_enabled": False, "save_as_enabled": False, "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
