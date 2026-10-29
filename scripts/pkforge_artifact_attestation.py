#!/usr/bin/env python3
"""Fail-closed provenance for the adapted PKForge Android diagnostic build.

This produces metadata ONLY. It never copies, uploads or authorizes an APK.
An APK containing unreviewed upstream media must not be published.
"""
from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

PKFORGE = "2df04430e532b78c247f45b936f3068d8f824987"
FORK = "91e9c88667a6a191a000db68e2db19505eb87561"
PLUGINS = "23ab1fc73f7be6ad427fbc900f9b99111cd2d560"
MAUI = "a6f7cfb78e1c96155baa67660fd7ae97895e375f"
CORE_SOURCE = "bfe89c74a3f5265805e364b79bc04ad38ebbc5ea"
CORE_PACKAGE = "26.10.18"
EXPECTED_PACKAGE = "org.pkforge.app.debug"
PROJECT = "src/PKForge.App/PKForge.App.csproj"
ASSET_TAGS = {"MauiAsset", "MauiIcon", "MauiSplashScreen", "MauiFont",
              "MauiImage", "AndroidAsset", "EmbeddedResource"}


class AttestationError(ValueError):
    pass


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def git_head(root: Path) -> str:
    return subprocess.check_output(
        ["git", "-C", str(root), "rev-parse", "HEAD"], text=True
    ).strip()


def check_pins(root: Path) -> None:
    actual = (git_head(root), git_head(root / "external/PKHeX"),
              git_head(root / "external/PKHeX-Plugins"))
    if actual != (PKFORGE, FORK, PLUGINS):
        raise AttestationError("Upstream checkout/gitlink SHA mismatch")


def classify(path: str) -> str:
    path = path.replace("\\", "/")
    if any(s in path for s in (
        "PKHeX.Drawing.", "/Showdown/", "/GameArt/", "/GameHero/",
        "/GameLogo/", "/Pokepark/", "/BadApple/", "/UI/lumi/",
    )):
        return "media-rights-unverified"
    if path.lower().endswith((".png", ".jpg", ".jpeg", ".webp", ".ttf",
                              ".otf", ".svg", ".mp3", ".wav", ".gif")):
        return "asset-rights-unverified"
    return "redistribution-unreviewed"


def inventory_assets(root: Path, project: Path) -> list[dict]:
    try:
        xml = ET.parse(project)
    except (ET.ParseError, OSError) as exc:
        raise AttestationError("Unreadable MAUI project") from exc
    base = project.parent
    entries = []
    for element in xml.iter():
        tag = element.tag.rsplit("}", 1)[-1]
        if tag not in ASSET_TAGS or "Include" not in element.attrib:
            continue
        pattern = element.attrib["Include"].replace("\\", "/")
        if "$(" in pattern or "@(" in pattern or "%(" in pattern:
            raise AttestationError("Unresolved asset Include expression")
        excluded = [s.replace("\\", "/") for s in
                    element.attrib.get("Exclude", "").split(";") if s]
        matches = sorted(glob.glob(str(base / pattern), recursive=True))
        files = [Path(p) for p in matches if Path(p).is_file()]
        for file in files:
            resolved = file.resolve(strict=True)
            if not resolved.is_relative_to(root.resolve()):
                raise AttestationError("Asset escapes pinned checkout")
            if any(glob.fnmatch.fnmatch(str(file), str(base / e)) for e in excluded):
                continue
            entries.append({
                "kind": tag,
                "source": resolved.relative_to(root.resolve()).as_posix(),
                "sha256": digest(resolved),
                "rights": classify(resolved.as_posix()),
            })
        if not files:
            entries.append({
                "kind": tag, "source_pattern": pattern,
                "rights": "unresolved-pattern-no-distribution",
            })
    if not entries:
        raise AttestationError("No MAUI asset declarations found")
    return entries


