# Diagnostic APK signing evidence: minimal integration handoff (2026-10-29)

**Scope:** `AUD-ADOPT`; source-only delta at main `82223c466f4de383b25e0b4c5080b4960782015b`. No application, workflow, queue or README changes in this audit. This is a narrow addition to the already-merged source-freeze, signed-filename and APK-hash gates, **not** a new adoption matrix.

## Exact evidence

1. `scripts/pkforge_emulator_apk_selection.py:126-139` requires exactly one attested filename ending `-Signed.apk`, matching application ID, payload flag, SHA-256 and size. `:141-157` rejects duplicates, symlinks and changed bytes. `:170-182` rechecks before installation. **These checks establish byte identity, not a verified Android signing certificate.** The tests at `tests/test_pkforge_emulator_apk_selection.py:25-27` deliberately use arbitrary synthetic bytes named `PKForge-Signed.apk`; that is appropriate for selection unit tests, but does not test signing.
2. `scripts/pkforge_artifact_attestation.py:127-233` checks APK ZIP structure, managed/runtime payload and `aapt2 dump badging` package identity; `:422-459` inventories APKs and reports source/asset metadata. No `apksigner verify` or certificate digest is recorded in the report. The existing `--snapshot-only` and source-unchanged checks are valuable and must be retained.
3. `.github/workflows/pkforge-official-core-probe.yml:311-350` compiles a Debug APK, checks offline permissions, Android backup exclusions and neutral media, then uploads **provenance JSON only**. The workflow does not verify APK signature schemes or signer identity before recording metadata. It also does not install an APK on an emulator in current main.
4. The still-open draft [#83](https://github.com/xbaconator9000x/PKHeX-Mobile/pull/83), head `29c85858ca941e8c92968e0f38c971f4aa34e081`, installs the first matching APK with an unsigned fallback. This draft is older than merged selector PRs #103, #106, #109 and #111; do not merge its old queue/workflow hunks. Previous audits already documented its first-match, stale-claim and foreground-Activity problems; **do not re-audit those unchanged defects**.

## Integration decision and shortest safe implementation

**ADOPT:** pinned PKForge Android MAUI build and official Core `26.10.18`, current source-freeze, compiled backup/export refusal, APK structural inventory, exact SHA-256 selector and atomic selection output. Pinned PKForge `2df04430e532b78c247f45b936f3068d8f824987`, PKHeX MAUI reference `a6f7cfb78e1c96155baa67660fd7ae97895e375f` and official Core source `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea` remain unchanged.

**ADAPT:** for the ephemeral emulator job, after attestation and immediately before install, use Android SDK `apksigner verify --verbose --print-certs` against the **same hash-selected APK**, require verification success and a parsed signer certificate digest, and record the digest with the exact APK SHA-256 and GitHub source SHA in ephemeral test evidence. Do not confuse this with a trusted release signer: a CI-generated debug key is not a stable release identity. Prefer invoking the verifier in the integration script so the selected file, signature result and `adb install` cannot silently diverge. Rehash immediately before installation; if APK path or digest differs, fail. Retain existing original-save write denial, rights exclusions and provenance-only artifact policy.

**REJECT:** relying on the suffix `-Signed.apk`, `aapt2 dump badging`, successful `adb install`, or arbitrary signing-key metadata as proof of an approved release signature. Android package manager enforces signature validity during installation, but that is not independent build-provenance evidence and does not establish trusted release key custody.

## Priority and ownership

**P1 diagnostic evidence gap; P0 before any signed release claim.** No compromised, unsigned or malicious APK was observed; no device run was performed. The existing protections against full-save sharing and cloud/device-transfer backup remain source/compiled-verified at earlier CI SHAs, not physically tested. Implementation owner should add only the signature verification and exact-file evidence to the rebased emulator integration PR; do not duplicate an emulator workflow or change the audit-owned docs. The audit owner does not touch scripts, tests, workflows or queue.

**Still required:** emulator foreground Activity, SAF provider cancellation/rotation, actual Android backup/restore, independent retail-save fixtures, safe Save As, licence/asset clearance and desktop parity. These are separate release gates.
