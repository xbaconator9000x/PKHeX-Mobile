# Temporary GitHub Actions hold — 2026-10-30

User requested that this project pause to avoid unintended GitHub resource/API usage.
All automatic `push`, `pull_request`, and scheduled workflow triggers on main have been removed in this commit.
Workflows remain as source files and may be launched only via explicit `workflow_dispatch`.
Existing source files, task queues, open PRs, releases, and historical runs are unchanged.

## Resuming
Restore the workflow trigger definitions from the parent commit of this hold commit, or revert this hold commit (taking care not to revert later unrelated changes).
Before resuming, review GitHub Actions billing and limits, any OPENAI_API_KEY secret use, and the frequency/expected cost of builds.

## Changed workflow triggers
- `.github/workflows/android-build.yml`: automatic events removed
- `.github/workflows/development-queue.yml`: automatic events removed
- `.github/workflows/pkforge-build-spike.yml`: automatic events removed
- `.github/workflows/pkforge-official-core-probe.yml`: automatic events removed
- `.github/workflows/repository-validation.yml`: automatic events removed

Note: GitHub Actions jobs that were already running before the hold may finish. Other repositories are not affected.
