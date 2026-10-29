#!/usr/bin/env python3
"""Contain diagnostic PKForge private storage, metadata and log-sharing routes."""
import argparse
from pathlib import Path
from scripts.pkforge_offline_preview import checked_file, verify_pins, OfflineRefusal
from scripts import pkforge_residual_cache_refusal as residual

INF = "src/PKForge.Infrastructure/"
PROJECT = INF + "PKForge.Infrastructure.csproj"
BANK = INF + "FileBankService.cs"
BACKUP = INF + "FileBackupService.cs"
MAUI = "src/PKForge.App/MauiProgram.cs"
HOME = "src/PKForge.App/Views/HomePage.cs"
BOX = "src/PKForge.App/Views/BoxBrowserPage.cs"
BANK_PAGE = "src/PKForge.App/Views/BankPage.cs"
EVENT_GALLERY = "src/PKForge.App/Views/EventGallery.cs"
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


def remove_once(source, needle):
    if source.count(needle) != 1:
        raise OfflineRefusal("Pinned diagnostic privacy menu source drift or repeat adaptation")
    return source.replace(needle, "", 1)


def adapt_home(source):
    source = remove_once(source, LOG_MENU)
    source = remove_once(source, LOG_CASE)
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


BOX_BULK_EXPORT = '''            case "Export (.pk files)":
            {
'''
BOX_SLOT_EXPORT = '''    private async Task ExportSlotAsync(int slot)
    {
'''
BANK_ENTITY_EXPORT = '''    private async Task ExportAsync(BankEntry entry)
    {
'''
EVENT_ENTITY_EXPORT = '''        private async Task ExportAsync(bool entity)
        {
            if (_busy) return;
'''
BOX_BULK_GUARD = '''                if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
                {
                    _viewModel.Status = "Diagnostic preview: Pokémon export disabled.";
                    break;
                }
'''
BOX_SLOT_GUARD = '''        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
        {
            _viewModel.Status = "Diagnostic preview: Pokémon export disabled.";
            return;
        }
'''
BANK_ENTITY_GUARD = '''        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
        {
            _boxViewModel.Status = "Diagnostic preview: bank export disabled.";
            return;
        }
'''
EVENT_ENTITY_GUARD = '''            if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
            {
                _note.Text = "Diagnostic preview: event export disabled.";
                return;
            }
'''


def guard_export(source, anchor, guard, required):
    if source.count(anchor) != 1 or anchor + guard in source:
        raise OfflineRefusal("Pinned diagnostic export source drift or repeat adaptation")
    if any(source.count(token) < 1 for token in required):
        raise OfflineRefusal("Pinned diagnostic export body changed")
    return source.replace(anchor, anchor + guard, 1)


def adapt_box_exports(source):
    source = guard_export(source, BOX_BULK_EXPORT, BOX_BULK_GUARD,
                          ("_viewModel.BulkExport(directory)", "ShareMultipleFilesRequest"))
    return guard_export(source, BOX_SLOT_EXPORT, BOX_SLOT_GUARD,
                        ("session.ExportSlot(_viewModel.BoxIndex, slot)", "File.WriteAllBytesAsync(path, export.Data)"))


def adapt_bank_export(source):
    return guard_export(source, BANK_ENTITY_EXPORT, BANK_ENTITY_GUARD,
                        ("_bank.GetData(entry.Id)", "File.WriteAllBytesAsync(path, bytes)"))


def adapt_event_export(source):
    return guard_export(source, EVENT_ENTITY_EXPORT, EVENT_ENTITY_GUARD,
                        ("_ctx.Service.ExportEntity", "_ctx.Service.ExportCard",
                         "File.WriteAllBytesAsync(path, export.Data)"))


def transform(root):
    root = root.resolve(strict=True)
    sources = {p: checked_file(root, p).read_text(encoding="utf-8")
               for p in (PROJECT, BANK, BACKUP, MAUI, HOME, BOX, BANK_PAGE, EVENT_GALLERY)}
    staged = {
        root / PROJECT: once(sources[PROJECT], PROJECT_ANCHOR, PROJECT_REPLACE),
        root / BANK: once(sources[BANK], BANK_ANCHOR, BANK_REPLACE),
        root / BACKUP: once(once(sources[BACKUP], BACKUP_START, BACKUP_START_REPLACE),
                            BACKUP_END, BACKUP_END_REPLACE),
        root / MAUI: adapt_maui(sources[MAUI]),
        root / HOME: adapt_home(sources[HOME]),
        root / BOX: adapt_box_exports(sources[BOX]),
        root / BANK_PAGE: adapt_bank_export(sources[BANK_PAGE]),
        root / EVENT_GALLERY: adapt_event_export(sources[EVENT_GALLERY]),
    }
    return residual.extend(root, staged)

def prepare(root):
    verify_pins(root)
    for path, content in transform(root).items():
        path.write_text(content, encoding="utf-8")

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--repo", type=Path, required=True)
    prepare(p.parse_args().repo)
    print("Diagnostic bank, backup, metadata, log and entity-cache export routes refused")
