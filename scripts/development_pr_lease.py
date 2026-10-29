#!/usr/bin/env python3
"""Read-only GitHub PR claim snapshot for deterministic queue selection.

Only an exact claim found in an open same-repository PR's pinned head queue
can reserve a task not yet claimed on main. This never writes GitHub state,
creates work, or authorizes a save operation. API failures fail closed.
"""
import argparse
import base64
import json
import os
from pathlib import Path
import re
from urllib.request import Request, urlopen

REPO = "xbaconator9000x/PKHeX-Mobile"
SHA = re.compile(r"^[0-9a-f]{40}$")
API = "https://api.github.com"


def api_json(path: str, token: str):
    headers = {"Accept": "application/vnd.github+json",
               "User-Agent": "PKHeX-Mobile-readonly-queue"}
    if token:
        headers["Authorization"] = "Bearer " + token
    request = Request(API + path, headers=headers)
    with urlopen(request, timeout=20) as response:
        data = response.read(2_000_001)
    if len(data) > 2_000_000:
        raise RuntimeError("GitHub queue metadata exceeds size bound")
    return json.loads(data)


def read_branch_queue(sha: str, token: str) -> dict:
    if not isinstance(sha, str) or not SHA.fullmatch(sha):
        raise ValueError("Invalid immutable PR head SHA")
    doc = api_json(f"/repos/{REPO}/contents/development/queue.json?ref={sha}", token)
    if doc.get("encoding") != "base64" or not isinstance(doc.get("content"), str):
        raise RuntimeError("PR queue is not a base64 GitHub file")
    data = base64.b64decode(doc["content"], validate=False)
    if len(data) > 1_000_000:
        raise RuntimeError("PR queue exceeds size bound")
    return json.loads(data)


def collect(main_queue: dict, prs: list, branch_queue_reader) -> dict:
    if main_queue.get("repository") != REPO:
        raise ValueError("Main queue repository mismatch")
    main = {task["id"]: task for task in main_queue["tasks"]}
    reservations = {}
    for pr in prs:
        if pr.get("state") != "open" or pr.get("base", {}).get("ref") != "main":
            continue
        head = pr.get("head") or {}
        if (head.get("repo") or {}).get("full_name") != REPO:
            continue  # Do not trust a fork to reserve repository locks.
        number, branch, sha = pr.get("number"), head.get("ref"), head.get("sha")
        if type(number) is not int or number <= 0 or not isinstance(branch, str) or not SHA.fullmatch(sha or ""):
            raise ValueError("Malformed same-repository PR identity")
        branch_queue = branch_queue_reader(sha)
        if branch_queue.get("repository") != REPO:
            raise ValueError("PR branch queue repository mismatch")
        for task in branch_queue.get("tasks", []):
            if task.get("status") != "in_progress":
                continue
            claim = task.get("claim")
            if not isinstance(claim, dict):
                continue
            if claim.get("pr") != number or claim.get("branch") != branch:
                continue
            base_sha = claim.get("base_sha")
            if not isinstance(base_sha, str) or not SHA.fullmatch(base_sha):
                raise ValueError("Malformed open PR claim base SHA")
            ident = task.get("id")
            if ident not in main:
                raise ValueError("Open PR claims unknown task")
            authoritative = main[ident]
            if authoritative["owner"] != task.get("owner"):
                raise ValueError("Open PR claim owner mismatch")
            if authoritative["status"] == "verified":
                continue  # A stale PR cannot reopen a completed task.
            if authoritative["status"] == "blocked":
                raise ValueError("Open PR claims a blocked task")
            if (authoritative["status"] == "in_progress"
                    and authoritative.get("claim") != claim):
                raise ValueError("Open PR conflicts with main's active claim")
            if ident in reservations and reservations[ident] != claim:
                raise ValueError("Two open PRs claim the same task")
            reservations[ident] = claim
    return {"repository": REPO, "claims": [
        {"task_id": ident, **claim}
        for ident, claim in sorted(reservations.items())
    ]}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--queue", type=Path, default=Path("development/queue.json"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    token = os.environ.get("GITHUB_TOKEN", "")
    main_queue = json.loads(args.queue.read_text(encoding="utf-8"))
    prs = []
    for page in range(1, 6):
        batch = api_json(f"/repos/{REPO}/pulls?state=open&per_page=100&page={page}", token)
        if not isinstance(batch, list):
            raise RuntimeError("Invalid GitHub open-PR listing")
        prs.extend(batch)
        if len(batch) < 100:
            break
    else:
        raise RuntimeError("Too many open PRs for bounded queue scan")
    snapshot = collect(main_queue, prs, lambda sha: read_branch_queue(sha, token))
    args.output.write_text(json.dumps(snapshot, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Read-only PR claim scan: {len(prs)} open PRs, {len(snapshot['claims'])} reserved tasks.")


if __name__ == "__main__":
    main()
