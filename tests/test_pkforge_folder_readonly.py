"""Pinned PKForge folder picker, provider and DI containment regression tests."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal
from scripts.pkforge_folder_readonly import (
    ACCESS, OLD_HELPERS, OLD_READ, OLD_WRITE, PICKER, OLD_PICK, OLD_RESULT,
    PROGRAM, WRAPPER, WRAPPER_SOURCE, adapt_access, adapt_picker, adapt_program, prepare,
)

PROGRAM_SOURCE = """        builder.Services.AddSingleton<ISafeSaveWriter, ReadOnlySaveWriter>();
        builder.Services.AddSingleton<ISaveFileAccess>(_ => new ReadOnlySaveFileAccess(new AndroidSafFileAccess()));
        builder.Services.AddSingleton<IFolderFileAccess, AndroidFolderFileAccess>();
"""
PICKER_SOURCE = OLD_PICK + """    public bool HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        var pending = _pending;
        _pending = null;
""" + OLD_RESULT + """
        {
            return true;
        }
        AndroidSafFileAccess.PersistPermission(uri, data);
        return true;
    }
"""
ACCESS_SOURCE = ("using PKForge.Domain;\n" + OLD_READ + OLD_WRITE + OLD_HELPERS
                 + "    public ValueTask<IReadOnlyList<PickedDocument>> ListFilesAsync() => default;\n")


class FolderReadOnlyTests(unittest.TestCase):
    def test_folder_picker_has_no_write_grants_and_is_cancel_safe(self):
        adapted = adapt_picker(PICKER_SOURCE)
        self.assertNotIn("GrantWriteUriPermission", adapted)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested()", adapted)
        self.assertIn("pending.Task.IsCanceled", adapted)
        self.assertIn("if (pending.Task.IsCanceled)\n            return true;", adapted)
        self.assertIn("return new ValueTask<PickedFolder?>(pending.Task)", adapted)
        self.assertNotIn("return new ValueTask<PickedFolder?>(_pending.Task)", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE + OLD_PICK)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE.replace("PersistPermission(uri, data)", "PersistPermission(uri, other)"))

    def test_direct_folder_access_is_bounded_and_never_writes(self):
        adapted = adapt_access(ACCESS_SOURCE)
        self.assertIn("SaveInputSnapshot.ReadAsync(input, cancellationToken)", adapted)
        self.assertIn("ValueTask.FromException(new UnsafeSaveWriteException", adapted)
        for forbidden in ("OpenOutputStream", "CreateDocument(", "CopyToAsync(buffer", "FindDocument("):
            self.assertNotIn(forbidden, adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_access(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_access(ACCESS_SOURCE + OLD_WRITE)
        with self.assertRaises(ProbeRefusal):
            adapt_access(ACCESS_SOURCE.replace("CopyToAsync(buffer, cancellationToken)",
                                               "CopyToAsync(buffer)"))

    def test_di_wraps_folder_service_independently_of_save_barriers(self):
        adapted = adapt_program(PROGRAM_SOURCE)
        self.assertIn("new ReadOnlyFolderFileAccess(new AndroidFolderFileAccess())", adapted)
        self.assertIn("ReadOnlySaveWriter", adapted)
        self.assertIn("ReadOnlySaveFileAccess", adapted)
        self.assertIn("SaveInputSnapshot.FromBytes(bytes.Span)", WRAPPER_SOURCE)
        self.assertNotIn("reader.WriteFileAsync", WRAPPER_SOURCE)
        with self.assertRaises(ProbeRefusal):
            adapt_program(PROGRAM_SOURCE.replace("ReadOnlySaveWriter", "SafeSaveWriter"))
        with self.assertRaises(ProbeRefusal):
            adapt_program(PROGRAM_SOURCE + PROGRAM_SOURCE)
        with self.assertRaises(ProbeRefusal):
            adapt_program(adapted)

    def test_preflight_refuses_bad_inputs_without_partial_changes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for path, source in ((PROGRAM, PROGRAM_SOURCE), (PICKER, PICKER_SOURCE), (ACCESS, ACCESS_SOURCE)):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(source, encoding="utf-8")
            picker = root / PICKER
            original = (root / PROGRAM).read_bytes()
            picker.write_text(PICKER_SOURCE.replace("GrantWriteUriPermission", "GrantUnknownUriPermission"))
            with patch("scripts.pkforge_folder_readonly.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            self.assertEqual((root / PROGRAM).read_bytes(), original)
            self.assertFalse((root / WRAPPER).exists())
            picker.write_text(PICKER_SOURCE)
            with patch("scripts.pkforge_folder_readonly.sha", return_value=PKFORGE):
                result = prepare(root)
                self.assertFalse(result["folder_exports_enabled"])
                self.assertTrue(result["folder_reads_bounded"])
                self.assertFalse(result["folder_write_grant_requested"])
                self.assertEqual((root / WRAPPER).read_text(), WRAPPER_SOURCE)
                with self.assertRaises(ProbeRefusal):
                    prepare(root)

    def test_unpinned_and_symlinked_sources_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for path, source in ((PROGRAM, PROGRAM_SOURCE), (PICKER, PICKER_SOURCE), (ACCESS, ACCESS_SOURCE)):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(source)
            with patch("scripts.pkforge_folder_readonly.sha", return_value="wrong"):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            target = root / ACCESS
            real = root / "real.cs"
            real.write_text(ACCESS_SOURCE)
            target.unlink()
            target.symlink_to(real)
            with patch("scripts.pkforge_folder_readonly.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)


if __name__ == "__main__":
    unittest.main()
