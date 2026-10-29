"""Synthetic-only refusal tests for attested APK-to-emulator selection."""
import copy
import hashlib
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pkforge_emulator_apk_selection import SelectionError, select_apk, verify_selected_apk

SOURCE_SHA = "a" * 40


class AttestedEmulatorSelectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.build = Path(self.temp.name) / "build"
        self.build.mkdir()
        self.signed = self.build / "PKForge-Signed.apk"
        self.signed.write_bytes(b"synthetic diagnostic APK bytes only")
        self.unsigned = self.build / "PKForge.apk"
        self.unsigned.write_bytes(b"synthetic unsigned APK bytes")
        self.report = {
            "schema": 1,
            "purpose": "non-distributable diagnostic metadata",
            "redistribution_authorized": False,
            "save_writes_authorized": False,
            "device_certified": False,
            "pins": {"mobile_head": SOURCE_SHA},
            "frozen_source": {"sha256": "b" * 64},
            "apks": [self.entry(self.signed), self.entry(self.unsigned)],
        }

    @staticmethod
    def entry(path):
        data = path.read_bytes()
        return {
            "filename": path.name,
            "sha256": hashlib.sha256(data).hexdigest(),
            "size_bytes": len(data),
            "application_id": "org.pkforge.app.debug",
            "payload_structural_check": True,
            "apk_tested_on_device": False,
        }

    def choose(self, report=None, variant="signed"):
        return select_apk(report if report is not None else self.report,
                          self.build, SOURCE_SHA, variant)

    def test_selects_only_attested_signed_apk(self):
        selected = self.choose()
        self.assertEqual(selected["path"], str(self.signed.absolute()))
        self.assertEqual(selected["sha256"], self.report["apks"][0]["sha256"])
        self.assertFalse(selected["distribution_authorized"])
        self.assertFalse(selected["save_writes_authorized"])
        self.assertTrue(selected["emulator_only"])

    def test_reverification_accepts_unchanged_selection(self):
        selection = self.choose()
        self.assertEqual(selection, verify_selected_apk(
            self.report, selection, self.build, SOURCE_SHA))

    def test_reverification_refuses_changed_bytes_after_selection(self):
        selection = self.choose()
        self.signed.write_bytes(b"x" * self.signed.stat().st_size)
        with self.assertRaisesRegex(SelectionError, "changed"):
            verify_selected_apk(self.report, selection, self.build, SOURCE_SHA)

    def test_reverification_refuses_modified_selection_record(self):
        selection = self.choose()
        for key, value in (("path", str(self.unsigned.absolute())),
                           ("sha256", "f" * 64),
                           ("distribution_authorized", True),
                           ("source_sha", "c" * 40)):
            modified = copy.deepcopy(selection)
            modified[key] = value
            with self.subTest(key=key), self.assertRaisesRegex(
                    SelectionError, "differs"):
                verify_selected_apk(self.report, modified, self.build, SOURCE_SHA)

    def test_reverification_refuses_deleted_or_symlinked_apk(self):
        selection = self.choose()
        self.signed.rename(self.build / "moved.apk")
        with self.assertRaises(SelectionError):
            verify_selected_apk(self.report, selection, self.build, SOURCE_SHA)
        self.signed.symlink_to(self.build / "moved.apk")
        with self.assertRaises(SelectionError):
            verify_selected_apk(self.report, selection, self.build, SOURCE_SHA)

    def test_modified_bytes_rejected(self):
        self.signed.write_bytes(b"changed but same? bytes")
        with self.assertRaises(SelectionError):
            self.choose()

    def test_same_size_tamper_rejected(self):
        self.signed.write_bytes(b"x" * self.signed.stat().st_size)
        with self.assertRaisesRegex(SelectionError, "changed"):
            self.choose()

    def test_ambiguous_signed_candidates_rejected(self):
        second = self.build / "Another-Signed.apk"
        second.write_bytes(b"second synthetic")
        self.report["apks"].append(self.entry(second))
        with self.assertRaisesRegex(SelectionError, "Expected one"):
            self.choose()

    def test_duplicate_basename_in_other_folder_rejected(self):
        nested = self.build / "other"
        nested.mkdir()
        (nested / self.signed.name).write_bytes(self.signed.read_bytes())
        with self.assertRaisesRegex(SelectionError, "on-disk"):
            self.choose()

    def test_missing_signed_does_not_fallback_to_unsigned(self):
        self.report["apks"] = [self.entry(self.unsigned)]
        with self.assertRaisesRegex(SelectionError, "Expected one"):
            self.choose()
        self.assertEqual(self.choose(variant="single")["path"],
                         str(self.unsigned.absolute()))

    def test_stale_commit_rejected(self):
        self.report["pins"]["mobile_head"] = "c" * 40
        with self.assertRaisesRegex(SelectionError, "source commit"):
            self.choose()

    def test_missing_freeze_rejected(self):
        self.report["frozen_source"] = None
        with self.assertRaisesRegex(SelectionError, "source freeze"):
            self.choose()

    def test_write_or_distribution_authorization_rejected(self):
        for key in ("save_writes_authorized", "redistribution_authorized",
                    "device_certified"):
            changed = copy.deepcopy(self.report)
            changed[key] = True
            with self.subTest(key=key), self.assertRaises(SelectionError):
                self.choose(changed)

    def test_bad_payload_or_package_rejected(self):
        for key, value in (("payload_structural_check", False),
                           ("application_id", "org.example.other"),
                           ("apk_tested_on_device", True)):
            changed = copy.deepcopy(self.report)
            changed["apks"][0][key] = value
            with self.subTest(key=key), self.assertRaises(SelectionError):
                self.choose(changed)

    def test_symlink_apk_rejected(self):
        original = self.build / "original.apk"
        self.signed.rename(original)
        self.signed.symlink_to(original)
        with self.assertRaisesRegex(SelectionError, "Symlink"):
            self.choose()

    def test_unsafe_attested_filename_rejected(self):
        for name in ("../PKForge-Signed.apk", "/tmp/evil.apk", "other\\file.apk"):
            changed = copy.deepcopy(self.report)
            changed["apks"][0]["filename"] = name
            with self.subTest(name=name), self.assertRaises(SelectionError):
                self.choose(changed)

    def test_duplicate_attestation_names_rejected(self):
        self.report["apks"].append(copy.deepcopy(self.report["apks"][0]))
        with self.assertRaisesRegex(SelectionError, "duplicate"):
            self.choose()

    def test_size_mismatch_rejected(self):
        self.report["apks"][0]["size_bytes"] += 1
        with self.assertRaisesRegex(SelectionError, "size"):
            self.choose()


if __name__ == "__main__":
    unittest.main()
