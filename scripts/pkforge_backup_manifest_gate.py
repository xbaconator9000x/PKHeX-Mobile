#!/usr/bin/env python3
"""Verify compiled diagnostic APKs really disable cloud and device-to-device backup."""
import argparse
from pathlib import Path
import re
import subprocess
import zipfile

from scripts.pkforge_artifact_attestation import find_android_packager
from scripts.pkforge_offline_preview import OfflineRefusal, verify_pins

RULE = "res/xml/pkhex_no_transfer.xml"
APK_DIR = "src/PKForge.App/bin/Debug/net10.0-android"


def attribute(xml, name):
    matches = re.findall(r"(?m)^\s*A:\s+[^\n=]*\b" + re.escape(name)
                         + r"(?:\([^\n]*?\))?\s*=\s*([^\n]+)", xml)
    if len(matches) != 1:
        raise OfflineRefusal("Missing or duplicate compiled attribute: " + name)
    return matches[0].strip()


def validate_xml(manifest, rules):
    if len(re.findall(r"(?m)^\s*E:\s+application\b", manifest)) != 1:
        raise OfflineRefusal("Ambiguous compiled application element")
    for name in ("allowBackup", "fullBackupContent"):
        if not re.match(r"(?i)^(?:false\b|\(type\s+0x12\)\s*0x0\b|0x0\b)",
                        attribute(manifest, name)):
            raise OfflineRefusal("Unsafe compiled backup attribute: " + name)
    if not re.match(r"(?i)^@(?:0x[0-9a-f]+\b|xml/pkhex_no_transfer\b)",
                    attribute(manifest, "dataExtractionRules")):
        raise OfflineRefusal("Compiled data extraction rules not referenced")
    if re.search(r"(?m)^\s*E:\s+include\b", rules):
        raise OfflineRefusal("Compiled backup includes data")
    for name in ("cloud-backup", "device-transfer"):
        sections = re.findall(r"(?ms)^\s*E:\s+" + name
                              + r"\b(.*?)(?=^\s*E:\s+(?:cloud-backup|device-transfer)\b|\Z)", rules)
        if len(sections) != 1:
            raise OfflineRefusal("Missing or duplicate compiled section: " + name)
        section = sections[0]
        if len(re.findall(r"(?m)^\s*E:\s+exclude\b", section)) != 1:
            raise OfflineRefusal("Missing compiled exclusion: " + name)
        for key, value in (("domain", "root"), ("path", ".")):
            if not re.search(r"(?m)^\s*A:\s*(?:android:)?" + key
                             + r"(?:\([^\n]*?\))?\s*=\s*[^\n]*[\"']"
                             + re.escape(value) + r"[\"']", section):
                raise OfflineRefusal("Missing compiled exclusion field: " + name + "/" + key)


def dump_xml(packager, apk, entry):
    if packager.name.startswith("aapt2"):
        commands = ([str(packager), "dump", "xmltree", str(apk), "--file", entry],
                    [str(packager), "dump", "xmltree", "--file", entry, str(apk)])
    else:
        commands = ([str(packager), "dump", "xmltree", str(apk), entry],)
    for command in commands:
        try:
            return subprocess.check_output(command, text=True, stderr=subprocess.PIPE, timeout=40)
        except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
            pass
    raise OfflineRefusal("Cannot decode compiled APK XML: " + entry)


def verify(root):
    verify_pins(root)
    root = root.resolve(strict=True)
    apks = sorted((root / APK_DIR).rglob("*.apk"))
    if not apks:
        raise OfflineRefusal("No diagnostic APK found")
    packager = find_android_packager()
    for apk in apks:
        if apk.is_symlink() or not apk.is_file() or not apk.resolve().is_relative_to(root):
            raise OfflineRefusal("Untrusted APK")
        try:
            with zipfile.ZipFile(apk) as archive:
                if archive.namelist().count(RULE) != 1 or archive.testzip() is not None:
                    raise OfflineRefusal("Missing/ambiguous compiled extraction XML")
        except (OSError, zipfile.BadZipFile) as exc:
            raise OfflineRefusal("Invalid APK ZIP") from exc
        validate_xml(dump_xml(packager, apk, "AndroidManifest.xml"),
                     dump_xml(packager, apk, RULE))
    return len(apks)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--repo", type=Path, required=True)
    args = p.parse_args()
    print("Compiled backup/transfer policy verified for", verify(args.repo), "APKs")
