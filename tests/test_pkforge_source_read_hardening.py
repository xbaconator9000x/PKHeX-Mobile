"""Exact-source tests for bounded, two-open PKForge Android source reads."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal
from scripts.pkforge_source_read_hardening import (
    SAF, WRAPPER, OLD_SAF_READ, NEW_SAF_READ, OLD_WRAPPER_READ,
    adapt_saf, adapt_wrapper, prepare,
)

SAF_SOURCE = ("using PKForge.Domain;\n" + OLD_SAF_READ +
              '    public ValueTask WriteAtomicallyAsync() => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));\n')
WRAPPER_SOURCE = ("using PKForge.Domain;\n" + OLD_WRAPPER_READ +
                  '    public ValueTask WriteAtomicallyAsync() => ValueTask.FromException(new UnsafeSaveWriteException(ReadOnlySaveWriter.Reason));\n')


class BoundedSourceTests(unittest.TestCase):
    def test_android_reader_is_bounded_and_original_writer_stays_disabled(self):
        adapted = adapt_saf(SAF_SOURCE)
        self.assertIn("SaveInputSnapshot.ReadAsync(input, cancellationToken)", adapted)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested();", adapted)
        self.assertNotIn("CopyToAsync(buffer", adapted)
        self.assertIn("UnsafeSaveWriteException(ReadOnlySaveWriter.Reason)", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_saf(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_saf(SAF_SOURCE + OLD_SAF_READ)
        with self.assertRaises(ProbeRefusal):
            adapt_saf(SAF_SOURCE.replace("CopyToAsync(buffer, cancellationToken)",
                                         "CopyToAsync(buffer)"))
        with self.assertRaises(ProbeRefusal):
            adapt_saf(SAF_SOURCE.replace("ReadOnlySaveWriter.Reason", "unsafe"))

    def test_wrapper_reopens_twice_and_rejects_changed_bytes(self):
        adapted = adapt_wrapper(WRAPPER_SOURCE)
        self.assertEqual(adapted.count("reader.ReadAsync(documentId, cancellationToken)"), 2)
        self.assertIn("SaveInputSnapshot.FromBytes(first.Span)", adapted)
        self.assertIn("snapshot.Matches(second.Span)", adapted)
        self.assertIn("return snapshot.CopyBytes()", adapted)
        self.assertIn("UnsafeSaveWriteException(ReadOnlySaveWriter.Reason)", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_wrapper(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_wrapper(WRAPPER_SOURCE.replace("reader.ReadAsync", "reader.OpenAsync"))
        with self.assertRaises(ProbeRefusal):
            adapt_wrapper(WRAPPER_SOURCE.replace("ReadOnlySaveWriter.Reason", "unsafe"))

    def test_preflight_prevents_partial_write_on_changed_input(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for relative, content in ((SAF, SAF_SOURCE), (WRAPPER, WRAPPER_SOURCE)):
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(content, encoding="utf-8")
            saf = root / SAF
            before = saf.read_bytes()
            wrapper = root / WRAPPER
            wrapper.write_text(WRAPPER_SOURCE.replace("reader.ReadAsync", "reader.OpenAsync"))
            with patch("scripts.pkforge_source_read_hardening.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            self.assertEqual(saf.read_bytes(), before)
            wrapper.write_text(WRAPPER_SOURCE)
            with patch("scripts.pkforge_source_read_hardening.sha", return_value=PKFORGE):
                verdict = prepare(root)
                self.assertEqual(verdict["independent_source_reads"], 2)
                self.assertFalse(verdict["writes_enabled"])
                self.assertFalse(verdict["source_identity_authenticated"])
                self.assertIn(NEW_SAF_READ, saf.read_text())
                with self.assertRaises(ProbeRefusal):
                    prepare(root)

    def test_unpinned_or_symlinked_source_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for relative, content in ((SAF, SAF_SOURCE), (WRAPPER, WRAPPER_SOURCE)):
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(content)
            with patch("scripts.pkforge_source_read_hardening.sha", return_value="wrong"):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            target = root / SAF
            original = root / "actual.cs"
            original.write_text(SAF_SOURCE)
            target.unlink()
            target.symlink_to(original)
            with patch("scripts.pkforge_source_read_hardening.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)


if __name__ == "__main__":
    unittest.main()
