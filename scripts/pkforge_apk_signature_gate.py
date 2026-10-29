#!/usr/bin/env python3
"""Verify the exact selected diagnostic APK's Android signature before emulator install.

This does not authorize distribution, release signing, or save writes. The caller
must invoke this in the same CI job immediately before adb install, then install
the exact path from the validated selection (not a fresh file search).
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

from pkforge_emulator_apk_selection import SelectionError, verify_selected_apk

CERT = re.compile(r"^Signer #\d+ certificate SHA-256 digest:\s*([0-9a-fA-F]{64})\s*$", re.M)
SCHEME = re.compile(r"^Verified using v([1-4]) scheme(?:\s*\([^\n]*\))?:\s*true\s*$", re.I | re.M)


def verify_signature(report: dict, selection: dict, build_dir: Path,
                     mobile_sha: str, snapshot: Path, apksigner: str = "apksigner") -> dict:
    """Fail closed on unsigned, tampered, mismatched or ambiguous APKs."""
    chosen = verify_selected_apk(report, selection, build_dir, mobile_sha,
                                 source_snapshot=snapshot)
    apk = Path(chosen["path"])
    before = hashlib.sha256(apk.read_bytes()).hexdigest()
    if before != chosen["sha256"]:
        raise SelectionError("APK changed before signature verification")
    try:
        result = subprocess.run(
            [apksigner, "verify", "--verbose", "--print-certs", str(apk)],
            capture_output=True, text=True, check=False, timeout=60,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise SelectionError("Android signature verifier unavailable or timed out") from exc
    if result.returncode != 0:
        raise SelectionError("Android APK signature verification failed")
    schemes = sorted(set(SCHEME.findall(result.stdout)))
    certs = sorted(set(d.lower() for d in CERT.findall(result.stdout)))
    if not schemes or not certs:
        raise SelectionError("Missing verified Android signature scheme or certificate SHA-256")
    # Verify source, provenance, selected file, and bytes again after invoking
    # the external signer tool. This does not remove a later install-time race.
    verify_selected_apk(report, selection, build_dir, mobile_sha,
                        source_snapshot=snapshot)
    after = hashlib.sha256(apk.read_bytes()).hexdigest()
    if before != after:
        raise SelectionError("APK changed during signature verification")
    return {
        "schema": 1,
        "source_sha": mobile_sha,
        "apk_sha256": after,
        "application_id": chosen["application_id"],
        "signer_certificate_sha256": certs,
        "verified_signature_schemes": schemes,
        "emulator_only": True,
        "distribution_authorized": False,
        "save_writes_authorized": False,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--provenance", type=Path, required=True)
    parser.add_argument("--selection", type=Path, required=True)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--source-snapshot", type=Path, required=True)
    parser.add_argument("--expected-mobile-sha", required=True)
    parser.add_argument("--apksigner", default="apksigner")
    args = parser.parse_args()
    evidence = verify_signature(
        json.loads(args.provenance.read_text(encoding="utf-8")),
        json.loads(args.selection.read_text(encoding="utf-8")),
        args.build_dir, args.expected_mobile_sha, args.source_snapshot,
        args.apksigner,
    )
    # Ephemeral stdout evidence only. Never upload the APK or treat the CI debug
    # signer as an approved release key.
    print(json.dumps(evidence, sort_keys=True))


if __name__ == "__main__":
    main()
