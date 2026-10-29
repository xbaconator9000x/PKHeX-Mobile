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
        xml = iter(["<?xml version='1.0'?>", good])
        def fake_adb(*args):
            return next(xml) if args[:2] == ("exec-out", "cat") else ""
        with (patch.object(module, "adb", side_effect=fake_adb) as adb_call,
              patch.object(module.time, "sleep") as pause):
            nodes = module.ui_nodes(retries=2)
        self.assertEqual("File", nodes[0]["text"])
        self.assertEqual(8, adb_call.call_count)
        paths = [call.args[-1] for call in adb_call.call_args_list
                 if call.args[:3] == ("shell", "rm", "-f")]
        self.assertEqual(2, len(paths))
        self.assertEqual(2, len(set(paths)))
        self.assertTrue(all(path.startswith("/data/local/tmp/") for path in paths))
        self.assertEqual(2, sum(call.args[:4] == ("shell", "test", "!", "-e")
                                for call in adb_call.call_args_list))
        pause.assert_called_once_with(0.5)

    def test_xml_cleanup_failure_is_never_swallowed(self):
        def fake_adb(*args):
            if args[:3] == ("shell", "rm", "-f"):
                raise RuntimeError("synthetic cleanup failure")
            return "<hierarchy/>" if args[:2] == ("exec-out", "cat") else ""
        with patch.object(module, "adb", side_effect=fake_adb):
            with self.assertRaises(module.CleanupFailure):
                module.ui_nodes(retries=2)

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
              patch.object(module, "verify_file_picker_launch") as verified,
              patch.object(module, "ui_nodes", side_effect=AssertionError("not needed"))):
            module.open_picker("org.pkforge.app.debug")
        self.assertEqual([
            ("shell", "logcat", "-c", "-b", "main"),
            ("shell", "input", "keyevent", "99"),
        ], [call.args for call in adb_call.call_args_list])
        verified.assert_called_once_with()

    def test_unrecognized_foreground_never_receives_blind_back(self):
        pkg = "org.pkforge.app.debug"
        activity = "topResumedActivity=ActivityRecord{u0 " + pkg + "/crc.MainActivity}"
        def fake_adb(*args):
            return activity if args[:3] == ("shell", "dumpsys", "activity") else ""
        with (patch.object(module, "adb", side_effect=fake_adb) as calls,
              patch.object(module, "wait_for",
                           side_effect=lambda predicate, seconds=20: predicate(activity)),
              patch.object(module, "ui_nodes", return_value=[]),
              patch.object(module.time, "sleep")):
            with self.assertRaisesRegex(RuntimeError, "file picker did not open"):
                module.open_picker(pkg)
        self.assertNotIn(("shell", "input", "keyevent", "97"),
                         [call.args for call in calls.call_args_list])

    def test_welcome_dialogue_is_the_only_b_advance_route(self):
        pkg = "org.pkforge.app.debug"
        activity = "topResumedActivity=ActivityRecord{u0 " + pkg + "/crc.MainActivity}"
        def fake_adb(*args):
            return activity if args[:3] == ("shell", "dumpsys", "activity") else ""
        with (patch.object(module, "adb", side_effect=fake_adb) as calls,
              patch.object(module, "wait_for",
                           side_effect=lambda predicate, seconds=20: predicate(activity)),
              patch.object(module, "ui_nodes", return_value=[
                  {"text": "Hello! Welcome to PKForge!", "class": "android.widget.TextView"}]),
              patch.object(module.time, "sleep")):
            with self.assertRaisesRegex(RuntimeError, "file picker did not open"):
                module.open_picker(pkg)
        self.assertIn(("shell", "input", "keyevent", "97"),
                      [call.args for call in calls.call_args_list])

    def test_fresh_intent_parser_rejects_tree_stale_and_write_grants(self):
        def start(action, flags="0x41", category="android.intent.category.OPENABLE"):
            return ("I ActivityTaskManager: START u0 {act=" + action
                    + " cat=[" + category + "] typ=*/* flg=" + flags
                    + " cmp=com.google.android.documentsui/.files.FilesActivity}")
        good = start("android.intent.action.OPEN_DOCUMENT")
        self.assertTrue(module.exact_readonly_document_launch(good))
        for bad in (
            start("android.intent.action.OPEN_DOCUMENT_TREE"),
            start("android.intent.action.GET_CONTENT"),
            start("android.intent.action.OPEN_DOCUMENT", flags="0x43"),
            start("android.intent.action.OPEN_DOCUMENT", flags="0x40"),
            start("android.intent.action.OPEN_DOCUMENT", category="android.intent.category.DEFAULT"),
            good + "\n" + start("android.intent.action.OPEN_DOCUMENT_TREE"),
            good + "\\n" + start("android.intent.action.OPEN_DOCUMENT_TREE"),  # malformed one-line record
            "",
        ):
            self.assertFalse(module.exact_readonly_document_launch(bad))
        self.assertTrue(module.exact_readonly_document_launch(
            start("android.intent.action.OPEN_DOCUMENT_TREE") + "\n" + good))

    def test_launch_verification_refuses_missing_or_wrong_action(self):
        with patch.object(module, "adb", return_value=""):
            with self.assertRaisesRegex(RuntimeError, "fresh read-only"):
                module.verify_file_picker_launch()
        with patch.object(module, "adb", return_value=(
            "I ActivityTaskManager: START u0 {act=android.intent.action.OPEN_DOCUMENT "
            "cat=[android.intent.category.OPENABLE] flg=0x41 "
            "cmp=com.android.documentsui/.files.FilesActivity}")) as adb_call:
            module.verify_file_picker_launch()
        adb_call.assert_called_once_with("shell", "logcat", "-d", "-b", "main",
                                         "-s", "ActivityTaskManager:I")

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
        self.assertNotIn("/sdcard/", source)
        self.assertIn('adb("shell", "test", "!", "-e", path)', source)
        self.assertIn("No document selected", source)


if __name__ == "__main__":
    unittest.main()
