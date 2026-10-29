"""Signed-APK emulator launch and foreground acceptance regressions."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNNER = ROOT / "scripts/pkforge_emulator_launch_smoke.sh"
FOREGROUND = ROOT / "scripts/pkforge_emulator_foreground.py"
WORKFLOW = ROOT / ".github/workflows/pkforge-official-core-probe.yml"
spec = importlib.util.spec_from_file_location("foreground_gate", FOREGROUND)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class EmulatorLaunchGates(unittest.TestCase):
    def test_shell_syntax_and_attestation_before_install(self):
        subprocess.run(["bash", "-n", str(RUNNER)], check=True)
        source = RUNNER.read_text()
        for required in ("--verify-selection", "pkforge_apk_signature_gate.py",
                         "pkforge-selected-apk.json", "sha256sum", "adb install -r",
                         "dumpsys activity activities", "KEYCODE_HOME",
                         "signer_certificate_sha256", "org.pkforge.app.debug"):
            self.assertIn(required, source)
        self.assertLess(source.index("--verify-selection"), source.index("adb install -r"))
        self.assertLess(source.index("pkforge_apk_signature_gate.py"), source.index("adb install -r"))
        for forbidden in ("adb push", "adb pull", "adb backup", "adb restore",
                          "gh release", "curl ", "wget "):
            self.assertNotIn(forbidden, source)

    def test_foreground_positive_and_live_background_negative(self):
        pkg = "org.pkforge.app.debug"
        positive = "topResumedActivity=ActivityRecord{a3 u0 org.pkforge.app.debug/crc.MainActivity t2}"
        legacy = "mResumedActivity: ActivityRecord{f2 u0 org.pkforge.app.debug/.MainActivity t2}"
        negative = ("topResumedActivity=ActivityRecord{b1 u0 com.android.launcher3/.Launcher t1}\n"
                    "ProcessRecord{f1 1234:org.pkforge.app.debug/u0a5}")
        self.assertTrue(module.is_foreground(positive, pkg))
        self.assertTrue(module.is_foreground(legacy, pkg))
        self.assertFalse(module.is_foreground(negative, pkg))
        self.assertFalse(module.is_foreground(positive, "org.example.other"))
        self.assertFalse(module.is_foreground("mResumedActivity=null", pkg))
        self.assertFalse(module.is_foreground(positive, "org.pkforge.app.debug\nother"))

    def test_cli_refuses_live_background_process(self):
        p = subprocess.run([sys.executable, str(FOREGROUND), "--package",
                            "org.pkforge.app.debug"],
                           input=("topResumedActivity=ActivityRecord{u0 com.android.launcher3/.Launcher}\n"
                                  "ProcessRecord{1234:org.pkforge.app.debug}"),
                           capture_output=True, text=True)
        self.assertEqual(p.returncode, 1)
        p = subprocess.run([sys.executable, str(FOREGROUND), "--package",
                            "org.pkforge.app.debug"],
                           input="topResumedActivity=ActivityRecord{u0 org.pkforge.app.debug/.MainActivity}",
                           capture_output=True, text=True)
        self.assertEqual(p.returncode, 0, p.stderr)

    def test_workflow_retains_safety_gates_and_final_verdict(self):
        source = WORKFLOW.read_text()
        for required in ("steps.apk-signer.outcome", "steps.backup-apk-manifest.outcome",
                         "steps.offline-apk-permissions.outcome", "steps.neutral-apk-media.outcome",
                         "steps.source-freeze.outcome", "pkforge_emulator_launch_smoke.sh",
                         "reactivecircus/android-emulator-runner@a421e43855164a8197daf9d8d40fe71c6996bb0d",
                         "EMULATOR_SMOKE", "steps.emulator-smoke.outcome"):
            self.assertIn(required, source)
        self.assertIn("push:\n    branches: [main]", source)
        self.assertIn("pull_request:\n    paths:", source)


if __name__ == "__main__":
    unittest.main()
