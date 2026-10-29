"""Regression checks for exact-source PKForge read-only DI containment."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal
from scripts.pkforge_readonly_containment import (
    ACCESS, ACCESS_SOURCE, PROGRAM, WRITER, WRITER_SOURCE, adapt_program, prepare,
)

PROGRAM_SOURCE = """using PKForge.Domain;
        builder.Services.AddSingleton<ISafeSaveWriter, SafeSaveWriter>();
#if ANDROID
        builder.Services.AddSingleton<ISaveFileAccess, AndroidSafFileAccess>();
#endif
"""


class ContainmentTests(unittest.TestCase):
    def test_both_write_routes_denied(self):
        adapted = adapt_program(PROGRAM_SOURCE)
        self.assertIn("AddSingleton<ISafeSaveWriter, ReadOnlySaveWriter>()", adapted)
        self.assertIn("new ReadOnlySaveFileAccess(new AndroidSafFileAccess())", adapted)
        self.assertNotIn("AddSingleton<ISafeSaveWriter, SafeSaveWriter>()", adapted)
        self.assertNotIn("AddSingleton<ISaveFileAccess, AndroidSafFileAccess>()", adapted)
        self.assertIn("ValueTask.FromException<SaveWriteReceipt>", WRITER_SOURCE)
        self.assertIn("ValueTask.FromException(new UnsafeSaveWriteException", ACCESS_SOURCE)
        self.assertNotIn("reader.WriteAtomicallyAsync", ACCESS_SOURCE)

    def test_missing_duplicate_or_repeated_anchor_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_program(PROGRAM_SOURCE.replace("SafeSaveWriter>();", "Changed>();"))
        with self.assertRaises(ProbeRefusal):
            adapt_program(PROGRAM_SOURCE + PROGRAM_SOURCE)
        with self.assertRaises(ProbeRefusal):
            adapt_program(adapt_program(PROGRAM_SOURCE))

    def test_preflight_and_no_partial_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            program = root / PROGRAM
            program.parent.mkdir(parents=True)
            (root / WRITER).parent.mkdir(parents=True)
            program.write_text(PROGRAM_SOURCE.replace("AndroidSafFileAccess", "Changed"))
            original = program.read_bytes()
            with patch("scripts.pkforge_readonly_containment.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            self.assertEqual(program.read_bytes(), original)
            self.assertFalse((root / WRITER).exists())
            self.assertFalse((root / ACCESS).exists())
            program.write_text(PROGRAM_SOURCE)
            with patch("scripts.pkforge_readonly_containment.sha", return_value=PKFORGE):
                result = prepare(root)
                self.assertFalse(result["writes_enabled"])
                self.assertFalse(result["save_as_enabled"])
                self.assertEqual((root / WRITER).read_text(), WRITER_SOURCE)
                self.assertEqual((root / ACCESS).read_text(), ACCESS_SOURCE)
                with self.assertRaises(ProbeRefusal):
                    prepare(root)

    def test_symlinked_or_unpinned_input_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            program = root / PROGRAM
            program.parent.mkdir(parents=True)
            (root / WRITER).parent.mkdir(parents=True)
            target = root / "real.cs"
            target.write_text(PROGRAM_SOURCE)
            program.symlink_to(target)
            with patch("scripts.pkforge_readonly_containment.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            with patch("scripts.pkforge_readonly_containment.sha", return_value="wrong"):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)


if __name__ == "__main__":
    unittest.main()
