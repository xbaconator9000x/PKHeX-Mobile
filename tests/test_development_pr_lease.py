"""Synthetic, network-free tests for read-only open-PR queue claim discovery."""
import copy
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import development_queue as dq
import development_pr_lease as lease


class OpenPrLeaseTests(unittest.TestCase):
    def setUp(self):
        self.queue = copy.deepcopy(dq.load(dq.QUEUE))
        self.task = next(t for t in self.queue["tasks"] if t["id"] == "QUEUE-RUN")
        self.task["status"] = "ready"
        self.task.pop("claim", None)
        self.claim = {"branch": "impl/pr-lease-test", "pr": 123, "base_sha": "a" * 40}
        self.pr = {"number": 123, "state": "open", "base": {"ref": "main"},
                   "head": {"ref": "impl/pr-lease-test", "sha": "b" * 40,
                            "repo": {"full_name": lease.REPO}}}
        self.branch = copy.deepcopy(self.queue)
        active = next(t for t in self.branch["tasks"] if t["id"] == "QUEUE-RUN")
        active["status"] = "in_progress"
        active["claim"] = self.claim

    def test_exact_same_repo_open_pr_claim_is_reserved(self):
        result = lease.collect(self.queue, [self.pr], lambda sha: self.branch)
        self.assertEqual({"repository": lease.REPO, "claims": [
            {"task_id": "QUEUE-RUN", **self.claim}]}, result)
        overlay = dq.overlay_pr_claims(self.queue, result)
        self.assertNotIn("QUEUE-RUN", {t["id"] for t in dq.select(overlay)["selected"]})
        self.assertEqual("ready", self.task["status"])

    def test_closed_fork_and_wrong_base_do_not_reserve(self):
        for patch in ({"state": "closed"},
                      {"base": {"ref": "release"}},
                      {"head": {**self.pr["head"], "repo": {"full_name": "fork/repo"}}}):
            pr = {**self.pr, **patch}
            self.assertEqual([], lease.collect(self.queue, [pr], lambda sha:
                self.fail("Unexpected branch queue read"))["claims"])

    def test_invalid_or_conflicting_claim_fails_closed(self):
        wrong = copy.deepcopy(self.branch)
        next(t for t in wrong["tasks"] if t["id"] == "QUEUE-RUN")["claim"]["base_sha"] = "main"
        with self.assertRaises(ValueError):
            lease.collect(self.queue, [self.pr], lambda sha: wrong)
        other = copy.deepcopy(self.pr)
        other["number"] = 124
        other["head"] = {**self.pr["head"], "ref": "impl/pr-lease-other", "sha": "c" * 40}
        other_branch = copy.deepcopy(self.branch)
        next(t for t in other_branch["tasks"] if t["id"] == "QUEUE-RUN")["claim"] = {
            "branch": "impl/pr-lease-other", "pr": 124, "base_sha": "c" * 40}
        with self.assertRaises(ValueError):
            lease.collect(self.queue, [self.pr, other], lambda sha:
                self.branch if sha == "b" * 40 else other_branch)

    def test_branch_head_sha_must_be_immutable(self):
        pr = copy.deepcopy(self.pr)
        pr["head"]["sha"] = "main"
        with self.assertRaises(ValueError):
            lease.collect(self.queue, [pr], lambda sha: self.branch)


if __name__ == "__main__":
    unittest.main()
