#!/usr/bin/env python3
"""Fail-closed offline diagnostic adaptation for pinned PKForge MAUI.

Only the temporary pinned upstream checkout is modified. This does not clear
bundled media rights, certify device behavior, or enable save writes.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import subprocess

from scripts.pkforge_artifact_attestation import find_android_packager

PKFORGE = "2df04430e532b78c247f45b936f3068d8f824987"
FORK = "91e9c88667a6a191a000db68e2db19505eb87561"
PLUGINS = "23ab1fc73f7be6ad427fbc900f9b99111cd2d560"
APP = "src/PKForge.App/"
POLICY = APP + "Services/PreviewRuntimePolicy.cs"

class OfflineRefusal(ValueError):
    pass

# Each anchor is an exact pinned method opening, not a fuzzy source search.
# The policy is a non-constant property to avoid unreachable-code warnings.
PATCHES = {
    APP + "Services/SpriteService.cs": [
        ("public SKBitmap? GetHome(SpriteLook look)\n    {", "if (PreviewRuntimePolicy.Offline) return null;"),
        ("public bool HomeUnavailable(SpriteLook look)\n    {", "if (PreviewRuntimePolicy.Offline) return true;"),
        ("public void WarmHome(SpriteLook look, Action onLoaded)\n    {", "if (PreviewRuntimePolicy.Offline) return;"),
        ("public bool TryGetShowdown(SpriteLook look, out AnimatedSprite? sprite)\n    {",
         "if (PreviewRuntimePolicy.Offline) { sprite = null; return true; }"),
        ("public void WarmShowdown(SpriteLook look, Action onLoaded)\n    {", "if (PreviewRuntimePolicy.Offline) return;"),
        ("public async Task<bool> DownloadPackFileAsync(SpritePack.Entry entry, CancellationToken cancellationToken)\n    {",
         "if (PreviewRuntimePolicy.Offline) return false;"),
        ("private static async Task DownloadToDiskAsync(string url, string diskPath, CancellationToken cancellationToken)\n    {",
         'if (PreviewRuntimePolicy.Offline) throw new InvalidOperationException("Offline diagnostic refuses sprite downloads.");'),
    ],
    APP + "Services/ItemArt.cs": [
        ("public static bool IsCachedOrKnownMissing(string itemName)\n    {", "if (PreviewRuntimePolicy.Offline) return true;"),
        ("public static async Task<string?> GetAsync(string itemName)\n    {", "if (PreviewRuntimePolicy.Offline) return null;"),
    ],
    APP + "Services/SpritePackDownloader.cs": [
        ("public int CountMissing()\n    {", "if (PreviewRuntimePolicy.Offline) return 0;"),
        ("public async Task RunAsync(Action<string, double> onProgress, CancellationToken cancellationToken)\n    {",
         "if (PreviewRuntimePolicy.Offline) return;"),
        ("public async Task FetchAddOnsQuietlyAsync()\n    {", "if (PreviewRuntimePolicy.Offline) return;"),
        ("private static async Task DownloadArchiveAsync(string partial, Archive pack, Action<string, double> onProgress, CancellationToken cancellationToken)\n    {",
         'if (PreviewRuntimePolicy.Offline) throw new InvalidOperationException("Offline diagnostic refuses sprite archives.");'),
    ],
    APP + "Services/PokeparkSpriteService.cs": [
        ("public ParkSpriteFrame? GetFrame(int species, int form, bool shiny, ParkPose pose, int direction, long elapsedMs)\n    {",
         "if (PreviewRuntimePolicy.Offline) return null;"),
        ('public SKBitmap? GetPortrait(int species, int form, bool shiny, string emotion = "Normal")\n    {',
         "if (PreviewRuntimePolicy.Offline) return null;"),
        ("public void Warm(int species, int form, bool shiny, Action onLoaded)\n    {", "if (PreviewRuntimePolicy.Offline) return;"),
        ("public async Task<IReadOnlyDictionary<string, string>> CreditNamesAsync()\n    {",
         "if (PreviewRuntimePolicy.Offline) return new Dictionary<string, string>();"),
        ("private static async Task<byte[]> ReadAsync(string remote, string directory, string file, int limit)\n    {",
         'if (PreviewRuntimePolicy.Offline) throw new InvalidOperationException("Offline diagnostic refuses PMD downloads.");'),
    ],
    APP + "Services/AppUpdateService.cs": [
        ("public async Task<AppUpdateCheck> CheckAsync(CancellationToken cancellationToken = default)\n    {",
         'if (PreviewRuntimePolicy.Offline) return new AppUpdateCheck(false, null, "Offline diagnostics do not check for updates.");'),
        ("public async Task<AppUpdateInstallResult> DownloadAndInstallAsync(\n        AvailableAppUpdate update,\n        Action<long, long> onProgress,\n        CancellationToken cancellationToken)\n    {",
         "if (PreviewRuntimePolicy.Offline) return AppUpdateInstallResult.InstallPermissionRequired;"),
        ("public static void OpenInstallPermissionSettings()\n    {",
         "if (PreviewRuntimePolicy.Offline) return;"),
        ("internal static void StartAndroidInstall(string apkPath, bool askUser = false)\n    {",
         'if (PreviewRuntimePolicy.Offline) throw new InvalidOperationException("Offline diagnostic refuses APK installation.");'),
        ("public override void OnReceive(Android.Content.Context? context, Android.Content.Intent? intent)\n    {",
         "if (PreviewRuntimePolicy.Offline) return;"),
    ],
}
REPLACEMENTS = {
    APP + "Services/AppUpdateService.cs": [
        ("#if ANDROID\n[assembly: UsesPermission(Android.Manifest.Permission.RequestInstallPackages)]",
         "#if ANDROID && !DIAGNOSTIC\n[assembly: UsesPermission(Android.Manifest.Permission.RequestInstallPackages)]"),
        ("public static bool CanInstallUpdates() => CanRequestInstalls();",
         "public static bool CanInstallUpdates() => !PreviewRuntimePolicy.Offline && CanRequestInstalls();"),
    ],
}
MANIFEST = APP + "Platforms/Android/AndroidManifest.xml"
MANIFEST_OPEN = '<manifest xmlns:android="http://schemas.android.com/apk/res/android" android:installLocation="auto">'
MANIFEST_REPLACE = '''<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.android.com/tools" android:installLocation="auto">
  <!-- Remove merged permissions even when transitive MAUI assemblies request them. -->
  <uses-permission android:name="android.permission.INTERNET" tools:node="remove" />
  <uses-permission android:name="android.permission.REQUEST_INSTALL_PACKAGES" tools:node="remove" />
  <uses-permission android:name="android.permission.UPDATE_PACKAGES_WITHOUT_USER_ACTION" tools:node="remove" />'''
POLICY_SOURCE = """// Diagnostic-only offline containment for the adapted, read-only PKForge editor.
// This property is deliberately non-constant so all branches compile cleanly.
namespace PKForge.App.Services;

