"""Retail-only official-Core source isolation contract tests.

These tests are synthetic transform tests, not a successful Core/Android build,
save parser certification, or permission to write user saves.
"""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from scripts.pkforge_official_core_probe import ProbeRefusal
from scripts.pkforge_retail_only_probe import (
    FILES, FORBIDDEN, adapt_sources, prepare_retail,
    retail_parser, retail_engine, retail_session, retail_legalizer, retail_lumi,
)


def sources():
    return dict(zip(FILES, (
        """internal static class SaveParser
{
    private const int LuminescentV13Size = 0xEF0A4;
    internal static bool TryGetSaveFile(byte[] data, out SaveFile? save)
    {
        data = RetroArchSaveContainer.Decode(data);
        if (SaveUtil.TryGetSaveFile(data, out save))
            return true;
        var trimmed = SramPadding.Trim(data);
        if (trimmed.Length != data.Length)
        {
            data = trimmed;
            if (SaveUtil.TryGetSaveFile(data, out save))
                return true;
        }
        if (IsLuminescentPlatinum(data))
        {
            save = new SAV8BSLuminescent(data);
            return true;
        }
        save = null;
        return false;
    }
}""",
        """public ISaveEngineSession OpenSession(ReadOnlyMemory<byte> bytes, string? displayName, SaveFormat format)
    {
        if (format == SaveFormat.Auto) return OpenSession(bytes, displayName);
    }
    private static string FormatName(SaveFormat format) => format switch
    {
    };
    public SaveSnapshot Open(ReadOnlyMemory<byte> bytes, string? displayName = null)
    {
    }
    public ISaveEngineSession OpenSession(ReadOnlyMemory<byte> bytes, string? displayName = null)
    {
    }
    public SaveDescription? TryDescribe()
    {
        if (save is SAV9SV sv && CompassBlockKeys.IsCompassSave(sv)) return null;
    }
    public bool Validate()
    {
            if (SaveParser.IsPokemonUnbound(decoded)) return true;
            if (SaveParser.IsPokemonGsChronicles(decoded)) return true;
            if (SaveParser.IsPokemonRadicalRed(decoded)) return true;
    }""",
        """    private void SetDexFlagsCore()
    {
            case SAV8BSLuminescent lumi:
                lumi.LumiZukan.SetEntry(species, seen, caught);
                return;
    }
    // ── Pokémon Compass (S/V romhack v2.1.x) ──
    public bool SupportsCompassSettings => _save is SAV9SV sv && CompassBlockKeys.HasCompassSettingBlocks(sv);
    public IReadOnlyList<CompassSetting> GetCompassSettings() { return []; }
    public bool SetCompassSetting(string id, int choiceIndex) { return true; }
    private SaveSnapshot BuildSnapshot(string? displayName)
    {
    }""",
        """            var generationSave = save is SAV8BSLuminescent
                ? new SAV8BS(save.Data.ToArray())
                : save;
                if (save is SAV8BSLuminescent)
                    return generationSave.GetLegalFromSet(set);

    private static PKM ConvertForSave(SaveFile save, PKM created)
    {
        if (save is not SAV8BSLuminescent || created is PB8LUMI)
            return created;

        var data = new byte[created.SIZE_PARTY];
        created.WriteDecryptedDataParty(data);
        return new PB8LUMI(data);
    }""",
        """    public static bool IsLumi(SaveFile save) => save is SAV8BSLuminescent;
    public static bool IsLumi(PKM pk) => pk.Context == EntityContext.Gen8bLumi;
    public static IReadOnlyList<string> FormList(ushort species)
    {
        var count = species <= PersonalTable.BDSPLUMI.MaxSpeciesID ? PersonalTable.BDSPLUMI[species].FormCount : names.Count;
    }""",
    )))


class RetailOnlyProbeTests(unittest.TestCase):
    def test_all_five_rewritten_with_explicit_refusals(self):
        before = sources()
        after = adapt_sources(before)
        self.assertEqual(set(FILES), set(after))
        parser = after[FILES[0]]
        self.assertIn("RejectKnownHackLayout(data);", parser)
        self.assertEqual(2, parser.count("RejectCompass(save);"))
        self.assertIn("0xA9296A9C", parser)
        self.assertLess(parser.index("RejectKnownHackLayout(data);"), parser.index("SaveUtil.TryGetSaveFile"))
        self.assertIn("ROM-hack layout is unsupported", parser)
        engine = after[FILES[1]]
        self.assertEqual(3, engine.count("RejectKnownHackLayout(bytes.Span);"))
        self.assertEqual(3, engine.count("return false;"))
        session = after[FILES[2]]
        self.assertIn("SupportsCompassSettings => false;", session)
        self.assertIn("SetCompassSetting(string id, int choiceIndex) => false;", session)
        self.assertIn("var generationSave = save;", after[FILES[3]])
        self.assertIn("public static bool IsLumi(SaveFile save) => false;", after[FILES[4]])
        for output in after.values():
            for symbol in FORBIDDEN:
                self.assertNotIn(symbol, output)
        self.assertEqual(before, sources())  # caller inputs remain immutable

    def test_unexpected_upstream_changes_refuse(self):
        for i, name in enumerate(FILES):
            altered = sources()
            altered[name] = altered[name].replace(
                ("private const int LuminescentV13Size", "if (format == SaveFormat.Auto)",
                 "case SAV8BSLuminescent", "var generationSave", "PersonalTable.BDSPLUMI")[i],
                "UPSTREAM_CHANGED", 1)
            with self.subTest(path=name), self.assertRaises(ProbeRefusal):
                adapt_sources(altered)

    def test_double_adaptation_refuses(self):
        with self.assertRaises(ProbeRefusal):
            adapt_sources(adapt_sources(sources()))

    def test_no_partial_file_write_on_missing_anchor(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            before = sources()
            for name, content in before.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")
            bad = root / FILES[-1]
            bad.write_text("upstream changed", encoding="utf-8")
            with self.assertRaises(ProbeRefusal):
                prepare_retail(root)
            for name, content in before.items():
                if name != FILES[-1]:
                    self.assertEqual(content, (root / name).read_text(encoding="utf-8"))

    def test_symlink_refusal(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name, content in sources().items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")
            target = root / FILES[0]
            target.unlink()
            target.symlink_to(root / FILES[1])
            with self.assertRaises(ProbeRefusal):
                prepare_retail(root)


if __name__ == "__main__":
    unittest.main()
