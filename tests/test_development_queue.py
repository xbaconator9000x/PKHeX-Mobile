import copy
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
        claimed = {x["id"] for x in dq.select(q, count=25)["selected"]}
        self.assertNotIn("PORT-APK", claimed)
        resumed = {x["id"] for x in dq.select(q, count=25, resume=True)["selected"]}
        self.assertIn("PORT-APK", resumed)

    def test_active_tasks_not_stolen(self):
        q = copy.deepcopy(self.queue)
        x = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
        x["status"] = "in_progress"
        self.assertNotIn("PORT-APK", {t["id"] for t in dq.select(q, count=25)["selected"]})
        self.assertIn("PORT-APK", {t["id"] for t in dq.select(q, count=25, resume=True)["selected"]})

    def test_verified_requires_evidence_and_zero_remaining(self):
        q = copy.deepcopy(self.queue)
        x = next(t for t in q["tasks"] if t["id"] == "PORT-APK")
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

    def test_progress_does_not_claim_release(self):
        r = dq.report(self.queue, self.checkpoint)
        self.assertGreater(r["estimated_remaining_hours"], 0)
        self.assertLess(r["overall_estimated_percent"], 100)
        self.assertLessEqual(r["verified_effort_percent"], r["overall_estimated_percent"])


if __name__ == "__main__":
    unittest.main()
