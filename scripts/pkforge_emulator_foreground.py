#!/usr/bin/env python3
"""A surviving Android PID is not evidence of a resumed foreground Activity."""
import argparse
import re
import sys


def is_foreground(dump: str, package: str) -> bool:
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_.]+", package):
        return False
    token = re.compile(r"(?<![A-Za-z0-9_.])" + re.escape(package) + r"/[A-Za-z0-9_.$]+")
    lines = [line for line in dump.splitlines()
             if re.search(r"\b(?:topResumedActivity|mResumedActivity)\s*[:=]", line)]
    return any(token.search(line) for line in lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True)
    args = parser.parse_args()
    if not is_foreground(sys.stdin.read(), args.package):
        print("Expected package is not the resumed foreground Activity", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
