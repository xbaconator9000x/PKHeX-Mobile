#!/usr/bin/env python3
"""Deny all PKForge SAF folder exports in the pinned preview-only Android build.

This closes an independent write surface used by bank and PKSM exports. Folder
imports remain available with bounded reads. This adaptation neither grants
Save As nor certifies a real Android document provider.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

PROGRAM = "src/PKForge.App/MauiProgram.cs"
PICKER = "src/PKForge.App/Platforms/Android/AndroidFolderPicker.cs"
ACCESS = "src/PKForge.App/Platforms/Android/AndroidFolderFileAccess.cs"
WRAPPER = "src/PKForge.App/Services/ReadOnlyFolderFileAccess.cs"

OLD_PICK = """    public ValueTask<PickedFolder?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        if (_pending is not null)
            throw new InvalidOperationException("A folder picker is already open.");

        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No foreground Android activity is available.");
        _pending = new TaskCompletionSource<PickedFolder?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellationRegistration = cancellationToken.Register(() =>
        {
            var pending = _pending;
            _pending = null;
            pending?.TrySetCanceled(cancellationToken);
        });
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, RequestCode);
        return new ValueTask<PickedFolder?>(_pending.Task);
    }
"""
NEW_PICK = """    public ValueTask<PickedFolder?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_pending is not null)
            throw new InvalidOperationException("A folder picker is already open.");

        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No foreground Android activity is available.");
        var pending = new TaskCompletionSource<PickedFolder?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        // Keep the cancelled picker reserved until Android returns its late result.
        // A second request must not accidentally consume the first request's result.
        _cancellationRegistration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        if (pending.Task.IsCanceled)
        {
            _pending = null;
            _cancellationRegistration.Dispose();
            return new ValueTask<PickedFolder?>(pending.Task);
        }
        try
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch
        {
            _pending = null;
            _cancellationRegistration.Dispose();
            throw;
        }
        return new ValueTask<PickedFolder?>(pending.Task);
    }
"""
OLD_RESULT = """        _cancellationRegistration.Dispose();
        if (resultCode != Result.Ok || data?.Data is not { } uri)"""
NEW_RESULT = """        _cancellationRegistration.Dispose();
        // Never persist a grant from a cancelled, late result.
        if (pending.Task.IsCanceled)
            return true;
        if (resultCode != Result.Ok || data?.Data is not { } uri)"""

OLD_READ = """    public async ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var uri = Parse(documentId);
        await using var input = Resolver.OpenInputStream(uri)
            ?? throw new IOException($"The document provider could not open {uri} for reading.");
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
"""
NEW_READ = """    public async ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(string documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = Parse(documentId);
        await using var input = Resolver.OpenInputStream(uri)
            ?? throw new IOException($"The document provider could not open {uri} for reading.");
        var snapshot = await SaveInputSnapshot.ReadAsync(input, cancellationToken).ConfigureAwait(false);
        return snapshot.CopyBytes();
    }
"""
OLD_WRITE = """    public ValueTask WriteFileAsync(string treeId, string fileName, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        return new ValueTask(Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var treeUri = Resolve(treeId);
            var target = FindDocument(treeUri, fileName) ?? CreateDocument(treeUri, fileName);
            using var output = Resolver.OpenOutputStream(target, "wt")
                ?? throw new IOException($"The document provider could not open {target} for writing.");
            await output.WriteAsync(bytes.ToArray(), cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken));
    }
"""
NEW_WRITE = """    // Independent bank/PKSM export surface: never write even if called directly.
    public ValueTask WriteFileAsync(string treeId, string fileName, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));
"""
OLD_HELPERS = """    /// <summary>The document URI of the exact-named file in the folder root, or null when absent.</summary>
    private static AndroidUri? FindDocument(AndroidUri treeUri, string fileName) =>
        QueryChildren(treeUri).FirstOrDefault(child => child.Name == fileName) is { } match
            ? DocumentsContract.BuildDocumentUriUsingTree(treeUri, match.DocId)
            : null;

    private static AndroidUri CreateDocument(AndroidUri treeUri, string fileName)
    {
        var rootDocId = DocumentsContract.GetTreeDocumentId(treeUri)
            ?? throw new InvalidOperationException("The folder grant has no tree document id.");
        var parent = DocumentsContract.BuildDocumentUriUsingTree(treeUri, rootDocId)
            ?? throw new IOException("The folder grant could not be resolved to a document.");
        var mime = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "application/octet-stream";
        return DocumentsContract.CreateDocument(Resolver, parent, mime, fileName)
            ?? throw new IOException($"The folder rejected a new file named {fileName}.");
    }

