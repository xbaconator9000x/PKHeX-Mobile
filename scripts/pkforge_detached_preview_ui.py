#!/usr/bin/env python3
"""Attach a NON-PUBLISHING detached nickname preview to pinned PKForge's MAUI editor.

Applies only to a temporary, exact-source checkout used for CI compilation.
The existing PKForge save writer is NOT certified by this patch. No APK is
released, no Android document is opened writable, and no source art is copied
into PKHeX Mobile. This is a UI wiring/compilation gate, not device testing.
"""
import argparse
import json
from pathlib import Path
from xml.sax.saxutils import escape

from scripts.pkforge_official_core_probe import ProbeRefusal

VIEW = "src/PKForge.App/Views/BoxBrowserPage.cs"
PROJECT = "src/PKForge.App/PKForge.App.csproj"


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected exactly one pinned anchor, found {count}")
    return source.replace(old, new, 1)


def adapt_view(source: str) -> str:
    if "using PKHeXMobile.PortConsolidation;" in source or "detachedPreview" in source:
        raise ProbeRefusal("MAUI editor is already adapted; refusing duplicate UI wiring.")
    source = replace_once(source, "using PKForge.App.Services;\n",
        "using PKForge.App.Services;\nusing PKHeXMobile.Domain;\n"
        "using PKHeXMobile.PortConsolidation;\n", "UI namespaces")
    anchor = '        var save = FocusButton(EditorTool("Save changes", primary: true), "SAVE CHANGES");'
    preview = """        // Only preview the current in-memory session. It may differ from the
        // provider's original bytes; it never grants Save As or in-place writes.
        // The existing PKForge save actions are NOT safety-certified by this probe.
        var detachedPreview = FocusButton(
            EditorTool("Preview nickname (discard)"), "PREVIEW NICKNAME");
        detachedPreview.Clicked += async (_, _) =>
        {
            var session = _sessionsFor();
            var box = _viewModel.BoxIndex;
            var slot = _viewModel.SelectedSlot;
            var proposed = _viewModel.EditNickname;
            if (session is null || slot < 0 || string.IsNullOrWhiteSpace(proposed))
                return;
            try
            {
                var source = SaveInputSnapshot.FromBytes(session.Serialize().Span);
                var preview = DetachedRetailNicknameEditor.Preview(source, box, slot, proposed);
                var location = box == -1
                    ? $"Party slot {slot + 1}"
                    : $"Box {box + 1}, slot {slot + 1}";
                await PadMenu.ShowAsync(_hostGrid, "Detached nickname preview",
                    $"{location}: {preview.OriginalNickname} → {preview.PreviewNickname}\\n" +
                    "In-memory preview only. No document was saved.", "Discard");
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidDataException or InvalidOperationException)
            {
                await PadMenu.ShowAsync(_hostGrid, "Preview refused",
                    error.Message, "OK");
            }
        };

"""
    source = replace_once(source, anchor, preview + anchor, "editor button")
    source = replace_once(source,
        "        panel.Children.Add(save);\n        panel.Children.Add(monActions);",
        "        panel.Children.Add(detachedPreview);\n"
        "        panel.Children.Add(save);\n        panel.Children.Add(monActions);",
        "editor panel wiring")
    return source


def adapt_project(source: str, mobile_root: Path) -> str:
    if "PKHeXMobile.PortConsolidation.csproj" in source:
        raise ProbeRefusal("MAUI project already references the mobile adapter.")
    dependency = mobile_root / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj"
    if not dependency.is_file() or dependency.is_symlink():
        raise ProbeRefusal("Pinned mobile adapter project is missing or symlinked.")
    anchor = '    <ProjectReference Include="../PKForge.Infrastructure/PKForge.Infrastructure.csproj" />'
    reference = f'    <ProjectReference Include="{escape(str(dependency))}" />'
    return replace_once(source, anchor, anchor + "\n" + reference, "UI project reference")


def prepare(root: Path, mobile_root: Path) -> dict:
    root = root.resolve(strict=True)
    mobile_root = mobile_root.resolve(strict=True)
    paths = [root / VIEW, root / PROJECT]
    for path in paths:
        if path.is_symlink() or not path.is_file():
            raise ProbeRefusal(f"Pinned MAUI source missing or symlinked: {path}")
    # Validate both transformations before touching either upstream file.
    updated = [adapt_view(paths[0].read_text(encoding="utf-8")),
               adapt_project(paths[1].read_text(encoding="utf-8"), mobile_root)]
    for path, text in zip(paths, updated):
        path.write_text(text, encoding="utf-8")
    return {"mode": "temporary compile-only MAUI preview wiring",
            "changed_files": [VIEW, PROJECT],
            "writes_certified": False, "device_tested": False,
            "desktop_parity_certified": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--mobile", required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(prepare(args.repo, args.mobile), indent=2))
