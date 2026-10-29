# Persistent PKHeX Mobile GitHub work queue

GitHub main is authoritative: development/queue.json contains all owned tasks, dependency graph, ownership locks, engineering-hour estimates, blockers, acceptance criteria and links to verified evidence. development/checkpoint.json records a known main SHA and latest verified CI, advanced via reviewed Git changes. Audit findings remain in docs/UPSTREAM_* and docs/PARITY_*.

## Watchdog execution protocol

1. Reconcile fresh HEAD, open PRs, Actions statuses, latest source/audit work, queue and checkpoint before each run. Do not use chat memory as queue state.
2. Run python3 scripts/development_queue.py validate and python3 scripts/development_queue.py snapshot --batch-size 5. Resume only a self-owned in_progress claim with the exact recorded branch, PR number and immutable base SHA. Independently verify the live PR head, branch, base and lock before resuming; the local selector does not query GitHub. Never steal ambiguous work.
3. Pick the highest-priority dependency-eligible tasks with nonoverlapping lock keys. Prioritize substantial working PKForge adaptation and official pinned PKHeX.Core rather than another equivalent reimplementation. Validate source SHAs and licence/asset rights before importing.
4. Claim tasks in PR-backed Git commits before parallel edits. Maintain assigned owner, task locks and acceptance gates. Use separate branches, expected-SHA updates and no force push to main. Implementation owns src/tests/scripts/build and queue; audit owns docs/UPSTREAM_* and docs/PARITY_*.
5. Add meaningful tests and run relevant Foundation/official-Core/Android workflows. Inspect exact-head results and source/asset integrity. Do not mark verified because code compiled or a draft PR was created. Failed work stays on its branch; record a blocker and proceed to other eligible work.
6. Merge only safely verified PRs at fresh HEAD. Advance checkpoint and verified task evidence in a subsequent coordinated PR if necessary. CI is a read-only validation and selector, NOT an autonomous coder or unconditional merger. No automation may authorize save edits based only on file extension or known ROM header.
7. Continue through independent eligible batches until the execution budget expires. Persist checkpoint/PR state before ending and resume next hourly run. Do not create competing watchdogs.

## Tracking definitions

- ready = dependency/lock eligible candidate; in_progress = GitHub PR/branch claim; blocked = explicit evidence-based blocker; verified = gates met with GitHub evidence and zero estimated remaining work.
- total_hours and remaining_hours are planning forecasts, not elapsed logged hours. Percentages are weighted engineering effort; strict verified-only effort is separately reported.
- Functional parity estimator covers CORE-, HACK- and POKEBACON-prefixed task groups and is NOT certified game compatibility. Report build, Android device and release readiness independently.
- A rights-cleared diagnostic build does not certify an adapted editor. Never ship non-commercial fonts/third-party art, licensed ROM assets or private data.
- Save writes remain prohibited for unsupported hacks until independently authenticated per-format fixtures, backups, staged round trips and recovery prove safety.

## Commands

python3 scripts/development_queue.py validate
python3 scripts/development_queue.py report
python3 scripts/development_queue.py next --owner implementation --batch-size 5
python3 scripts/development_queue.py next --owner audit --batch-size 3
python3 scripts/development_queue.py snapshot --output work-queue-snapshot.json

Queue state is changed by code-reviewed Git commits only. A scheduled coding watchdog must create/merge those commits after actual tests rather than letting Actions silently rewrite main.

## Exact-claim resume (fail-closed)

A task may include a `claim` object **only while `status: in_progress`**,
with `branch` (prefix `impl/` or `audit/` matching owner), positive numeric
`pr`, and immutable 40-hex `base_sha`. A generic `--resume` is refused.
After checking the current PR head/base, author, branch and task lock in
GitHub, use the exact recorded claim:

```sh
python3 scripts/development_queue.py next --owner implementation --batch-size 5 \
  --resume --claim-branch impl/example --claim-pr 123 \
  --claim-base-sha 0123456789abcdef0123456789abcdef01234567
```

The selector compares claim values but **does not authenticate GitHub state**:
a matching queue entry alone is insufficient authorization to push, merge or
override another worker. Missing claim metadata on an older in-progress task
means it cannot be resumed automatically until reconciled in a reviewed PR.
Release the claim when moving the task back to ready, blocked or verified.
This change does not schedule work, create agents, or cause GitHub Actions to
write code.
