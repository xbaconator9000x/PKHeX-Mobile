"""Synthetic signature-gate protocol tests; not Android cryptographic tests."""
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "pkforge_apk_signature_gate.py"
sys.path.insert(0, str(SCRIPT.parent))
from pkforge_emulator_apk_selection import SelectionError

spec = importlib.util.spec_from_file_location("pkforge_apk_signature_gate", SCRIPT)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

CERT = "a" * 64
GOOD = ("Verifies\nVerified using v1 scheme (JAR signing): false\n"
        "Verified using v2 scheme (APK Signature Scheme v2): true\n"
        "Number of signers: 1\nSigner #1 certificate SHA-256 digest: " + CERT + "\n")


class SignatureGateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.build = Path(self.tmp.name)
        self.apk = self.build / "Synthetic-Signed.apk"
        self.apk.write_bytes(b"synthetic fixture; not an Android APK")
        self.selection = {
            "path": str(self.apk),
            "sha256": hashlib.sha256(self.apk.read_bytes()).hexdigest(),
            "application_id": "org.pkforge.app.debug",
        }
        self.snapshot = self.build / "freeze.json"
        self.snapshot.write_text("{}", encoding="utf-8")

    def run_gate(self, result=None, side_effect=None):
        response = result or subprocess.CompletedProcess([], 0, GOOD, "")
        with patch.object(gate, "verify_selected_apk", return_value=self.selection) as selected:
            with patch.object(gate.subprocess, "run", return_value=response,
                              side_effect=side_effect) as proc:
                output = gate.verify_signature({}, self.selection, self.build,
                                               "b" * 40, self.snapshot)
                self.assertEqual(selected.call_count, 2)
                self.assertEqual(proc.call_args.args[0][:4],
                                 ["apksigner", "verify", "--verbose", "--print-certs"])
                return output

    def test_verified_scheme_and_cert_are_bound_to_apk_and_source(self):
        result = self.run_gate()
        self.assertEqual(result["apk_sha256"], self.selection["sha256"])
        self.assertEqual(result["source_sha"], "b" * 40)
        self.assertEqual(result["signer_certificate_sha256"], [CERT])
        self.assertEqual(result["verified_signature_schemes"], ["2"])
        self.assertFalse(result["distribution_authorized"])
        self.assertFalse(result["save_writes_authorized"])

    def test_modern_v3_certificate_and_scheme_are_accepted(self):
        modern = ("Verifies\nVerified using v3.1 scheme (APK Signature Scheme v3.1): true\n"
                  "V3.0 Signer: certificate SHA-256 digest: " + CERT + "\n")
        result = self.run_gate(subprocess.CompletedProcess([], 0, modern, ""))
        self.assertEqual(result["signer_certificate_sha256"], [CERT])
        self.assertEqual(result["verified_signature_schemes"], ["3.1"])

    def test_unsigned_suffix_is_not_proof_of_signature(self):
        with self.assertRaisesRegex(SelectionError, "signature verification failed"):
            self.run_gate(subprocess.CompletedProcess([], 1, "DOES NOT VERIFY", ""))

    def test_success_exit_without_verified_scheme_is_refused(self):
        for output in ("", "Verified using v2 scheme (APK Signature Scheme v2): false\nSigner #1 certificate SHA-256 digest: " + CERT,
                       "Verified using v2 scheme: true\n"):
            with self.subTest(output=output), self.assertRaisesRegex(SelectionError, "Missing verified"):
                self.run_gate(subprocess.CompletedProcess([], 0, output, ""))

    def test_missing_or_invalid_certificate_is_refused(self):
        for output in ("Verified using v2 scheme: true\n",
                       "Verified using v2 scheme: true\nSigner #1 certificate SHA-256 digest: invalid\n"):
            with self.subTest(output=output), self.assertRaisesRegex(SelectionError, "certificate"):
                self.run_gate(subprocess.CompletedProcess([], 0, output, ""))

    def test_missing_verifier_and_timeout_are_refused(self):
        for exc in (FileNotFoundError("apksigner"), subprocess.TimeoutExpired("apksigner", 60)):
            with self.subTest(exc=type(exc).__name__), self.assertRaisesRegex(SelectionError, "unavailable"):
                self.run_gate(side_effect=exc)

    def test_mutation_during_signature_verification_is_refused(self):
        def tamper(*args, **kwargs):
            self.apk.write_bytes(b"changed fixture; not an Android APK")
            return subprocess.CompletedProcess([], 0, GOOD, "")
        with self.assertRaises(SelectionError):
            self.run_gate(side_effect=tamper)

    def test_preexisting_tamper_is_refused(self):
        self.apk.write_bytes(b"changed")
        with self.assertRaisesRegex(SelectionError, "changed before"):
            self.run_gate()


if __name__ == "__main__":
    unittest.main()
