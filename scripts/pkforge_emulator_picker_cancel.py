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
    # Android can retain a legacy mResumedActivity record for a paused app.
    # Prefer the actual top-resumed activity when both are present.
    lines = dump.splitlines()
    for key in ("topResumedActivity", "mResumedActivity"):
        matching = [line for line in lines if re.search(
            r"\b" + key + r"\s*[:=]", line)]
        if matching:
            return matching[-1]
    return ""


def is_picker(dump: str) -> bool:
    activity = resumed_activity(dump).lower()
    return any(package in activity for package in PICKER_PACKAGES)

def resumed_packages(dump: str) -> tuple[str, ...]:
    """Return only Android component package names, never accessibility text."""
    names = set()
    for line in dump.splitlines():
        if re.search(r"\b(?:topResumedActivity|mResumedActivity)\s*[:=]", line):
            names.update(re.findall(r"\b([A-Za-z][A-Za-z0-9_.]+)/[A-Za-z0-9_.$]+", line))
    return tuple(sorted(names))



def exact_readonly_document_launch(log: str) -> bool:
    """Only the last fresh DocumentsUI START intent can authorize this smoke."""
    starts = [line for line in log.splitlines()
              if "START u" in line and "documentsui" in line.lower()]
    if not starts:
        return False
    line = starts[-1]
    # A corrupted/concatenated log line cannot prove which launch was last.
    # Never accept its first action while ignoring a later unsafe picker.
    if line.count("START u") != 1:
        return False
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
    events = adb("shell", "logcat", "-d", "-b", "main", "-b", "system", "-s", "ActivityTaskManager:I")
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


def open_picker(package: str) -> None:
    """Reach only PKForge's single-file route; never blindly Back out of Home.

    The previous B-then-X loop could send B before the first-run router owned
    the key. Android then returned to Launcher, where subsequent inputs were
    meaningless. Try Home's safe X route first, and use B only when an actual
    pinned welcome-dialogue label is visible. Never choose the folder option.
    """
    last_ui_state = "no accessible labels"
    welcome_fragments = ("Hello!", "This is a place where", "I can edit them",
                         "And every change", "Now then")
    for attempt in range(20):
        if not wait_for(lambda text: is_app(text, package), 3):
            raise RuntimeError("PKForge left foreground before picker interaction")
        # X is a no-op while a dialogue/menu owns the router. On Home it
        # reaches LinkFileAsync directly without choosing an emulator folder.
        adb("shell", "logcat", "-c", "-b", "main", "-b", "system")
        adb("shell", "input", "keyevent", "99")
        if wait_for(is_picker, 3):
            verify_file_picker_launch()
            return
        current = adb("shell", "dumpsys", "activity", "activities")
        if not is_app(current, package):
            # Distinguish an unexpected Launcher/permission foreground from a
            # file picker that our resumed-Activity matcher failed to identify.
            # Only safe package names and action classification reach CI logs.
            events = adb("shell", "logcat", "-d", "-b", "main", "-b", "system",
                         "-s", "ActivityTaskManager:I")
            action = ("OPEN_DOCUMENT launch seen" if exact_readonly_document_launch(events)
                      else "no verified OPEN_DOCUMENT launch")
            # Only classify crash presence. Never print arbitrary logcat text,
            # filenames, UI hierarchy or exception messages into CI output.
            crash = adb("shell", "logcat", "-d", "-b", "crash", "-s", "AndroidRuntime:E")
            crash_state = ("AndroidRuntime fatal marker present"
                           if "FATAL EXCEPTION" in crash and package in crash
                           else "no matching AndroidRuntime fatal marker")
            packages = ",".join(resumed_packages(current))[:160] or "unavailable"
            raise RuntimeError("PKForge left foreground after safe X input; " +
                               "resumed packages=" + packages + "; " + action + "; " + crash_state)
        try:
            nodes = ui_nodes(retries=2)
        except RuntimeError as error:
            last_ui_state = str(error)
            nodes = []
        point = find_target(nodes)
        if point is not None:
            adb("shell", "logcat", "-c", "-b", "main", "-b", "system")
            adb("shell", "input", "tap", str(point[0]), str(point[1]))
            if wait_for(is_picker, 8):
                verify_file_picker_launch()
                return
            if not is_app(adb("shell", "dumpsys", "activity", "activities"), package):
                raise RuntimeError("PKForge left foreground after explicit file tap")
        # Report only pinned, public onboarding text. Never dump arbitrary UI
        # labels, document names, or the accessibility hierarchy into CI logs.
        labels = [node.get("text", "") or node.get("content-desc", "") for node in nodes]
        known = ("Get started", "Open a single save file", "Maybe later",
                 "Link an emulator", "File", "Hello! Welcome to PKForge!")
        visible = [label for label in known if any(label in text for text in labels)]
        if visible:
            last_ui_state = ", ".join(visible)
        else:
            classes = sorted({node.get("class", "?") for node in nodes})
            last_ui_state = f"accessible nodes={len(nodes)}; classes={classes[:8]}"
        if any(any(fragment in label for fragment in welcome_fragments) for label in labels):
            # B advances only a proven welcome dialogue. Never press B on an
            # unrecognized screen: it can exit the app before the router binds.
            adb("shell", "input", "keyevent", "97")
            if not wait_for(lambda text: is_app(text, package), 3):
                raise RuntimeError("PKForge left foreground during welcome dialogue")
        elif "Get started" in visible:
            raise RuntimeError("Welcome menu lacks an accessible safe single-file choice")
        else:
            time.sleep(0.5)
    raise RuntimeError("Real PKForge file picker did not open; " + last_ui_state)


def run(package: str) -> None:
    for attempt in range(2):
        if not wait_for(lambda text: is_app(text, package), 10):
            raise RuntimeError("PKForge not foreground before picker")
        open_picker(package)
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
