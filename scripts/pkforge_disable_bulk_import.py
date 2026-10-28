#!/usr/bin/env python3
"""Disable PKForge's unbounded PKSM multi-document import in preview-only builds.

The existing single-document picker and read-only inspector remain available.
The import entry point and Android multi-picker both refuse before any URI
allocation, provider read or bank mutation. Re-enable only after aggregate,
cancellation and bank-transaction tests pass.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal, sha
from scripts.pkforge_provider_origin_binding import once

PICKER = "src/PKForge.App/Platforms/Android/AndroidDocumentPicker.cs"
FLOW = "src/PKForge.App/Views/PksmTransferFlow.cs"


def replace_between(source: str, start: str, end: str, replacement: str, label: str) -> str:
    if source.count(start) != 1 or source.count(end) != 1:
        raise ProbeRefusal(f"{label}: missing or duplicated pinned anchors")
    a = source.index(start)
    b = source.index(end, a + len(start))
    if b <= a:
        raise ProbeRefusal(f"{label}: unexpected anchor ordering")
    return source[:a] + replacement + source[b:]


def adapt_picker(source: str) -> str:
    if "PKSM_MULTI_IMPORT_DISABLED" in source:
        raise ProbeRefusal("Multi-picker already disabled")
    source = replace_between(
        source,
        "    public ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default)",
        "    private bool HandleMultiResult(int requestCode, Result resultCode, Intent? data)",
        """    public ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PKSM_MULTI_IMPORT_DISABLED: no pending task, launch or URI allocation.
        throw new NotSupportedException("Multi-file PKSM import is disabled in preview-only mode.");
    }

""",
        "multi-picker")
    source = once(source,
        "    private const int MultiRequestCode = 4109;\n"
        "    private TaskCompletionSource<IReadOnlyList<PickedDocument>>? _pendingMany;\n\n",
        "", "multi-picker pending state")
    source = replace_between(source,
        "    private bool HandleMultiResult(int requestCode, Result resultCode, Intent? data)",
        "    public bool HandleActivityResult(int requestCode, Result resultCode, Intent? data)",
        "", "multi-result handler")
    source = once(source,
        "        if (HandleMultiResult(requestCode, resultCode, data))\n"
        "            return true;\n",
        "", "multi-result dispatch")
    return source


def adapt_flow(source: str) -> str:
    if "PKSM_MULTI_IMPORT_DISABLED" in source:
        raise ProbeRefusal("PKSM import flow already disabled")
    return replace_between(
        source,
        "    public static async Task<string?> ImportAsync(Grid host, IBankService bank)",
        "    private static (List<PksmImportPlan>, List<string>) BuildPlans(",
        """    public static Task<string?> ImportAsync(Grid host, IBankService bank)
    {
        // PKSM_MULTI_IMPORT_DISABLED: never launch picker, decode files or mutate bank.
        return Task.FromResult<string?>("PKSM import is disabled in read-only preview mode.");
    }

""",
        "PKSM import flow")


def prepare(root: Path) -> dict:
    root = root.resolve(strict=True)
    if sha(root) != PKFORGE:
        raise ProbeRefusal("Unexpected pinned PKForge HEAD")
    paths = [root / PICKER, root / FLOW]
    if any(not p.is_file() or p.is_symlink() or p.parent.is_symlink() for p in paths):
        raise ProbeRefusal("Missing or symlinked pinned import sources")
    # No partial transformation: preflight both files before writing either.
    changed = [adapt_picker(paths[0].read_text(encoding="utf-8")),
               adapt_flow(paths[1].read_text(encoding="utf-8"))]
    for path, data in zip(paths, changed):
        path.write_text(data, encoding="utf-8")
    return {"mode": "PKSM multi-import disabled, read-only preview",
            "changed_files": [PICKER, FLOW], "writes_enabled": False,
            "bulk_import_enabled": False, "device_tested": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.repo), indent=2))