def find_android_packager() -> Path:
    for tool in ("aapt2", "aapt"):
        found = shutil.which(tool)
        if found:
            return Path(found)
    for env in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        sdk = os.environ.get(env)
        if sdk:
            for tool in ("aapt2", "aapt"):
                found = sorted(Path(sdk).glob("build-tools/*/" + tool), reverse=True)
                if found:
                    return found[0]
    raise AttestationError("No Android aapt/aapt2 available to verify APK identity")


def inspect_apk(apk: Path, packager: Path) -> dict:
    """Inspect actual packaged runtime, not merely a spoofable package label.

    This is a structural diagnostic, NOT an Android launch or rights certificate.
    Managed assemblies may use the .NET Android assembly store, so both the
    ordinary DLL layout and the documented packed layouts are recognized.
    """
    if apk.is_symlink() or not apk.is_file() or apk.stat().st_size == 0:
        raise AttestationError("Missing, symlinked or empty APK")
    try:
        with zipfile.ZipFile(apk) as archive:
            infos = [info for info in archive.infolist() if not info.is_dir()]
            names = [info.filename for info in infos]
            if len(names) != len(set(names)):
                raise AttestationError("Duplicate APK ZIP entries")
            if any(
                name.startswith("/") or "\\" in name
                or any(part in ("", ".", "..") for part in name.split("/"))
                or (info.external_attr >> 16) & 0o170000 == 0o120000
                for info, name in zip(infos, names)
            ):
                raise AttestationError("Unsafe APK ZIP entry path or symlink")
            if archive.testzip() is not None or "AndroidManifest.xml" not in names:
                raise AttestationError("Invalid APK archive or missing manifest")

            dex = sorted(name for name in names
                         if re.fullmatch(r"classes(?:[0-9]+)?\.dex", name))
            native = sorted(name for name in names
                            if re.fullmatch(
                                r"lib/(?:arm64-v8a|armeabi-v7a|x86|x86_64)/"
                                r"lib(?:monodroid|monosgen-2\.0|xamarin-app)\.so",
                                name))
            managed = sorted(name for name in names if (
                re.fullmatch(
                    r"assemblies/PKForge\.(?:App|Engine|Infrastructure)\.dll",
                    name,
                ) or re.fullmatch(
                    r"lib/(?:arm64-v8a|armeabi-v7a|x86|x86_64)/"
                    r"lib[_-]PKForge\.(?:App|Engine|Infrastructure)\.dll\.so",
                    name,
                )
            ))
            if not dex:
                raise AttestationError("APK missing executable classes.dex payload")
            if not native:
                raise AttestationError("APK missing .NET Android native runtime payload")
            for component in ("App", "Engine", "Infrastructure"):
                if not any(f"PKForge.{component}.dll" in name for name in managed):
                    candidates = sorted(
                        name for name in names if "PKForge" in name
                    )[:20]
                    raise AttestationError(
                        f"APK missing embedded PKForge.{component} managed payload; "
                        f"observed={candidates!r}"
                    )

            # Hash the actual packaged entries, including implicitly imported
            # resources. Source project Include globs alone cannot prove contents.
            entry_hash = hashlib.sha256()
            media_count = 0
            for info in sorted(infos, key=lambda item: item.filename):
                h = hashlib.sha256()
                with archive.open(info) as stream:
                    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                        h.update(chunk)
                entry_hash.update(
                    f"{info.filename}\0{info.file_size}\0{h.hexdigest()}\n".encode()
                )
                if (info.filename.startswith(("assets/", "res/", "resources/"))
                        or info.filename.lower().endswith(
                            (".png", ".jpg", ".jpeg", ".webp", ".ttf", ".otf",
                             ".svg", ".mp3", ".wav", ".gif"))):
                    media_count += 1
    except (zipfile.BadZipFile, RuntimeError, OSError) as exc:
        raise AttestationError("Invalid APK ZIP") from exc
    try:
        result = subprocess.run(
            [str(packager), "dump", "badging", str(apk)],
            capture_output=True, text=True, check=True, timeout=45,
        )
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
        raise AttestationError("Android package identity inspection failed") from exc
    match = re.search(r"^package:\s+name='([^']+)'", result.stdout, re.MULTILINE)
    if not match or match.group(1) != EXPECTED_PACKAGE:
        raise AttestationError("Unexpected Android package ID; refuse artifact handoff")
    return {
        "filename": apk.name, "sha256": digest(apk),
        "size_bytes": apk.stat().st_size, "application_id": match.group(1),
        "apk_tested_on_device": False,
        "payload_structural_check": True,
        "dex_entries": dex,
        "native_runtime_entries": native,
        "managed_payload_entries": managed,
        "packaged_entry_count": len(infos),
        "packaged_entry_manifest_sha256": entry_hash.hexdigest(),
        "packaged_media_entry_count_unreviewed": media_count,
        "packaged_media_rights_verified": False,
    }