"""
WRAPPER_SOURCE = """// PKHeX Mobile adaptation of pinned PKForge 2df04430; GPL-3.0-only.
using PKForge.Domain;
using PKHeXMobile.Domain;

namespace PKForge.App;

/// <summary>Read-only bank/folder access. No export can reach the provider.</summary>
public sealed class ReadOnlyFolderFileAccess(IFolderFileAccess reader) : IFolderFileAccess
{
    public ValueTask<IReadOnlyList<PickedDocument>> ListFilesAsync(
        string treeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return reader.ListFilesAsync(treeId, cancellationToken);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = await reader.ReadFileAsync(documentId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return SaveInputSnapshot.FromBytes(bytes.Span).CopyBytes();
    }

    public ValueTask WriteFileAsync(
        string treeId, string fileName, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
        => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));
}
"""

def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one pinned anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_picker(source: str) -> str:
    source = replace_once(source, OLD_PICK, NEW_PICK, "folder picker read-only lifecycle")
    source = replace_once(source, OLD_RESULT, NEW_RESULT, "late folder picker result")
    if "GrantWriteUriPermission" in source:
        raise ProbeRefusal("Folder picker still requests a write grant")
    if source.count("AndroidSafFileAccess.PersistPermission(uri, data);") != 1:
        raise ProbeRefusal("Folder picker permission handoff changed")
    return source


def adapt_access(source: str) -> str:
    source = replace_once(source, "using PKForge.Domain;\n",
        "using PKForge.Domain;\nusing PKHeXMobile.Domain;\n", "folder read dependency")
    source = replace_once(source, OLD_READ, NEW_READ, "bounded folder read")
    source = replace_once(source, OLD_WRITE, NEW_WRITE, "folder write denial")
    source = replace_once(source, OLD_HELPERS, "", "remove folder create/overwrite helpers")
    if any(marker in source for marker in
           ("OpenOutputStream", "CreateDocument(", "CopyToAsync(buffer", "GrantWriteUriPermission")):
        raise ProbeRefusal("Folder provider retains a direct write or unbounded read route")
    return source


def adapt_program(source: str) -> str:
    if "ReadOnlySaveWriter" not in source or "ReadOnlySaveFileAccess" not in source:
        raise ProbeRefusal("Existing save write containment must be installed first")
    return replace_once(source,
        "        builder.Services.AddSingleton<IFolderFileAccess, AndroidFolderFileAccess>();",
        "        builder.Services.AddSingleton<IFolderFileAccess>(_ => new ReadOnlyFolderFileAccess(new AndroidFolderFileAccess()));",
        "folder DI registration")


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected PKForge HEAD")
    paths = [root / PROGRAM, root / PICKER, root / ACCESS]
    destination = root / WRAPPER
    if any(p.is_symlink() or not p.is_file() or p.parent.is_symlink() for p in paths):
        raise ProbeRefusal("Missing or symlinked pinned folder source")
    if destination.exists() or destination.is_symlink() or destination.parent.is_symlink() or not destination.parent.is_dir():
        raise ProbeRefusal("Folder containment target exists or is unsafe")
    originals = [p.read_text(encoding="utf-8") for p in paths]
    updated = [adapt_program(originals[0]), adapt_picker(originals[1]), adapt_access(originals[2])]
    try:
        for path, value in zip(paths, updated):
            path.write_text(value, encoding="utf-8")
        destination.write_text(WRAPPER_SOURCE, encoding="utf-8")
    except OSError:
        for path, value in zip(paths, originals):
            path.write_text(value, encoding="utf-8")
        destination.unlink(missing_ok=True)
        raise
    return {"mode": "temporary read-only folder import",
            "changed_files": [PROGRAM, PICKER, ACCESS, WRAPPER],
            "folder_write_grant_requested": False, "folder_exports_enabled": False,
            "folder_reads_bounded": True, "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
