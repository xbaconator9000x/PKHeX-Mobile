"""Offline repository hygiene checks, not actual Pokémon save editing tests."""
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REQUIRED = (
    "README.md", "LICENSE", "CONTRIBUTING.md", "THIRD_PARTY.md",
    "docs/ROADMAP.md", "docs/ARCHITECTURE.md", "docs/SAFETY.md",
    "docs/UPSTREAM_REVIEW.md", "docs/POKEBACON_INTEGRATION.md",
    "src/PKHeXMobile.Domain/SaveWriteGuard.cs",
    "tests/PKHeXMobile.Domain.Smoke/Program.cs",
)
FORBIDDEN_SUFFIXES = {
    ".gba", ".gb", ".gbc", ".nds", ".3ds", ".cia", ".nsp", ".xci",
    ".sav", ".dsv", ".gci", ".apk", ".aab", ".jks", ".keystore",
    ".pk1", ".pk2", ".pk3", ".pk4", ".pk5", ".pk6", ".pk7", ".pk8", ".pk9",
}

def failures():
    issues = []
    for name in REQUIRED:
        path = ROOT / name
        if not path.is_file() or not path.read_bytes():
            issues.append(f"Missing/empty required file: {name}")
    for path in ROOT.rglob("*"):
        if not path.is_file() or ".git" in path.parts:
            continue
        if path.suffix.lower() in FORBIDDEN_SUFFIXES:
            issues.append(f"Forbidden binary or user file: {path.relative_to(ROOT)}")
    return issues

if __name__ == "__main__":
    errors = failures()
    if errors:
        print("\n".join(errors))
        raise SystemExit(1)
    print("Repository scaffold checks passed (no real-save tests run).")