def attestation(root: Path, mobile: Path, packager: Path | None = None) -> dict:
    root, mobile = root.resolve(strict=True), mobile.resolve(strict=True)
    check_pins(root)
    project = root / PROJECT
    if not project.is_file() or project.is_symlink():
        raise AttestationError("Missing/symlinked pinned MAUI project")
    xml = ET.parse(project).getroot()
    props = {e.tag.rsplit("}", 1)[-1]: (e.text or "").strip()
             for e in xml.iter() if e.tag.rsplit("}", 1)[-1] in
             {"TargetFramework", "UseMaui", "ApplicationId", "RuntimeIdentifiers"}}
    if props.get("TargetFramework") != "net10.0-android" or props.get("UseMaui") != "true":
        raise AttestationError("Unexpected PKForge Android framework")
    # The conditional diagnostic property overrides the production ID at build time.
    if EXPECTED_PACKAGE not in project.read_text(encoding="utf-8"):
        raise AttestationError("Diagnostic application ID not declared")
    apks = sorted((root / "src/PKForge.App/bin/Debug/net10.0-android").rglob("*.apk"))
    if not apks:
        raise AttestationError("Android build produced no APK")
    packager = packager or find_android_packager()
    inspected = [inspect_apk(p, packager) for p in apks]
    assets = inventory_assets(root, project)
    scripts = sorted((mobile / "scripts").glob("pkforge_*.py"))
    if not scripts:
        raise AttestationError("Missing adaptation scripts")
    lockfile = root / "src/PKForge.App/obj/project.assets.json"
    if not lockfile.is_file():
        raise AttestationError("Missing NuGet restore graph")
    lock = json.loads(lockfile.read_text(encoding="utf-8"))
    return {
        "schema": 1,
        "purpose": "non-distributable diagnostic metadata",
        "redistribution_authorized": False,
        "save_writes_authorized": False,
        "device_certified": False,
        "desktop_fixture_parity_certified": False,
        "pins": {
            "pkforge": PKFORGE, "fork_core": FORK, "plugins": PLUGINS,
            "pkhex_maui_reference": MAUI, "official_core_source": CORE_SOURCE,
            "official_core_package": CORE_PACKAGE,
            "mobile_head": git_head(mobile),
        },
        "project": {"path": PROJECT, "sha256": digest(project),
                    "target_framework": props["TargetFramework"]},
        "adaptations": {p.name: digest(p) for p in scripts},
        "nuget": {
            "restore_graph_sha256": digest(lockfile),
            "libraries": sorted(lock.get("libraries", {}).keys()),
            "transitive_licences_verified": False,
        },
        "apks": inspected,
        "assets": assets,
        "rights_review_complete": False,
        "gate": "metadata-only; do not upload APK or advertise an installable editor",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--mobile", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = attestation(args.repo, args.mobile)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {len(report['apks'])} diagnostic APK(s), "
          f"{len(report['assets'])} asset entries; metadata only, distribution prohibited.")
    for item in report["apks"]:
        print(f"{item['filename']} sha256={item['sha256']} package={item['application_id']}")


if __name__ == "__main__":
    main()
