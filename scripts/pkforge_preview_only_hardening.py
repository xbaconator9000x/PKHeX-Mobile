#!/usr/bin/env python3
"""Harden the pinned PKForge Android preview against grants and live mutation.

Applies to a temporary, exact-source PKForge checkout AFTER official-Core and
read-only DI adaptations. Never enables Save As, original writes or hack edits.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha

PICKER = "src/PKForge.App/Platforms/Android/AndroidDocumentPicker.cs"
ACCESS = "src/PKForge.App/Platforms/Android/AndroidSafFileAccess.cs"
VIEWMODEL = "src/PKForge.App/ViewModels/BoxBrowserViewModel.cs"
GRANTS = "src/PKForge.App/Services/ReadOnlySourceGrants.cs"

GRANTS_SOURCE = """// PKHeX Mobile adaptation of pinned PKForge 2df04430; GPL-3.0-only.
namespace PKForge.App;

/// <summary>Least-privilege Android source-document grant policy.</summary>
/// <remarks>Uses Android Intent grant bit values, without depending on Android
/// types, so the policy can be executed in portable regression tests.</remarks>
public static class ReadOnlySourceGrants
{
    public const int Read = 0x00000001;
    public const int Write = 0x00000002;
    public const int Persistable = 0x00000040;
    public const int RequestedFlags = Read | Persistable;

    public static int PersistedFlags(int returnedFlags)
    {
        if ((returnedFlags & Read) == 0)
            throw new InvalidOperationException("The picker result did not grant read access.");
        return Read; // Never persist a write grant, even if a provider supplies one.
    }
}
"""

def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one pinned anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_picker(source: str) -> str:
    old = ("intent.AddFlags(ActivityFlags.GrantReadUriPermission | "
           "ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);")
    new = "intent.AddFlags((ActivityFlags)ReadOnlySourceGrants.RequestedFlags);"
    source = replace_once(source, old, new, "single source picker grant")
    if source.count("intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);") != 1:
        raise ProbeRefusal("Pinned multi-picker grant changed")
    if "GrantWriteUriPermission" in source:
        raise ProbeRefusal("Source picker still requests write permission")
    return source


OLD_WRITE = """    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        var uri = Parse(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        // SAF providers do not expose a portable rename-and-swap primitive. The descriptor must support
        // reliable truncate/write/flush; providers that reject this mode fail closed.
        using var descriptor = Resolver.OpenFileDescriptor(uri, "rwt")
            ?? throw new IOException($"The document provider does not support safe truncate/write access for {uri}.");
        var output = new ParcelFileDescriptor.AutoCloseOutputStream(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        output.Write(bytes.ToArray());
        output.Flush();
        output.Close();
        return ValueTask.CompletedTask;
    }
"""
NEW_WRITE = """    // Defense in depth: direct callers cannot bypass the DI writer/access barriers.
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));
"""


def adapt_access(source: str) -> str:
    source = replace_once(source, OLD_WRITE, NEW_WRITE, "direct Android SAF writer")
    old = """        var grants = intent.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if (grants == 0)
            throw new InvalidOperationException("The picker result did not grant document access.");
        Resolver.TakePersistableUriPermission(uri, grants);"""
    new = """        var grants = (ActivityFlags)ReadOnlySourceGrants.PersistedFlags((int)intent.Flags);
        Resolver.TakePersistableUriPermission(uri, grants);"""
    source = replace_once(source, old, new, "persisted source grant")
    if "GrantWriteUriPermission" in source or 'OpenFileDescriptor(uri, "rwt")' in source:
        raise ProbeRefusal("Android source file still has a write-capable route")
    return source


def adapt_viewmodel(source: str) -> str:
    guard = """    // Temporary Android consolidation is preview-only, regardless of DI registration.
    // Refuse before mutating the live session, including keyboard/gamepad and bulk routes.
    private bool RefuseLiveMutation()
    {
        Status = "Preview-only: live edits, releases and moves are disabled. No save was changed.";
        return true;
    }

"""
    source = replace_once(source,
        "    [RelayCommand]\n    private async Task SaveEditAsync()\n    {\n        if (IsBusy) return;",
        guard + "    [RelayCommand]\n    private async Task SaveEditAsync()\n    {\n        if (RefuseLiveMutation()) return;\n        if (IsBusy) return;",
        "save-edit pre-mutation guard")
    source = replace_once(source,
        "        if (HardcoreMode.Blocks(action, out var hardcoreStatus))\n        {\n",
        "        if (RefuseLiveMutation()) return false;\n        if (HardcoreMode.Blocks(action, out var hardcoreStatus))\n        {\n",
        "bulk/legalizer pre-mutation guard")
    source = replace_once(source,
        "    public bool BeginCarry()\n    {\n        if (Save is null || SelectedSlot < 0) return false;",
        "    public bool BeginCarry()\n    {\n        if (RefuseLiveMutation()) return false;\n        if (Save is null || SelectedSlot < 0) return false;",
        "carry pre-mutation guard")
    source = replace_once(source,
        "    public async Task DropAsync()\n    {\n        if (CarrySource is null || _dropWaiting) return;",
        "    public async Task DropAsync()\n    {\n        if (RefuseLiveMutation())\n        {\n            CarrySource = null;\n            CarriedSummary = null;\n            return;\n        }\n        if (CarrySource is null || _dropWaiting) return;",
        "move/drop pre-mutation guard")
    # This source has exactly three live mutation families. All must be guarded
    # before ApplyEdit, MoveSlot or the RunMutation operation delegate executes.
    for marker in ("engineSession.ApplyEdit(", "engineSession.MoveSlot(", "operation(engineSession)"):
        if source.count(marker) != 1:
            raise ProbeRefusal(f"Unreviewed mutation route: {marker}")
    if source.count("RefuseLiveMutation()") != 5:
        raise ProbeRefusal("Missing pre-mutation guard")
    return source


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    files = [root / PICKER, root / ACCESS, root / VIEWMODEL]
    destination = root / GRANTS
    if any(p.is_symlink() or not p.is_file() for p in files):
        raise ProbeRefusal("Missing or symlinked pinned source")
    if destination.exists() or destination.is_symlink() or destination.parent.is_symlink() or not destination.parent.is_dir():
        raise ProbeRefusal("Grant-policy target exists or is unsafe")
    originals = [p.read_text(encoding="utf-8") for p in files]
    updated = [adapt_picker(originals[0]), adapt_access(originals[1]),
               adapt_viewmodel(originals[2])]
    # Validate every input before changing the temporary checkout.
    try:
        for path, content in zip(files, updated):
            path.write_text(content, encoding="utf-8")
        destination.write_text(GRANTS_SOURCE, encoding="utf-8")
    except OSError:
        for path, content in zip(files, originals):
            path.write_text(content, encoding="utf-8")
        destination.unlink(missing_ok=True)
        raise
    return {"mode": "temporary preview-only Android hardening",
            "changed_files": [PICKER, ACCESS, VIEWMODEL, GRANTS],
            "requested_source_write_grant": False,
            "persisted_source_write_grant": False,
            "live_mutations_enabled": False, "save_as_enabled": False,
            "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