internal static class PreviewRuntimePolicy
{
    internal static bool Offline
    {
        get
        {
#if DIAGNOSTIC
            return true;
#else
            return false;
#endif
        }
    }
}
"""

def head(root: Path, relative: str = ".") -> str:
    return subprocess.check_output(
        ["git", "-C", str(root / relative), "rev-parse", "HEAD"],
        text=True, stderr=subprocess.PIPE,
    ).strip()

def verify_pins(root: Path) -> None:
    if (head(root), head(root, "external/PKHeX"),
        head(root, "external/PKHeX-Plugins")) != (PKFORGE, FORK, PLUGINS):
        raise OfflineRefusal("Pinned PKForge, Core fork or plugin revision changed")

def checked_file(root: Path, relative: str) -> Path:
    path = root / relative
    if path.is_symlink() or not path.is_file() or not path.resolve().is_relative_to(root.resolve()):
        raise OfflineRefusal("Missing, symlinked or escaping pinned source: " + relative)
    return path

def transform_sources(root: Path) -> dict[Path, str]:
    """Validate every anchor before writing any source or creating the policy."""
    root = root.resolve(strict=True)
    policy = root / POLICY
    if policy.exists() or policy.is_symlink():
        raise OfflineRefusal("Offline runtime policy already exists; refusing repeated patch")
    staged = {}
    for relative in sorted(set(PATCHES) | set(REPLACEMENTS)):
        path = checked_file(root, relative)
        source = path.read_text(encoding="utf-8")
        for anchor, guard in PATCHES.get(relative, []):
            if source.count(anchor) != 1 or source.count(guard) != 0:
                raise OfflineRefusal("Pinned method changed or already adapted: " + relative + " " + anchor[:60])
            source = source.replace(anchor, anchor + "\n        " + guard, 1)
        for before, after in REPLACEMENTS.get(relative, []):
            if source.count(before) != 1 or after in source:
                raise OfflineRefusal("Pinned permission/installer surface changed: " + relative)
            source = source.replace(before, after, 1)
        staged[path] = source
    manifest = checked_file(root, MANIFEST)
    original = manifest.read_text(encoding="utf-8")
    if original.count(MANIFEST_OPEN) != 1 or "tools:node" in original:
        raise OfflineRefusal("Pinned Android manifest changed")
    staged[manifest] = original.replace(MANIFEST_OPEN, MANIFEST_REPLACE, 1)
    staged[policy] = POLICY_SOURCE
    return staged

def prepare(root: Path) -> None:
    verify_pins(root)
    staged = transform_sources(root)
    for path, source in staged.items():
        path.write_text(source, encoding="utf-8")

FORBIDDEN_PERMISSIONS = (
    "android.permission.INTERNET",
    "android.permission.REQUEST_INSTALL_PACKAGES",
    "android.permission.UPDATE_PACKAGES_WITHOUT_USER_ACTION",
)

def verify_apk_permissions(root: Path) -> list[str]:
    """Check actual built APK manifests, not only source manifest declarations."""
    verify_pins(root)
    folder = root / "src/PKForge.App/bin/Debug/net10.0-android"
    apks = sorted(folder.rglob("*.apk"))
    if not apks:
        raise OfflineRefusal("No diagnostic APKs available for permission verification")
    packager = find_android_packager()
    inspected = []
    for apk in apks:
        if apk.is_symlink() or not apk.is_file() or not apk.resolve().is_relative_to(root.resolve()):
            raise OfflineRefusal("Untrusted APK path")
        try:
            output = subprocess.check_output(
                [str(packager), "dump", "permissions", str(apk)],
                text=True, stderr=subprocess.PIPE,
            )
        except (OSError, subprocess.CalledProcessError) as exc:
            raise OfflineRefusal("Cannot inspect compiled Android permissions") from exc
        if "package:" not in output.lower():
            raise OfflineRefusal("Android permissions dump missing package identity")
        if any(permission in output for permission in FORBIDDEN_PERMISSIONS):
            raise OfflineRefusal("Offline APK retains network or installer permission: " + str(apk))
        inspected.append(str(apk))
    return inspected

def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--verify-apks", action="store_true")
    args = parser.parse_args()
    if args.verify_apks:
        print("Offline APK permissions verified:", *verify_apk_permissions(args.repo), sep="\n")
    else:
        prepare(args.repo)

if __name__ == "__main__":
    main()
