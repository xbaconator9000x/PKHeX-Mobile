"""Pinned-source regression tests for preview private-data containment."""
import tempfile
import unittest
from pathlib import Path
from scripts import pkforge_private_storage_refusal as guard

class PrivateStorageTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        fixtures = {
            guard.PROJECT: "<Project>\n" + guard.PROJECT_ANCHOR + "\n</Project>\n",
            guard.BANK: "class Bank {\n" + guard.BANK_ANCHOR + "\n}\n",
            guard.BACKUP: ("class Backup {\npublic async Task CreateAsync()\n" +
                           guard.BACKUP_START + "        // do not write in preview\n" +
                           guard.BACKUP_END + "}\n"),
            guard.MAUI: "class Maui {\n" + guard.GIFT_REG + "\n" + guard.IDENTITY_REG + "\n}\n",
            guard.HOME: ("class Home {\n" + guard.LOG_MENU + guard.LOG_CASE +
                         guard.LOG_START + "    private async Task ShareLogsAsync()\n" +
                         "    { AppLog.BuildShareArchive(null); await Share.Default.RequestAsync(null); }\n" +
                         guard.LOG_END + "\n" + guard.CRASH_START +
                         "    private async Task OfferCrashReportAsync()\n" +
                         "    { /* Share report */ await ShareLogsAsync(); }\n" +
                         guard.CRASH_END + "    private async Task DownloadSpritePackAsync() {}\n}\n"),
        }
        for name, content in fixtures.items():
            p = self.root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(content)

    def test_compile_time_diagnostic_guards(self):
        out = guard.transform(self.root)
        self.assertIn("DIAGNOSTIC", out[self.root / guard.PROJECT])
        self.assertIn("#if DIAGNOSTIC", out[self.root / guard.BANK])
        self.assertIn("bank persistence disabled", out[self.root / guard.BANK])
        self.assertIn("raw-save backup persistence disabled", out[self.root / guard.BACKUP])
        self.assertIn("#else", out[self.root / guard.BACKUP])
        maui = out[self.root / guard.MAUI]
        self.assertIn("new InjectedGiftHistory(null)", maui)
        self.assertIn("new JsonSaveIdentityStore(null)", maui)
        self.assertNotIn("injected-gifts.json", maui)
        self.assertNotIn("save-identities.json", maui)
        home = out[self.root / guard.HOME]
        self.assertNotIn("AppLog.BuildShareArchive", home)
        self.assertNotIn("Share.Default.RequestAsync", home)
        self.assertNotIn("new PadOption(\"Share logs\"", home)
        self.assertNotIn("case \"Share logs\"", home)
        self.assertNotIn("Share report", home)
        self.assertIn("log sharing disabled", home)
        self.assertIn("private Task OfferCrashReportAsync()", home)

    def test_fail_closed_on_drift_before_writes(self):
        p = self.root / guard.BANK
        p.write_text(p.read_text().replace("EnsureWritable", "Unknown"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertNotIn("DIAGNOSTIC", (self.root / guard.PROJECT).read_text())

    def test_fail_closed_on_identity_registration_drift(self):
        p = self.root / guard.MAUI
        p.write_text(p.read_text().replace("save-identities.json", "other-identity-file.json"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertIn("injected-gifts.json", p.read_text())

    def test_fail_closed_on_log_share_method_drift(self):
        p = self.root / guard.HOME
        p.write_text(p.read_text().replace("AppLog.BuildShareArchive", "DifferentArchive"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertIn("Share.Default.RequestAsync", p.read_text())

    def test_refuse_repeat_patch(self):
        out = guard.transform(self.root)
        for p, content in out.items():
            p.write_text(content)
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)

if __name__ == "__main__":
    unittest.main()
