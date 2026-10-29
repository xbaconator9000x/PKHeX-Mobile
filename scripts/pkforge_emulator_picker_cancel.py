#!/usr/bin/env python3
"""Exercise PKForge's real ACTION_OPEN_DOCUMENT picker cancellation on an empty emulator.

No fixture is selected, no URI is persisted, and no save is read or written.
This is a UI smoke, not provider-byte or physical-device certification.
"""
import argparse
import re
import subprocess
import time
import uuid
from xml.etree import ElementTree as ET

BOUNDS = re.compile(r"^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$")
PICKER_PACKAGES = ("com.google.android.documentsui/", "com.android.documentsui/")


def adb(*args: str, timeout: int = 35) -> str:
    proc = subprocess.run(["adb", *args], capture_output=True, text=True,
                          check=False, timeout=timeout)
    if proc.returncode:
        raise RuntimeError("Android UI command failed: " + " ".join(args))
    return proc.stdout


class CleanupFailure(RuntimeError):
    """A private Android UI hierarchy was not demonstrably removed."""


def ui_nodes(retries: int = 3) -> list[dict]:
    """Use unique shell-private XML, always remove it, never log visible text."""
    if not 1 <= retries <= 5:
        raise ValueError("Accessibility retry count must be bounded")
    size = 0
    for attempt in range(retries):
        path = "/data/local/tmp/pkforge-ci-" + uuid.uuid4().hex + ".xml"
        try:
            try:
                adb("shell", "uiautomator", "dump", path)
                xml = adb("exec-out", "cat", path)
                size = len(xml)
                start = xml.find("<hierarchy")
                if start >= 0:
                    try:
                        root = ET.fromstring(xml[start:])
                    except ET.ParseError:
                        pass
                    else:
                        return [node.attrib for node in root.iter("node")]
            finally:
                # No hierarchy containing document names may survive even a
                # malformed parse, interrupted read or failed dump.
                try:
                    adb("shell", "rm", "-f", path)
                    adb("shell", "test", "!", "-e", path)
                except Exception as error:
                    raise CleanupFailure("Android private accessibility XML cleanup failed") from error
        except CleanupFailure:
            raise
        except RuntimeError:
            pass  # Transient UI dump failures are bounded, not accepted.
        if attempt + 1 < retries:
            time.sleep(0.5)
    raise RuntimeError(f"Android accessibility hierarchy unavailable after {retries} tries (bytes={size})")


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


def exact_readonly_document_launch(log: str) -> bool:
    """Only the last fresh DocumentsUI START intent can authorize this smoke."""
    starts = [line for line in log.splitlines()
              if "START u" in line and "documentsui" in line.lower()]
    if not starts:
        return False
    line = starts[-1]
    action = re.search(r"\bact=([^\s}]+)", line)
    flags = re.search(r"\bflg=0x([0-9a-fA-F]+)", line)
    if action is None or action.group(1) != "android.intent.action.OPEN_DOCUMENT":
        return False
    if "android.intent.category.OPENABLE" not in line or flags is None:
        return False
    grants = int(flags.group(1), 16)
    return bool(grants & 0x1) and not bool(grants & 0x2)


def verify_file_picker_launch() -> None:
    # The ActivityTaskManager START record is cleared immediately before the
    # input that opens the picker. Foreground DocumentsUI alone is insufficient.
    events = adb("shell", "logcat", "-d", "-b", "main", "-s", "ActivityTaskManager:I")
    if not exact_readonly_document_launch(events):
        raise RuntimeError("DocumentsUI did not prove fresh read-only ACTION_OPEN_DOCUMENT")


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
    # The pinned DialogueBox advances on B; the first-run PadMenu cancels
    # safely on B. Unlike A, B can NEVER select the default "Link an emulator"
    # option (which could open a folder picker and cause a false-positive).
    # Once Home owns the router, X calls its existing LinkFileAsync path.
    # No picker is launched by this script and no document is selected.
    last_ui_state = "no accessible labels"
    for _ in range(20):
        adb("shell", "input", "keyevent", "97")  # KEYCODE_BUTTON_B: advance/cancel, never choose
        time.sleep(0.3)
        adb("shell", "logcat", "-c", "-b", "main")
        adb("shell", "input", "keyevent", "99")  # KEYCODE_BUTTON_X -> HomePage LinkFileAsync
        if wait_for(is_picker, 3):
            verify_file_picker_launch()
            return
        try:
            nodes = ui_nodes(retries=2)
        except RuntimeError as error:
            last_ui_state = str(error)
            nodes = []
        point = find_target(nodes)
        if point is not None:
            adb("shell", "logcat", "-c", "-b", "main")
            adb("shell", "input", "tap", str(point[0]), str(point[1]))
            if wait_for(is_picker, 8):
                verify_file_picker_launch()
                return
        # Report only known public onboarding labels, never arbitrary UI text.
        labels = [node.get("text", "") or node.get("content-desc", "") for node in nodes]
        known = ("Get started", "Open a single save file", "Maybe later",
                 "Link an emulator", "File", "Hello! Welcome to PKForge!")
        visible = [label for label in known if any(label in text for text in labels)]
        if visible:
            last_ui_state = ", ".join(visible)
        else:
            # Classes are safe diagnostics even when MAUI text is inaccessible.
            classes = sorted({node.get("class", "?") for node in nodes})
            last_ui_state = f"accessible nodes={len(nodes)}; classes={classes[:8]}"
    raise RuntimeError("Real PKForge file picker did not open; " + last_ui_state)


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
