#!/usr/bin/env python3
"""Exercise PKForge's real ACTION_OPEN_DOCUMENT picker cancellation on an empty emulator.

No fixture is selected, no URI is persisted, and no save is read or written.
This is a UI smoke, not provider-byte or physical-device certification.
"""
import argparse
import re
import subprocess
import time
from xml.etree import ElementTree as ET

BOUNDS = re.compile(r"^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$")
PICKER_PACKAGES = ("com.google.android.documentsui/", "com.android.documentsui/")


def adb(*args: str, timeout: int = 35) -> str:
    proc = subprocess.run(["adb", *args], capture_output=True, text=True,
                          check=False, timeout=timeout)
    if proc.returncode:
        raise RuntimeError("Android UI command failed: " + " ".join(args))
    return proc.stdout


def ui_nodes() -> list[dict]:
    adb("shell", "uiautomator", "dump", "/sdcard/pkforge-ci-window.xml")
    xml = adb("exec-out", "cat", "/sdcard/pkforge-ci-window.xml")
    start = xml.find("<?xml")
    if start < 0:
        raise RuntimeError("Android accessibility hierarchy unavailable")
    return [node.attrib for node in ET.fromstring(xml[start:]).iter("node")]


def center(bounds: str) -> tuple[int, int]:
    match = BOUNDS.fullmatch(bounds)
    if not match:
        raise ValueError("Invalid Android accessibility bounds")
    x1, y1, x2, y2 = map(int, match.groups())
    if x2 <= x1 or y2 <= y1:
        raise ValueError("Empty Android accessibility bounds")
    return (x1 + x2) // 2, (y1 + y2) // 2


def find_target(nodes: list[dict]) -> tuple[int, int] | None:
    # Prefer the wizard's explicit file choice; otherwise the Home footer.
    for wanted in ("Open a single save file", "File"):
        for node in nodes:
            label = node.get("text", "") or node.get("content-desc", "")
            if label.strip() == wanted:
                try:
                    return center(node.get("bounds", ""))
                except ValueError:
                    continue
    return None


def resumed_activity(dump: str) -> str:
    for line in dump.splitlines():
        if re.search(r"\b(?:topResumedActivity|mResumedActivity)\s*[:=]", line):
            return line
    return ""


def is_picker(dump: str) -> bool:
    activity = resumed_activity(dump).lower()
    return any(package in activity for package in PICKER_PACKAGES)


def is_app(dump: str, package: str) -> bool:
    activity = resumed_activity(dump)
    return package + "/" in activity


def wait_for(predicate, seconds: int = 20) -> bool:
    for _ in range(seconds):
        if predicate(adb("shell", "dumpsys", "activity", "activities")):
            return True
        time.sleep(1)
    return False


def open_picker() -> None:
    # First launch can show the upstream professor-style intro. Advance only
    # until the real file choice becomes accessible; never select a document.
    for _ in range(20):
        nodes = ui_nodes()
        point = find_target(nodes)
        if point is not None:
            adb("shell", "input", "tap", str(point[0]), str(point[1]))
            if wait_for(is_picker, 12):
                return
        adb("shell", "input", "keyevent", "96")  # gamepad A: advance intro
        time.sleep(0.4)
    raise RuntimeError("Real PKForge file picker did not open")


def run(package: str) -> None:
    for attempt in range(2):
        if not wait_for(lambda text: is_app(text, package), 10):
            raise RuntimeError("PKForge not foreground before picker")
        open_picker()
        adb("shell", "input", "keyevent", "4")  # Cancel ACTION_OPEN_DOCUMENT.
        if not wait_for(lambda text: is_app(text, package), 15):
            raise RuntimeError("PKForge failed to resume after picker cancellation")
        if is_picker(adb("shell", "dumpsys", "activity", "activities")):
            raise RuntimeError("Cancelled picker remained foreground")
        print(f"Android provider picker cancel {attempt + 1}/2 returned to PKForge.")
    print("No document selected; no URI read, persist, write or grant verified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", default="org.pkforge.app.debug")
    run(parser.parse_args().package)
