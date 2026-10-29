#!/usr/bin/env python3
"""Fail-closed, deterministic, non-mutating development queue validator/selector."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
QUEUE = ROOT / "development/queue.json"
CHECKPOINT = ROOT / "development/checkpoint.json"
PRIORITY = {"P0": 0, "P1": 1, "P2": 2, "P3": 3}
STATES = {"ready", "in_progress", "blocked", "verified"}
OWNERS = {"implementation", "audit"}
KEYS = {"id", "title", "owner", "priority", "status", "total_hours",
        "remaining_hours", "dependencies", "locks", "acceptance",
        "evidence", "blockers"}


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def validate(q, c):
    errors = []
    if q.get("schema_version") != 1 or c.get("schema_version") != 1:
        errors.append("unsupported schema")
    if q.get("repository") != "xbaconator9000x/PKHeX-Mobile" or q.get("repository") != c.get("repository"):
        errors.append("repository mismatch")
    sha = c.get("main_sha", "")
    if not isinstance(sha, str) or len(sha) != 40 or any(x not in "0123456789abcdef" for x in sha):
        errors.append("invalid immutable checkpoint SHA")
    tasks = q.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        return errors + ["empty or invalid task array"]
    ids = {}
    for task in tasks:
        if not isinstance(task, dict):
            errors.append("non-object task")
            continue
        ident = task.get("id")
        if not isinstance(ident, str) or not ident or ident in ids:
            errors.append("empty or duplicate ID " + repr(ident))
            continue
        ids[ident] = task
        for key in KEYS - task.keys():
            errors.append(ident + ": missing " + key)
        # Schema 1 is strict: legacy claim_branch/claim_base_sha keys must
        # never silently bypass the reviewed branch/PR/base-SHA lease.
        for key in sorted(task.keys() - KEYS - {"claim"}):
            errors.append(ident + ": unsupported task field " + key)
        if task.get("owner") not in OWNERS or task.get("status") not in STATES or task.get("priority") not in PRIORITY:
            errors.append(ident + ": invalid owner/status/priority")
        total, remain = task.get("total_hours"), task.get("remaining_hours")
        if (type(total) not in (int, float) or type(remain) not in (int, float)
                or total <= 0 or remain < 0 or remain > total):
            errors.append(ident + ": invalid engineering estimate")
        for key in ("dependencies", "locks", "acceptance", "evidence", "blockers"):
            v = task.get(key)
            if not isinstance(v, list) or any(not isinstance(x, str) or not x for x in v):
                errors.append(ident + ": invalid " + key)
            elif len(v) != len(set(v)):
                errors.append(ident + ": duplicate " + key)
        if not task.get("locks") or not task.get("acceptance"):
            errors.append(ident + ": locks and tests are mandatory")
        claim = task.get("claim")
        if task.get("status") == "in_progress" and claim is None:
            errors.append(ident + ": active task requires an exact claim")
        if claim is not None:
            if task.get("status") != "in_progress":
                errors.append(ident + ": only active tasks may hold a claim")
            if not isinstance(claim, dict) or set(claim) != {"branch", "pr", "base_sha"}:
                errors.append(ident + ": invalid claim structure")
            else:
                branch, pr, base_sha = claim["branch"], claim["pr"], claim["base_sha"]
                prefix = "impl/" if task.get("owner") == "implementation" else "audit/"
                if not isinstance(branch, str) or not branch.startswith(prefix) or len(branch) <= len(prefix):
                    errors.append(ident + ": claim branch/owner mismatch")
                if type(pr) is not int or pr <= 0:
                    errors.append(ident + ": claim PR must be positive")
                if not isinstance(base_sha, str) or len(base_sha) != 40 or any(x not in "0123456789abcdef" for x in base_sha):
                    errors.append(ident + ": claim base SHA must be immutable")
        if task.get("status") == "verified":
            if remain != 0 or not task.get("evidence"):
                errors.append(ident + ": verified requires zero remaining and evidence")
            elif any(not x.startswith("https://github.com/") for x in task["evidence"]):
                errors.append(ident + ": evidence must be a GitHub permalink")
        if task.get("status") == "blocked" and not task.get("blockers"):
            errors.append(ident + ": blocked task requires reason")
    for task in ids.values():
        for dep in task.get("dependencies", []):
            if dep not in ids or dep == task["id"]:
                errors.append(task["id"] + ": unknown/self dependency " + dep)
    visited, visiting = set(), set()
    def dfs(ident):
        if ident in visiting:
            errors.append("dependency cycle at " + ident)
            return
        if ident in visited:
            return
        visiting.add(ident)
        for dep in ids[ident].get("dependencies", []):
            if dep in ids and dep != ident:
                dfs(dep)
        visiting.remove(ident)
        visited.add(ident)
    for ident in ids:
        dfs(ident)
    active = [t for t in ids.values() if t.get("status") == "in_progress"]
    locks = set()
    for t in active:
        overlap = locks.intersection(t.get("locks", []))
        if overlap:
            errors.append(t["id"] + ": active lock collision " + repr(sorted(overlap)))
        locks.update(t.get("locks", []))
    return errors


def select(q, owner="implementation", count=5, resume=False, claim=None):
    # A generic --resume must never steal another PR's active queue lock.
    # The caller must also verify the PR's current head and branch in GitHub;
    # the selector only compares the recorded immutable claim tuple.
    def is_owned_claim(t):
        return (resume and claim is not None and t["owner"] == owner
                and t["status"] == "in_progress" and t.get("claim") == claim)
    by_id = {x["id"]: x for x in q["tasks"]}
    reserved = {l for t in q["tasks"] if t["status"] == "in_progress"
                and not is_owned_claim(t) for l in t["locks"]}
    candidates = [t for t in q["tasks"]
                  if t["owner"] == owner
                  and (t["status"] == "ready" or is_owned_claim(t))
                  and all(by_id[d]["status"] == "verified" for d in t["dependencies"])]
    candidates.sort(key=lambda t: (t["status"] != "in_progress",
                                   PRIORITY[t["priority"]], -t["remaining_hours"], t["id"]))
    chosen = []
    for t in candidates:
        if len(chosen) >= count:
            break
        if reserved.intersection(t["locks"]):
            continue
        chosen.append(t)
        reserved.update(t["locks"])
    deferred = []
    for t in q["tasks"]:
        if t["owner"] != owner or t["status"] == "verified" or t in chosen:
            continue
        unmet = [d for d in t["dependencies"] if by_id[d]["status"] != "verified"]
        reason = ("blocked: " + ", ".join(t["blockers"]) if t["status"] == "blocked"
                  else "dependencies: " + ", ".join(unmet) if unmet
                  else "already claimed" if t["status"] == "in_progress"
                  else "conflicting path ownership")
        deferred.append({"id": t["id"], "reason": reason})
    return {"selected": chosen, "deferred": deferred}


def report(q, c):
    tasks = q["tasks"]
    total = sum(t["total_hours"] for t in tasks)
    remaining = sum(t["remaining_hours"] for t in tasks)
    verified = sum(t["total_hours"] for t in tasks if t["status"] == "verified")
    parity = [t for t in tasks if t["id"].startswith(("CORE-", "HACK-", "POKEBACON"))]
    ptotal = sum(t["total_hours"] for t in parity)
    pleft = sum(t["remaining_hours"] for t in parity)
    return {"overall_estimated_percent": round(100 * (total - remaining) / total, 1),
            "verified_effort_percent": round(100 * verified / total, 1),
            "functional_parity_estimate_percent": round(100 * (ptotal - pleft) / ptotal, 1) if ptotal else None,
            "estimated_total_hours": total, "estimated_remaining_hours": remaining,
            "last_verified_main": c["main_sha"],
            "statuses": {s: sum(t["status"] == s for t in tasks) for s in sorted(STATES)},
            "warning": "Estimated future engineering hours, not tracked elapsed hours or authenticated feature parity."}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate", "report", "next", "snapshot"))
    parser.add_argument("--owner", choices=sorted(OWNERS), default="implementation")
    parser.add_argument("--batch-size", type=int, default=5)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--claim-branch")
    parser.add_argument("--claim-pr", type=int)
    parser.add_argument("--claim-base-sha")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    if not 1 <= args.batch_size <= 25:
        parser.error("batch size must be 1..25")
    fields = (args.claim_branch, args.claim_pr, args.claim_base_sha)
    if args.resume and any(x is None for x in fields):
        parser.error("--resume requires --claim-branch, --claim-pr and --claim-base-sha")
    if not args.resume and any(x is not None for x in fields):
        parser.error("claim identifiers are only valid with --resume")
    claim = ({"branch": args.claim_branch, "pr": args.claim_pr,
              "base_sha": args.claim_base_sha} if args.resume else None)
    q, c = load(QUEUE), load(CHECKPOINT)
    errors = validate(q, c)
    if errors:
        for error in errors:
            print("QUEUE ERROR: " + error, file=sys.stderr)
        return 1
    if args.command == "validate":
        obj = {"valid": True, "task_count": len(q["tasks"]), "queue_sha256": hashlib.sha256(QUEUE.read_bytes()).hexdigest()}
    elif args.command == "report":
        obj = report(q, c)
    elif args.command == "next":
        obj = select(q, args.owner, args.batch_size, args.resume, claim)
    else:
        obj = {"progress": report(q, c),
               "implementation": select(q, "implementation", args.batch_size, args.resume, claim),
               "audit": select(q, "audit", args.batch_size, args.resume, claim)}
    content = json.dumps(obj, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(content, encoding="utf-8")
    print(content, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
