"""Pinned-source regression tests for preview bank/backup persistence refusal."""
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

    def test_fail_closed_on_drift_before_writes(self):
        p = self.root / guard.BANK
        p.write_text(p.read_text().replace("EnsureWritable", "Unknown"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertNotIn("DIAGNOSTIC", (self.root / guard.PROJECT).read_text())

    def test_refuse_repeat_patch(self):
        out = guard.transform(self.root)
        for p, content in out.items():
            p.write_text(content)
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)

if __name__ == "__main__":
    unittest.main()
