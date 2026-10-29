#!/usr/bin/env python3
"""Contain diagnostic PKForge private storage, metadata and log-sharing routes."""
import argparse
from pathlib import Path
from scripts.pkforge_offline_preview import checked_file, verify_pins, OfflineRefusal

INF = "src/PKForge.Infrastructure/"
PROJECT = INF + "PKForge.Infrastructure.csproj"
BANK = INF + "FileBankService.cs"
BACKUP = INF + "FileBackupService.cs"
MAUI = "src/PKForge.App/MauiProgram.cs"
HOME = "src/PKForge.App/Views/HomePage.cs"
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

GIFT_REG = '''        builder.Services.AddSingleton<InjectedGiftHistory>(_ =>
            new InjectedGiftHistory(Path.Combine(FileSystem.AppDataDirectory, "injected-gifts.json")));'''
IDENTITY_REG = '''        builder.Services.AddSingleton<ISaveIdentityStore>(_ =>
            new JsonSaveIdentityStore(Path.Combine(FileSystem.AppDataDirectory, "save-identities.json")));'''
LOG_MENU = '''            new PadOption("Share logs", IconPath: "export", Detail: "Crash reports and recent activity, to send us when something goes wrong."),
'''
LOG_CASE = '''            case "Share logs":
                await ShareLogsAsync();
                break;
'''
LOG_START = '''    /// <summary>
    /// Zips the logs, crash reports, device details and scan report, then opens the share
'''
LOG_END = "    private static bool _crashOffered;"
CRASH_START = "    /// <summary>After a crash, offers the report once on the next launch.</summary>\n"
CRASH_END = "    /// <summary>Downloads every species' animated + HOME sprites for full offline use.</summary>\n"


def replace_section(source, start, end, expected, replacement):
    if source.count(start) != 1 or source.count(end) != 1:
        raise OfflineRefusal("Pinned diagnostic privacy method boundary changed")
    first, last = source.index(start), source.index(end)
    if first >= last or any(token not in source[first:last] for token in expected):
        raise OfflineRefusal("Pinned diagnostic privacy method body changed")
    return source[:first] + replacement + source[last:]


def adapt_maui(source):
    source = once(source, GIFT_REG, "        builder.Services.AddSingleton<InjectedGiftHistory>(_ => new InjectedGiftHistory(null));")
    return once(source, IDENTITY_REG, "        builder.Services.AddSingleton<ISaveIdentityStore>(_ => new JsonSaveIdentityStore(null));")


def adapt_home(source):
    source = once(source, LOG_MENU, "")
    source = once(source, LOG_CASE, "")
    source = replace_section(source, LOG_START, LOG_END,
        ("AppLog.BuildShareArchive", "Share.Default.RequestAsync", "private async Task ShareLogsAsync"),
        '''    // Diagnostic preview never archives or shares logs, scan results or file names.
    private Task ShareLogsAsync()
    {
        _viewModel.Status = "Diagnostic preview: log sharing disabled.";
        return Task.CompletedTask;
    }

''')
    source = replace_section(source, CRASH_START, CRASH_END,
        ("private async Task OfferCrashReportAsync()", "Share report", "ShareLogsAsync()"),
        '''    // A crash report may contain private paths; no automatic sharing prompt.
    private Task OfferCrashReportAsync() => Task.CompletedTask;

''')
    return source


def transform(root):
    root = root.resolve(strict=True)
    sources = {p: checked_file(root, p).read_text(encoding="utf-8")
               for p in (PROJECT, BANK, BACKUP, MAUI, HOME)}
    return {
        root / PROJECT: once(sources[PROJECT], PROJECT_ANCHOR, PROJECT_REPLACE),
        root / BANK: once(sources[BANK], BANK_ANCHOR, BANK_REPLACE),
        root / BACKUP: once(once(sources[BACKUP], BACKUP_START, BACKUP_START_REPLACE),
                            BACKUP_END, BACKUP_END_REPLACE),
        root / MAUI: adapt_maui(sources[MAUI]),
        root / HOME: adapt_home(sources[HOME]),
    }

def prepare(root):
    verify_pins(root)
    for path, content in transform(root).items():
        path.write_text(content, encoding="utf-8")

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--repo", type=Path, required=True)
    prepare(p.parse_args().repo)
    print("Diagnostic bank, backup, metadata persistence and log sharing refused")
