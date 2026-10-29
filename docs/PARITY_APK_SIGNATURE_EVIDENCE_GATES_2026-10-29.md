# Diagnostic APK signer and exact-file acceptance gates (2026-10-29)

Paired source handoff: [UPSTREAM_APK_SIGNATURE_EVIDENCE_HANDOFF_2026-10-29.md](UPSTREAM_APK_SIGNATURE_EVIDENCE_HANDOFF_2026-10-29.md). Baseline main `82223c466f4de383b25e0b4c5080b4960782015b`. **All gates below are proposals, not tests executed by this audit.** They do not authorize APK distribution, writes or desktop parity claims.

| Gate | Priority | Reproducible execution / expected result |
| --- | --- | --- |
| SIG-G01 | P1 | Produce a rights-neutral diagnostic Android APK in the pinned official-Core job. Run Android SDK `apksigner verify --verbose --print-certs <exact-selected-apk>`; require exit zero, at least one verified signature scheme and parseable signer certificate SHA-256. Record APK SHA-256, source SHA and signer digest together. |
| SIG-G02 | P1 | Copy a synthetically generated *unsigned* APK to a filename ending `-Signed.apk` and generate a matching synthetic selector report. The existing selection-unit test may accept it as a byte-identical fixture, but the new **integration signer gate must reject it** before `adb install`. Never substitute real user saves or ROMs. |
| SIG-G03 | P1 | Sign a synthetic APK with a throwaway test key, then change a signed ZIP entry without resigning while retaining the `-Signed.apk` suffix. Require signer verification failure even if the test harness refreshes filename/size/hash metadata. |
| SIG-G04 | P1 | After successful selection and signature verification, mutate or replace the on-disk APK before install (same size is sufficient). Require the existing `verify_selected_apk` or equivalent immediate SHA-256 recheck to fail and assert that `adb install` was **not** invoked. |
| SIG-G05 | P1 | Run the rebased emulator job on the current main-derived SHA: assert the signed APK's selected SHA-256 equals the signature-verified file and the installed file; assert actual foreground `org.pkforge.app.debug` Activity, not just PID. Keep evidence ephemeral and do not upload the APK. |
| SIG-G06 | P0 release | Separately from diagnostic CI, prove approved release signing-key fingerprint, signer rotation policy, corresponding source/GPL and packaged-asset rights. A debug certificate digest, successful emulator install or `-Signed.apk` suffix **must not** satisfy release certification. |

**Existing executed evidence (not re-executed here):** [PR #111](https://github.com/xbaconator9000x/PKHeX-Mobile/pull/111) implemented signed-*filename* selection, atomic output and negative synthetic tests; [PR #112](https://github.com/xbaconator9000x/PKHeX-Mobile/pull/112) recorded passing [Foundation](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40858618642) and [queue](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40858618604) checks on merge `82223c466f4de383b25e0b4c5080b4960782015b`. Those tests are not Android cryptographic-signature tests.

**Ownership:** implementation owns test/script/workflow changes and any queue/checkpoint update. Audit owns this documentation only. Continue `AUD-ADOPT` only for changed source or independently verified new evidence; do not repeatedly review stable upstream modules.
