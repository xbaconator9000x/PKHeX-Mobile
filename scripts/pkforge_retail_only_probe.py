#!/usr/bin/env python3
"""Isolate pinned PKForge's fork-only handlers for an official-Core compile probe.

This transforms ONLY a clean, temporary upstream checkout after the existing
SHA-checked official-Core substitution. It is not a production adapter and
does not authorize writes or redistribution. Unknown hack layouts still need
independent authenticated profile tests before any release.
"""
import argparse
import json
from pathlib import Path

from scripts.pkforge_official_core_probe import PLUGIN_PATH, PROJECTS, ProbeRefusal

ENGINE = "src/PKForge.Engine/"
FILES = (
    ENGINE + "SaveParser.cs",
    ENGINE + "SaveEngine.cs",
    ENGINE + "SaveEngineSession.cs",
    ENGINE + "LegalizerService.cs",
    ENGINE + "Luminescent/LumiData.cs",
)


def exactly(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise ProbeRefusal(f"{label}: expected one pinned source anchor, found {count}")
    return source.replace(old, new, 1)


def retail_parser(source: str) -> str:
    source = exactly(source, "    private const int LuminescentV13Size = 0xEF0A4;",
        """    private const int LuminescentV13Size = 0xEF0A4;

    // Retail-only diagnostic: these signatures must be rejected BEFORE SaveUtil,
    // which may otherwise accept a ROM-hack envelope as a retail save.
    private static void RejectKnownHackLayout(ReadOnlySpan<byte> data)
    {
        if (IsLuminescentPlatinum(data) || IsPokemonUnbound(data) ||
            IsPokemonGsChronicles(data) || IsPokemonRadicalRed(data))
            throw new InvalidDataException("Known ROM-hack layout is unsupported by official-Core retail mode.");
    }

    private static void RejectCompass(SaveFile? save)
    {
        // TrainerSeed block is present in all known Compass revisions, including
        // those before the settings blocks. Key from pinned fork source.
        if (save is SAV9SV sv && sv.Blocks.HasBlock(0xA9296A9C))
            throw new InvalidDataException("Compass layout is unsupported by official-Core retail mode.");
    }""", "parser guard declaration")
    source = exactly(source, "        data = RetroArchSaveContainer.Decode(data);",
        "        data = RetroArchSaveContainer.Decode(data);\n        RejectKnownHackLayout(data);",
        "initial layout check")
    source = exactly(source, "                data = trimmed;",
        "                data = trimmed;\n                RejectKnownHackLayout(data);",
        "trimmed layout check") if "                data = trimmed;" in source else exactly(
        source, "            data = trimmed;", "            data = trimmed;\n            RejectKnownHackLayout(data);", "trimmed layout check")
    source = exactly(source,
        "        if (SaveUtil.TryGetSaveFile(data, out save))\n            return true;",
        "        if (SaveUtil.TryGetSaveFile(data, out save))\n        {\n            RejectCompass(save);\n            return true;\n        }",
        "first SaveUtil guard")
    source = exactly(source,
        "            if (SaveUtil.TryGetSaveFile(data, out save))\n                return true;",
        "            if (SaveUtil.TryGetSaveFile(data, out save))\n            {\n                RejectCompass(save);\n                return true;\n            }",
        "trimmed SaveUtil guard")
    source = exactly(source,
        """        if (IsLuminescentPlatinum(data))
        {
            save = new SAV8BSLuminescent(data);
            return true;
        }""",
        """        if (IsLuminescentPlatinum(data))
            throw new InvalidDataException("Luminescent Platinum is unsupported in retail mode.");""",
        "Luminescent constructor")
    return source


def retail_engine(source: str) -> str:
    source = exactly(source,
        "        if (format == SaveFormat.Auto) return OpenSession(bytes, displayName);",
        "        RejectKnownHackLayout(bytes.Span);\n        if (format == SaveFormat.Auto) return OpenSession(bytes, displayName);",
        "explicit format entry")
    source = exactly(source,
        "    private static string FormatName(SaveFormat format) => format switch",
        """    private static void RejectKnownHackLayout(ReadOnlySpan<byte> bytes)
    {
        var raw = RetroArchSaveContainer.Decode(bytes);
        if (SaveParser.IsLuminescentPlatinum(raw) || SaveParser.IsPokemonUnbound(raw) ||
            SaveParser.IsPokemonGsChronicles(raw) || SaveParser.IsPokemonRadicalRed(raw))
            throw new InvalidDataException("ROM-hack layout cannot open as a retail save.");
    }

    private static string FormatName(SaveFormat format) => format switch""",
        "engine entry guard")
    source = exactly(source,
        "    public SaveSnapshot Open(ReadOnlyMemory<byte> bytes, string? displayName = null)\n    {",
        "    public SaveSnapshot Open(ReadOnlyMemory<byte> bytes, string? displayName = null)\n    {\n        RejectKnownHackLayout(bytes.Span);",
        "snapshot entry")
    source = exactly(source,
        "    public ISaveEngineSession OpenSession(ReadOnlyMemory<byte> bytes, string? displayName = null)\n    {",
        "    public ISaveEngineSession OpenSession(ReadOnlyMemory<byte> bytes, string? displayName = null)\n    {\n        RejectKnownHackLayout(bytes.Span);",
        "session entry")
    source = exactly(source, "CompassBlockKeys.IsCompassSave(sv)",
        "sv.Blocks.HasBlock(0xA9296A9C)", "Compass marker")
    for name in ("Unbound", "GsChronicles", "RadicalRed"):
        source = exactly(source,
            f"            if (SaveParser.IsPokemon{name}(decoded)) return true;",
            f"            if (SaveParser.IsPokemon{name}(decoded)) return false;",
            f"{name} validation")
    return source


def retail_session(source: str) -> str:
    source = exactly(source,
        """            case SAV8BSLuminescent lumi:
                lumi.LumiZukan.SetEntry(species, seen, caught);
                return;
""", "", "fork-only Zukan")
    begin = "    // ── Pokémon Compass (S/V romhack v2.1.x) ──"
    end = "    private SaveSnapshot BuildSnapshot(string? displayName)"
    if source.count(begin) != 1 or source.count(end) != 1:
        raise ProbeRefusal("Compass settings section changed")
    before, remainder = source.split(begin, 1)
    _, after = remainder.split(end, 1)
    source = before + """    // Fork-only Compass editor deliberately unavailable in retail mode.
    public bool SupportsCompassSettings => false;
    public IReadOnlyList<CompassSetting> GetCompassSettings() => [];
    public bool SetCompassSetting(string id, int choiceIndex) => false;

""" + end + after
    return source


def retail_legalizer(source: str) -> str:
    source = exactly(source,
        """            var generationSave = save is SAV8BSLuminescent
                ? new SAV8BS(save.Data.ToArray())
                : save;""",
        "            var generationSave = save;", "fork-specific generator")
    source = exactly(source,
        """                if (save is SAV8BSLuminescent)
                    return generationSave.GetLegalFromSet(set);

""", "", "fork-only legality route")
    source = exactly(source,
        """        if (save is not SAV8BSLuminescent || created is PB8LUMI)
            return created;

        var data = new byte[created.SIZE_PARTY];
        created.WriteDecryptedDataParty(data);
        return new PB8LUMI(data);""",
        "        return created;", "fork-only entity conversion")
    return source


def retail_lumi(source: str) -> str:
    source = exactly(source, "save is SAV8BSLuminescent", "false", "Lumi save type")
    source = exactly(source, "pk.Context == EntityContext.Gen8bLumi", "false", "Lumi entity context")
    source = exactly(source,
        "species <= PersonalTable.BDSPLUMI.MaxSpeciesID ? PersonalTable.BDSPLUMI[species].FormCount : names.Count",
        "names.Count", "Lumi personal table")
    return source


ADAPTERS = (retail_parser, retail_engine, retail_session, retail_legalizer, retail_lumi)
FORBIDDEN = ("SAV8BSLuminescent", "PB8LUMI", "EntityContext.Gen8bLumi",
             "PersonalTable.BDSPLUMI", "CompassBlockKeys")


def adapt_sources(sources: dict[str, str]) -> dict[str, str]:
    if set(sources) != set(FILES):
        raise ProbeRefusal("Expected exactly five pinned engine files")
    output = {}
    for path, adapter in zip(FILES, ADAPTERS):
        updated = adapter(sources[path])
        # Comments can mention fork types; executable references may not.
        for token in FORBIDDEN:
            if token in updated:
                raise ProbeRefusal(f"Fork-only symbol remains in {path}: {token}")
        output[path] = updated
    return output


def prepare_retail(root: Path) -> dict:
    root = root.resolve(strict=True)
    paths = [root / relative for relative in FILES]
    for path in paths:
        if path.is_symlink() or not path.is_file():
            raise ProbeRefusal(f"Missing/symlinked pinned source: {path}")
    originals = {relative: path.read_text(encoding="utf-8")
                 for relative, path in zip(FILES, paths)}
    changes = adapt_sources(originals)  # validate all before writing anything
    for relative, path in zip(FILES, paths):
        path.write_text(changes[relative], encoding="utf-8")
    return {"mode": "retail-only diagnostic", "changed_files": list(FILES),
            "official_core": "[26.10.18]", "writes_enabled": False,
            "hack_profiles_enabled": False, "desktop_parity_certified": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    print(json.dumps(prepare_retail(parser.parse_args().repo), indent=2))
