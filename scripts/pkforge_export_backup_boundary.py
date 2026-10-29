#!/usr/bin/env python3
"""Remove unverified save sharing and cloud/device backup from pinned PKForge preview."""
import argparse
from pathlib import Path
from scripts.pkforge_offline_preview import checked_file, verify_pins, OfflineRefusal

BOX = "src/PKForge.App/Views/BoxBrowserPage.cs"
MANIFEST = "src/PKForge.App/Platforms/Android/AndroidManifest.xml"
XML = "src/PKForge.App/Platforms/Android/Resources/xml/pkhex_no_transfer.xml"
MENU = '            new PadOption("Export modified save", IconPath: "export", Detail: "Share a copy of the edited save file."),\n'
CASE = '            case "Export modified save": await ExportModifiedSaveAsync(session); return;\n'
BEGIN = '    /// <summary>Exports the live edited save through the Android share sheet.</summary>\n'
END = '    /// <summary>\n    /// Pokemon Compass romhack settings:'
RULES = '''<?xml version="1.0" encoding="utf-8"?>
<data-extraction-rules xmlns:android="http://schemas.android.com/apk/res/android">
  <cloud-backup><exclude domain="root" path="." /></cloud-backup>
  <device-transfer><exclude domain="root" path="." /></device-transfer>
</data-extraction-rules>
'''

def once(s, needle, replacement):
    if s.count(needle) != 1:
        raise OfflineRefusal("Pinned share/backup source anchor changed")
    return s.replace(needle, replacement, 1)

def adapt_box(s):
    s = once(once(s, MENU, ""), CASE, "")
    if s.count(BEGIN) != 1 or s.count(END) != 1 or s.index(END) <= s.index(BEGIN):
        raise OfflineRefusal("Export method boundary changed")
    start, end = s.index(BEGIN), s.index(END)
    old = s[start:end]
    for token in ("session.Serialize()", "File.WriteAllBytesAsync", "Share.Default.RequestAsync"):
        if token not in old:
            raise OfflineRefusal("Original export implementation changed: " + token)
    replacement = '''    // Diagnostic preview: no whole-save export through any caller.
    private Task ExportModifiedSaveAsync(Domain.ISaveEngineSession session)
    {
        _viewModel.Status = "Diagnostic preview: save export disabled.";
        return Task.CompletedTask;
    }

'''
    result = s[:start] + replacement + s[end:]
    if result.count("ExportModifiedSaveAsync(") != 1 or "Export modified save" in result:
        raise OfflineRefusal("Whole-save export remains reachable")
    return result

def adapt_manifest(s):
    return once(s, 'android:allowBackup="true"',
                'android:allowBackup="false" android:fullBackupContent="false" android:dataExtractionRules="@xml/pkhex_no_transfer"')

def transform(root):
    first = checked_file(root, BOX).read_text(encoding="utf-8")
    second = checked_file(root, MANIFEST).read_text(encoding="utf-8")
    rules = root / XML
    if rules.exists() or rules.is_symlink() or any(p.is_symlink() for p in rules.parents if p != root):
        raise OfflineRefusal("Untrusted or existing Android extraction rules")
    return {root / BOX: adapt_box(first), root / MANIFEST: adapt_manifest(second), rules: RULES}

def prepare(root):
    root = root.resolve(strict=True)
    verify_pins(root)
    staged = transform(root)
    for path, content in staged.items():
        if path.name == "pkhex_no_transfer.xml":
            path.parent.mkdir(parents=True, exist_ok=False)
        path.write_text(content, encoding="utf-8")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    prepare(parser.parse_args().repo)
    print("Diagnostic export and cloud/device transfer refused in adapted checkout")
