#!/usr/bin/env python3
"""Fail-closed selection of the exact APK described by diagnostic provenance.

For ephemeral Android emulator CI only. This is not a licence, device, provider,
save-write, or release certification. Never silently select the first find result.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


EXPECTED_PACKAGE = "org.pkforge.app.debug"
SHA256 = re.compile(r"[0-9a-f]{64}\Z")
GIT_SHA = re.compile(r"[0-9a-f]{40}\Z")
APK_NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]*\.apk\Z")


class SelectionError(ValueError):
    pass


def _digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _require_sha(value: object, pattern: re.Pattern, label: str) -> str:
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise SelectionError(f"Missing or malformed {label}")
    return value


def select_apk(report: dict, build_dir: Path, expected_mobile_sha: str,
               variant: str = "signed") -> dict:
    """Verify source pin, unique attested filename and actual APK bytes.

    signed: exactly one attested *-Signed.apk; no unsigned fallback.
    single: exactly one attested APK of any name. Both reject ambiguity.
    """
    _require_sha(expected_mobile_sha, GIT_SHA, "expected source commit")
    if not isinstance(report, dict) or report.get("schema") != 1:
        raise SelectionError("Unknown provenance schema")
    if report.get("purpose") != "non-distributable diagnostic metadata":
        raise SelectionError("Not diagnostic provenance")
    if (report.get("redistribution_authorized") is not False
            or report.get("save_writes_authorized") is not False
            or report.get("device_certified") is not False):
        raise SelectionError("Diagnostic safety flags missing or inconsistent")
    pins = report.get("pins")
    if not isinstance(pins, dict) or pins.get("mobile_head") != expected_mobile_sha:
        raise SelectionError("Provenance is not bound to expected source commit")
    frozen = report.get("frozen_source")
    if not isinstance(frozen, dict):
        raise SelectionError("Missing verified prebuild source freeze")
    _require_sha(frozen.get("sha256"), SHA256, "frozen source hash")
    apks = report.get("apks")
    if not isinstance(apks, list) or not apks:
        raise SelectionError("No attested APKs")
    names = []
    for entry in apks:
        if not isinstance(entry, dict):
            raise SelectionError("Malformed APK attestation")
        name = entry.get("filename")
        if not isinstance(name, str) or not APK_NAME.fullmatch(name):
            raise SelectionError("Unsafe attested APK filename")
        names.append(name)
    if len(names) != len(set(names)):
        raise SelectionError("Ambiguous duplicate attested APK names")
    if variant == "signed":
        candidates = [x for x in apks if x["filename"].endswith("-Signed.apk")]
    elif variant == "single":
        candidates = apks
    else:
        raise SelectionError("Unknown selection variant")
    if len(candidates) != 1:
        raise SelectionError(f"Expected one {variant} APK, got {len(candidates)}")
    chosen = candidates[0]
    if (chosen.get("application_id") != EXPECTED_PACKAGE
            or chosen.get("payload_structural_check") is not True
            or chosen.get("apk_tested_on_device") is not False):
        raise SelectionError("Package/payload diagnostic identity mismatch")
    expected_hash = _require_sha(chosen.get("sha256"), SHA256, "APK hash")
    expected_size = chosen.get("size_bytes")
    if type(expected_size) is not int or expected_size <= 0:
        raise SelectionError("Missing positive APK size")

    if not build_dir.is_dir() or build_dir.is_symlink():
        raise SelectionError("Missing or symlinked build directory")
    # Duplicate filenames anywhere in the build tree are ambiguous, even if
    # one has the correct hash. Never use find -print -quit for APK selection.
    matches = list(build_dir.rglob(chosen["filename"]))
    if len(matches) != 1:
        raise SelectionError(f"Expected one on-disk APK, got {len(matches)}")
    path = matches[0]
    current = path
    while current != build_dir:
        if current.is_symlink():
            raise SelectionError("Symlink in APK path")
        current = current.parent
    if not path.is_file() or path.stat().st_size != expected_size:
        raise SelectionError("APK missing or size changed")
    if _digest(path) != expected_hash:
        raise SelectionError("APK bytes changed after attestation")
    return {
        "schema": 1,
        "path": str(path.absolute()),
        "sha256": expected_hash,
        "application_id": EXPECTED_PACKAGE,
        "source_sha": expected_mobile_sha,
        "emulator_only": True,
        "distribution_authorized": False,
        "save_writes_authorized": False,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--provenance", type=Path, required=True)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--expected-mobile-sha", required=True)
    parser.add_argument("--variant", choices=("signed", "single"), default="signed")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = json.loads(args.provenance.read_text(encoding="utf-8"))
    result = select_apk(report, args.build_dir, args.expected_mobile_sha, args.variant)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, sort_keys=True, indent=2) + "\n",
                           encoding="utf-8")
    print("Selected exact attested diagnostic APK for ephemeral emulator only; "
          "no APK uploaded or release authorized.")


if __name__ == "__main__":
    main()
