"""Fail-closed trigger coverage for the adapted PKForge Android/Core workflow.

This verifies workflow scheduling, not a successful Android compilation.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/pkforge-official-core-probe.yml"
REQUIRED = {
    ".github/workflows/pkforge-official-core-probe.yml",
    "src/PKHeXMobile.PortConsolidation/**",
    "src/PKHeXMobile.Domain/**",
    "src/PKHeXMobile.ReadOnly/**",
    "scripts/pkforge_private_storage_refusal.py",
    "scripts/pkforge_residual_cache_refusal.py",
    "tests/test_pkforge_ci_trigger_coverage.py",
}


def trigger_paths(text: str, event: str) -> list[str] | None:
    pattern = (r"(?m)^  " + re.escape(event) +
               r":\n" + (r"    branches: \[main\]\n" if event == "push" else "") +
               r"    paths:\n(?P<paths>(?:      - '[^'\n]+'\n)+)")
    match = re.search(pattern, text)
    if match is None:
        return None
    return [line.strip()[3:-1] for line in match.group("paths").splitlines()]


def trigger_errors(text: str) -> list[str]:
    errors = []
    if not re.search(r"(?m)^  workflow_dispatch:\s*$", text):
        errors.append("manual dispatch missing")
    paths = {}
    for event in ("push", "pull_request"):
        entries = trigger_paths(text, event)
        if entries is None:
            errors.append(event + " trigger/paths missing")
            continue
        if len(entries) != len(set(entries)):
            errors.append(event + " duplicate paths")
        missing = REQUIRED - set(entries)
        if missing:
            errors.append(event + " missing: " + ", ".join(sorted(missing)))
        paths[event] = set(entries)
    if len(paths) == 2 and paths["push"] != paths["pull_request"]:
        errors.append("PR and merged-main adapted build inputs diverge")
    return errors


class AdaptedEditorTriggerGates(unittest.TestCase):
    def setUp(self):
        self.workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_live_workflow_covers_main_and_pull_request(self):
        self.assertEqual([], trigger_errors(self.workflow))
        self.assertGreater(len(trigger_paths(self.workflow, "push") or []), 40)

    def test_transitive_source_inputs_are_real(self):
        project = (ROOT / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj").read_text()
        private = (ROOT / "scripts/pkforge_private_storage_refusal.py").read_text()
        self.assertIn("../PKHeXMobile.Domain/PKHeXMobile.Domain.csproj", project)
        self.assertIn("pkforge_residual_cache_refusal", private)

    def test_removing_main_trigger_is_refused(self):
        self.assertTrue(trigger_errors(self.workflow.replace("  push:\n", "  missing_push:\n", 1)))

    def test_missing_domain_or_residual_adapter_is_refused(self):
        for path in ("src/PKHeXMobile.Domain/**", "scripts/pkforge_residual_cache_refusal.py"):
            broken = self.workflow.replace("      - '" + path + "'\n", "", 1)
            self.assertTrue(trigger_errors(broken), path)

    def test_duplicate_paths_are_refused(self):
        path = "      - 'src/PKHeXMobile.Domain/**'\n"
        broken = self.workflow.replace(path, path + path, 1)
        self.assertTrue(trigger_errors(broken))

    def test_missing_manual_dispatch_is_refused(self):
        self.assertTrue(trigger_errors(self.workflow.replace("  workflow_dispatch:\n", "", 1)))


if __name__ == "__main__":
    unittest.main()
