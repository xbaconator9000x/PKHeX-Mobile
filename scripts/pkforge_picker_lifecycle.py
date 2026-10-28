#!/usr/bin/env python3
"""Fail-closed single-save picker lifecycle for pinned PKForge Android MAUI.

Apply AFTER preview-only grant hardening. A cancelled external picker stays
reserved until its result is consumed, so a late result cannot select a new
document. Read-once is allowed only with an actual read grant.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

PICKER = "src/PKForge.App/Platforms/Android/AndroidDocumentPicker.cs"


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one pinned anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_picker(source: str) -> str:
    if source.count("intent.AddFlags((ActivityFlags)ReadOnlySourceGrants.RequestedFlags);") != 1:
        raise ProbeRefusal("Read-only single-picker grant policy is missing")
    if "GrantWriteUriPermission" in source:
        raise ProbeRefusal("Source picker requests write access")
    source = replace_once(source,
        """    public ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default)
    {
        if (_pending is not null)""",
        """    public ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_pending is not null)""", "pre-cancelled picker")
    source = replace_once(source,
        """        _pending = new TaskCompletionSource<PickedDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellationRegistration = cancellationToken.Register(() =>
        {
            var pending = _pending;
            _pending = null;
            pending?.TrySetCanceled(cancellationToken);
        });
        var intent = new Intent(Intent.ActionOpenDocument);""",
        """        var pending = new TaskCompletionSource<PickedDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        // Keep the request reserved until its Android result arrives. A late
        // cancelled result must never attach to a subsequently opened picker.
        _cancellationRegistration = cancellationToken.Register(() =>
            pending.TrySetCanceled(cancellationToken));
        if (cancellationToken.IsCancellationRequested)
        {
            _pending = null;
            _cancellationRegistration.Dispose();
            return new ValueTask<PickedDocument?>(pending.Task);
        }
        var intent = new Intent(Intent.ActionOpenDocument);""", "picker cancellation ownership")
    source = replace_once(source,
        """        activity.StartActivityForResult(intent, RequestCode);
        return new ValueTask<PickedDocument?>(_pending.Task);""",
        """        try
        {
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch
        {
            _pending = null;
            _cancellationRegistration.Dispose();
            throw;
        }
        return new ValueTask<PickedDocument?>(pending.Task);""", "picker launch rollback")
    source = replace_once(source,
        """        var pending = _pending;
        _pending = null;
        _cancellationRegistration.Dispose();
        if (resultCode != Result.Ok || data?.Data is not { } uri)""",
        """        var pending = _pending;
        _pending = null;
        _cancellationRegistration.Dispose();
        if (pending.Task.IsCompleted)
            return true; // Cancelled external result; never rebind to a newer session.
        if (resultCode != Result.Ok || data?.Data is not { } uri)""", "late picker result")
    source = replace_once(source,
        """        try
        {
            AndroidSafFileAccess.PersistPermission(uri, data);
            var documentId = uri.ToString()""",
        """        try
        {
            // A read-once grant is acceptable for immediate bounded two-open
            // inspection; a missing read grant is not. Never request writes.
            ReadOnlySourceGrants.PersistedFlags((int)data.Flags);
            try { AndroidSafFileAccess.PersistPermission(uri, data); }
            catch (Java.Lang.SecurityException) { /* Provider grants read-once only. */ }
            var documentId = uri.ToString()""", "read-once provider fallback")
    return source


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    path = root / PICKER
    if not path.is_file() or path.is_symlink() or path.parent.is_symlink():
        raise ProbeRefusal("Missing or symlinked pinned Android picker")
    original = path.read_text(encoding="utf-8")
    updated = adapt_picker(original)
    path.write_text(updated, encoding="utf-8")
    return {"mode": "temporary read-only picker lifecycle",
            "changed_files": [PICKER], "write_grants_requested": False,
            "late_cancelled_results_rebound": False,
            "read_once_fallback": True, "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
