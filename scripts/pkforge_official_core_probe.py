#!/usr/bin/env python3
"""Code-only compatibility probe: pinned PKForge against official PKHeX.Core.

Rewrites a temporary upstream checkout, not the production application.
No APK, assets or ROM-hack write permission is created by this diagnostic.
"""
import argparse
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

PKFORGE = "2df04430e532b78c247f45b936f3068d8f824987"
FORK = "91e9c88667a6a191a000db68e2db19505eb87561"
PLUGINS = "23ab1fc73f7be6ad427fbc900f9b99111cd2d560"
VERSION = "[26.10.18]"
PROJECTS = (
    "src/PKForge.Engine/PKForge.Engine.csproj",
    "src/PKForge.AutoMod/PKForge.AutoMod.csproj",
)


class ProbeRefusal(ValueError):
    pass


def rewrite(xml):
    """Replace exactly one fork Core reference; reject any changed input shape."""
    try:
        root = ET.fromstring(xml)
    except ET.ParseError as exc:
        raise ProbeRefusal("Malformed project XML") from exc
    if root.tag != "Project":
        raise ProbeRefusal("Unexpected project root")
    found = []
    for group in root.findall("ItemGroup"):
        for item in group:
            if item.tag == "PackageReference" and item.get("Include", "").lower() == "pkhex.core":
                raise ProbeRefusal("Core package already present")
            if item.tag != "ProjectReference":
                continue
            path = item.get("Include", "").replace("\\", "/")
            if path.endswith("external/PKHeX/PKHeX.Core/PKHeX.Core.csproj"):
                found.append((group, item))
    if len(found) != 1:
        raise ProbeRefusal(f"Expected exactly one Core reference, got {len(found)}")
    group, old = found[0]
    if len(old.attrib) != 1 or len(old):
        raise ProbeRefusal("Unreviewed Core reference metadata")
    pos = list(group).index(old)
    group.remove(old)
    group.insert(pos, ET.Element("PackageReference", {"Include": "PKHeX.Core", "Version": VERSION}))
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="unicode") + "\n"


PLUGIN_PATH = "external/PKHeX-Plugins/PKHeX.Core.AutoMod/AutoMod/APILegality.cs"
OLD_SEED64 = "GS64.GenerateSeed64(raw, converted64); break;"
NEW_SEED64 = "GS64.GenerateSeed64(raw, tr, converted64); break;"


def adapt_seed64_plugin(source):
    """Adapt the pinned AutoMod call to official Core's ITrainerInfo argument.

    Exact one-site replacement only. The plugin stays in the temporary upstream
    checkout, and this adaptation is not legality or AutoMod runtime certification.
    """
    if source.count(OLD_SEED64) != 1 or NEW_SEED64 in source:
        raise ProbeRefusal("Pinned AutoMod GenerateSeed64 call changed")
    return source.replace(OLD_SEED64, NEW_SEED64)


def sha(root, relative="."):
    return subprocess.check_output(
        ["git", "-C", str(root / relative), "rev-parse", "HEAD"],
        text=True, stderr=subprocess.PIPE,
    ).strip()


def prepare(root):
    root = root.resolve(strict=True)
    if (sha(root), sha(root, "external/PKHeX"), sha(root, "external/PKHeX-Plugins")) != (
        PKFORGE, FORK, PLUGINS,
    ):
        raise ProbeRefusal("Pinned source or gitlink revision mismatch")
    status = subprocess.check_output(
        ["git", "-C", str(root), "status", "--porcelain"],
        text=True, stderr=subprocess.PIPE,
    )
    if status.strip():
        raise ProbeRefusal("Upstream checkout is not clean")
    # Validate both inputs before changing either.
    changes = {}
    for relative in PROJECTS:
        path = root / relative
        if path.is_symlink() or not path.is_file():
            raise ProbeRefusal(f"Missing/symlinked project: {relative}")
        changes[path] = rewrite(path.read_text(encoding="utf-8"))
    plugin = root / PLUGIN_PATH
    if plugin.is_symlink() or not plugin.is_file():
        raise ProbeRefusal("Pinned AutoMod source missing or symlinked")
    changes[plugin] = adapt_seed64_plugin(plugin.read_text(encoding="utf-8"))
    for path, content in changes.items():
        path.write_text(content, encoding="utf-8")
    return {"pkforge": PKFORGE, "fork": FORK, "plugins": PLUGINS,
            "official_core": VERSION, "changed_projects": list(PROJECTS), "adapted_plugin": PLUGIN_PATH,
            "writes_enabled": False, "redistribution_authorized": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    print(json.dumps(prepare(parser.parse_args().repo), indent=2))
