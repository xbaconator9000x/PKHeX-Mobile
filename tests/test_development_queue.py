import copy
import contextlib
import io
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import development_queue as dq


class QueueGates(unittest.TestCase):
    def setUp(self):
        self.queue = dq.load(dq.QUEUE)
        self.checkpoint = dq.load(dq.CHECKPOINT)

    def test_live_queue_valid(self):
        self.assertEqual([], dq.validate(self.queue, self.checkpoint))
        self.assertEqual(dq.select(self.queue, count=5), dq.select(self.queue, count=5))

    def test_live_queue_uses_reviewable_claim_schema(self):
        for task in self.queue["tasks"]:
            self.assertNotIn("claim_branch", task)
            self.assertNotIn("claim_base_sha", task)
            if task["status"] == "in_progress":
                self.assertIn("claim", task)

    def test_dependencies_and_cycle_fail_closed(self):
        for a, b in (("BASE-CORE", "not-a-task"), ("PORT-APK", "PORT-DEVICE")):
            q = copy.deepcopy(self.queue)
            next(x for x in q["tasks"] if x["id"] == a)["dependencies"].append(b)
            self.assertTrue(dq.validate(q, self.checkpoint))

    def test_priority_and_conflicting_locks(self):
        # An implementation PR may legitimately claim PORT-APK in_progress.
        # Exercise both states explicitly rather than requiring main's live queue
        # to remain unclaimed while the emulator PR is under review.
        q = copy.deepcopy(self.queue)
        apk = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        apk["status"] = "ready"
        selected = dq.select(q, count=25)["selected"]
        names = {x["id"] for x in selected}
        self.assertIn("PORT-APK", names)
        self.assertNotIn("PORT-DEVICE", names)
        self.assertNotIn("SAFE-EXPORT", names)
        locks = [l for x in selected for l in x["locks"]]
        self.assertEqual(len(locks), len(set(locks)))

        apk["status"] = "in_progress"
        claim = {"branch": "impl/port-apk-test", "pr": 83, "base_sha": "a" * 40}
        apk["claim"] = claim
        claimed = {x["id"] for x in dq.select(q, count=25)["selected"]}
        self.assertNotIn("PORT-APK", claimed)
        resumed = {x["id"] for x in dq.select(q, count=25, resume=True, claim=claim)["selected"]}
        self.assertIn("PORT-APK", resumed)

    def test_active_tasks_not_stolen(self):
        q = copy.deepcopy(self.queue)
        x = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        x["status"] = "in_progress"
        claim = {"branch": "impl/port-apk-test", "pr": 83, "base_sha": "a" * 40}
        x["claim"] = claim
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, count=25)["selected"]})
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, count=25, resume=True)["selected"]})
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, count=25, resume=True,
            claim={**claim, "pr": 84})["selected"]})
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, count=25, resume=True,
            claim={**claim, "base_sha": "b" * 40})["selected"]})
        self.assertIn("PORT-APK", {t["id"] for t in dq.select(q, count=25, resume=True,
            claim=claim)["selected"]})


    def test_claim_validation_rejects_stale_or_wrong_owner(self):
        q = copy.deepcopy(self.queue)
        task = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        task["status"] = "in_progress"
        task["claim"] = {"branch": "impl/emulator", "pr": 83, "base_sha": "a" * 40}
        self.assertEqual([], dq.validate(q, self.checkpoint))
        for changed in (
            {"branch": "audit/emulator"},
            {"pr": 0},
            {"pr": True},
            {"base_sha": "main"},
            {"base_sha": "A" * 40},
            {"branch": ""},
        ):
            bad = copy.deepcopy(q)
            next(t for t in bad["tasks"] if t["id"] == "PORT-APK")["claim"].update(changed)
            self.assertTrue(dq.validate(bad, self.checkpoint), changed)
        task["status"] = "ready"
        self.assertTrue(dq.validate(q, self.checkpoint))

    def test_active_claim_is_mandatory_and_legacy_fields_are_refused(self):
        q = copy.deepcopy(self.queue)
        task = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        task.pop("claim", None)  # Live queue may already have PR #83 claimed.
        task["status"] = "in_progress"
        self.assertTrue(any("requires an exact claim" in error
                            for error in dq.validate(q, self.checkpoint)))
        task["claim_branch"] = "impl/legacy"
        task["claim_base_sha"] = "a" * 40
        errors = dq.validate(q, self.checkpoint)
        self.assertTrue(any("unsupported task field claim_branch" in e for e in errors))
        self.assertTrue(any("unsupported task field claim_base_sha" in e for e in errors))
        task["claim"] = {"branch": "impl/reviewed", "pr": 106, "base_sha": "a" * 40}
        self.assertTrue(dq.validate(q, self.checkpoint))
        del task["claim_branch"]
        del task["claim_base_sha"]
        self.assertEqual([], dq.validate(q, self.checkpoint))
        task["unreviewed_extension"] = "unsafe"
        self.assertTrue(any("unsupported task field" in e
                            for e in dq.validate(q, self.checkpoint)))

    def test_resume_requires_full_explicit_claim(self):
        q = copy.deepcopy(self.queue)
        task = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        task["status"] = "in_progress"
        task["claim"] = {"branch": "impl/emulator", "pr": 83, "base_sha": "a" * 40}
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, resume=True, count=25)["selected"]})
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                dq.main(["next", "--resume"])
            with self.assertRaises(SystemExit):
                dq.main(["next", "--claim-branch", "impl/emulator"])
        # Other active tasks and their locks are never included in an exact resume.
        other = next(t for t in q["tasks"] if t["id"] == "QUEUE-RUN")
        other["status"] = "in_progress"
        other["claim"] = {"branch": "impl/queue-other", "pr": 91, "base_sha": "b" * 40}
        chosen = {t["id"] for t in dq.select(q, resume=True, count=25,
            claim=task["claim"])["selected"]}
        self.assertIn("PORT-APK", chosen)
        self.assertNotIn("QUEUE-RUN", chosen)

    def test_verified_requires_evidence_and_zero_remaining(self):
        q = copy.deepcopy(self.queue)
        x = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        x.pop("claim", None)  # A verified task cannot retain an active lease.
        x["status"] = "verified"
        x["remaining_hours"] = 0
        x["evidence"] = []
        self.assertTrue(dq.validate(q, self.checkpoint))
        x["evidence"].append("https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/1")
        self.assertFalse(dq.validate(q, self.checkpoint))

    def test_reject_checkpoint_and_unexplained_blockers(self):
        c = copy.deepcopy(self.checkpoint)
        c["main_sha"] = "main"
        self.assertTrue(dq.validate(self.queue, c))
        q = copy.deepcopy(self.queue)
        q["tasks"][3]["status"] = "blocked"
        self.assertTrue(dq.validate(q, self.checkpoint))

    def test_open_pr_overlay_reserves_ready_task_without_mutating_main(self):
        q = copy.deepcopy(self.queue)
        task = next(t for t in q["tasks"] if t["id"] == "QUEUE-RUN")
        task["status"] = "ready"
        task.pop("claim", None)
        original = copy.deepcopy(q)
        claim = {"branch": "impl/queue-lease-test", "pr": 120, "base_sha": "a" * 40}
        snapshot = {"repository": q["repository"],
                    "claims": [{"task_id": "QUEUE-RUN", **claim}]}
        overlaid = dq.overlay_pr_claims(q, snapshot)
        self.assertEqual(q, original)
        self.assertEqual([], dq.validate(overlaid, self.checkpoint))
        self.assertNotIn("QUEUE-RUN", {t["id"] for t in dq.select(overlaid)["selected"]})
        self.assertIn("QUEUE-RUN", {t["id"] for t in dq.select(overlaid, resume=True,
            claim=claim)["selected"]})

    def test_open_pr_overlay_fails_closed_on_conflicts_and_forgery(self):
        q = copy.deepcopy(self.queue)
        queue_task = next(t for t in q["tasks"] if t["id"] == "QUEUE-RUN")
        queue_task["status"] = "ready"
        queue_task.pop("claim", None)
        device = next(t for t in q["tasks"] if t["id"] == "PORT-DEVICE")
        device["status"] = "in_progress"
        device["claim"] = {"branch": "impl/owned-device", "pr": 118, "base_sha": "b" * 40}
        claim = {"task_id": "QUEUE-RUN", "branch": "impl/queue-lease-test",
                 "pr": 120, "base_sha": "a" * 40}
        for entry in ({**claim, "branch": "audit/not-ours"},
                      {**claim, "pr": True},
                      {**claim, "base_sha": "main"},
                      {**claim, "task_id": "MISSING"}):
            with self.assertRaises(ValueError):
                dq.overlay_pr_claims(q, {"repository": q["repository"],
                                                  "claims": [entry]})
        with self.assertRaises(ValueError):
            dq.overlay_pr_claims(q, {"repository": "other/repo", "claims": []})
        with self.assertRaises(ValueError):
            dq.overlay_pr_claims(q, {"repository": q["repository"],
                                              "claims": [claim, claim]})
        # An open PR must not override main's already-claimed device task.
        with self.assertRaises(ValueError):
            dq.overlay_pr_claims(q, {"repository": q["repository"],
                "claims": [{"task_id": "PORT-DEVICE", **{**claim,
                    "branch": "impl/other-device"}}]})

    def test_progress_does_not_claim_release(self):
        r = dq.report(self.queue, self.checkpoint)
        self.assertGreater(r["estimated_remaining_hours"], 0)
        self.assertLess(r["overall_estimated_percent"], 100)
        self.assertLessEqual(r["verified_effort_percent"], r["overall_estimated_percent"])


if __name__ == "__main__":
    unittest.main()
