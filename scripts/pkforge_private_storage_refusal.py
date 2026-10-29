#!/usr/bin/env python3
"""Prevent adapted PKForge diagnostic previews from persisting bank or backup bytes."""
import argparse
from pathlib import Path
from scripts.pkforge_offline_preview import checked_file, verify_pins, OfflineRefusal

INF = "src/PKForge.Infrastructure/"
PROJECT = INF + "PKForge.Infrastructure.csproj"
BANK = INF + "FileBankService.cs"
BACKUP = INF + "FileBackupService.cs"
PROJECT_ANCHOR = "  </PropertyGroup>\n  <ItemGroup>"
PROJECT_REPLACE = """  </PropertyGroup>
  <PropertyGroup Condition="'$(DiagnosticBuild)' == 'true'">
    <DefineConstants>$(DefineConstants);DIAGNOSTIC</DefineConstants>
  </PropertyGroup>
  <ItemGroup>"""
BANK_ANCHOR = """    private void EnsureWritable()
    {
        if (_indexUnreadable)
            throw new InvalidOperationException("The bank index could not be read, so the bank is read-only to protect it. Check the bank folder's index.json.");
    }"""
BANK_REPLACE = """    private void EnsureWritable()
    {
#if DIAGNOSTIC
        throw new InvalidOperationException("Diagnostic preview: bank persistence disabled.");
#else
        if (_indexUnreadable)
            throw new InvalidOperationException("The bank index could not be read, so the bank is read-only to protect it. Check the bank folder's index.json.");
#endif
    }"""
BACKUP_START = "    {\n        ArgumentNullException.ThrowIfNull(source);\n"
BACKUP_END = "        return new BackupReceipt(id, createdUtc, sha);\n    }\n"
BACKUP_START_REPLACE = """    {
#if DIAGNOSTIC
        await Task.Yield();
        throw new InvalidOperationException("Diagnostic preview: raw-save backup persistence disabled.");
#else
        ArgumentNullException.ThrowIfNull(source);
"""
BACKUP_END_REPLACE = """        return new BackupReceipt(id, createdUtc, sha);
#endif
    }
"""

def once(source, before, after):
    if source.count(before) != 1 or after in source:
        raise OfflineRefusal("Pinned private-storage source drift or repeat adaptation")
    return source.replace(before, after, 1)

def transform(root):
    root = root.resolve(strict=True)
    sources = {p: checked_file(root, p).read_text(encoding="utf-8")
               for p in (PROJECT, BANK, BACKUP)}
    return {
        root / PROJECT: once(sources[PROJECT], PROJECT_ANCHOR, PROJECT_REPLACE),
        root / BANK: once(sources[BANK], BANK_ANCHOR, BANK_REPLACE),
        root / BACKUP: once(once(sources[BACKUP], BACKUP_START, BACKUP_START_REPLACE),
                            BACKUP_END, BACKUP_END_REPLACE),
    }

def prepare(root):
    verify_pins(root)
    for path, content in transform(root).items():
        path.write_text(content, encoding="utf-8")

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--repo", type=Path, required=True)
    prepare(p.parse_args().repo)
    print("Diagnostic bank and raw-save backup persistence refused")
