"""Bounded Android accessibility picker-cancellation probes."""
import importlib.util
from pathlib import Path
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/pkforge_emulator_picker_cancel.py"
spec = importlib.util.spec_from_file_location("picker_cancel", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PickerCancelTests(unittest.TestCase):
    def test_accessibility_bounds_and_explicit_file_choice(self):
        self.assertEqual((50, 70), module.center("[10,20][90,120]"))
        for value in ("", "[0,0][0,0]", "[-1,0][2,3]", "[1,2][3]"):
            with self.assertRaises(ValueError):
                module.center(value)
        nodes = [
            {"text": "File", "bounds": "[0,80][100,100]"},
            {"text": "Open a single save file", "bounds": "[20,20][100,80]"},
        ]
        self.assertEqual((60, 50), module.find_target(nodes))
        self.assertIsNone(module.find_target([{"text": "Other", "bounds": "[0,0][20,20]"}]))

    def test_foreground_picker_only_not_background_pid(self):
        picker = "topResumedActivity=ActivityRecord{u0 com.google.android.documentsui/.FilesActivity}"
        app = "topResumedActivity=ActivityRecord{u0 org.pkforge.app.debug/crc.MainActivity}"
        other = "topResumedActivity=ActivityRecord{u0 com.android.launcher3/.Launcher}\n" \
                "ProcessRecord{1234:org.pkforge.app.debug}"
        self.assertTrue(module.is_picker(picker))
        self.assertFalse(module.is_picker(app))
        self.assertTrue(module.is_app(app, "org.pkforge.app.debug"))
        self.assertFalse(module.is_app(other, "org.pkforge.app.debug"))
        self.assertFalse(module.is_app(picker, "org.pkforge.app.debug"))

    def test_transient_empty_accessibility_dump_recovers_without_ui_leak(self):
        good = "<hierarchy><node text='File' bounds='[0,0][20,20]'/></hierarchy>"
        with patch.object(module, "adb", side_effect=[
            "dumped", "<?xml version='1.0'?>", "dumped", good,
        ]) as adb_call, patch.object(module.time, "sleep") as pause:
            nodes = module.ui_nodes(retries=2)
        self.assertEqual("File", nodes[0]["text"])
        self.assertEqual(4, adb_call.call_count)
        pause.assert_called_once_with(0.5)

    def test_persistent_missing_accessibility_root_fails_closed(self):
        with patch.object(module, "adb", return_value="ERROR"), patch.object(module.time, "sleep"):
            with self.assertRaisesRegex(RuntimeError, r"after 2 tries \(bytes=5\)"):
                module.ui_nodes(retries=2)
        for count in (0, 6):
            with self.assertRaises(ValueError):
                module.ui_nodes(retries=count)

    def test_pinned_homepage_x_route_precedes_unreliable_ui_dump(self):
        with (patch.object(module, "adb") as adb_call,
              patch.object(module, "wait_for", return_value=True),
              patch.object(module, "ui_nodes", side_effect=AssertionError("not needed"))):
            module.open_picker()
        self.assertEqual([
            ("shell", "input", "keyevent", "97"),
            ("shell", "input", "keyevent", "99"),
        ], [call.args for call in adb_call.call_args_list])

    def test_script_never_selects_or_transfers_documents(self):
        source = SCRIPT.read_text()
        for forbidden in ("adb pull", "adb push", "adb backup", "adb restore",
                          "ACTION_CREATE_DOCUMENT", "takePersistableUriPermission",
                          "content query", "gh release"):
            self.assertNotIn(forbidden, source)
        self.assertIn('keyevent", "4"', source)
        self.assertIn("uiautomator", source)
        self.assertIn('keyevent", "99"', source)
        self.assertIn('keyevent", "97"', source)
        self.assertNotIn('keyevent", "96"', source)  # A could choose the folder picker.
        self.assertIn("ui_nodes(retries=2)", source)
        self.assertIn("No document selected", source)


if __name__ == "__main__":
    unittest.main()
