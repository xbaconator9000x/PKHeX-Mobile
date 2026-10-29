# Android provider, lifecycle, reproducibility and UI parity gates — pass D (2026-10-28)

**Status: all 22 rows proposed, NOT EXECUTED in this audit.** Use with [pass D source findings](UPSTREAM_ANDROID_PROTOTYPE_REVIEW_2026-10-28.md), [pass C container/SAF/bank gates](PARITY_IO_BANK_GATES_2026-10-28.md) and [roadmap](ROADMAP.md). The source-based CI result for the current Android prototype is **compile-passed**, not device-passed. Implementation owns code/tests; audit owns this matrix.

## Evidence protocol

For each row record: test ID; commit SHA; exact PKHeX.Core package version and resolved package hash; APK SHA-256/build manifest; Android API/ABI, device or emulator model; provider package/version and URI capabilities (redact actual private URI); synthetic/consented fixture provenance and SHA-256 (keep fixtures private); test steps; expected/actual result; logcat excerpt **redacted of personal data**; screenshots only when they contain no user save details; rerun/recovery result. For parser parity, compare with desktop using **the same PKHeX.Core 26.10.18**, not the newer official audit head or the candidates' different engines.

Evidence tiers: `source-inspected`, `unit-passed`, `desktop-compared`, `emulator-passed`, `device-passed`, `write-certified`, `blocked`. A source or compile check never counts as a device result. All writing remains disabled.

## Provider identity and read-only behavior — 8 cases

| ID | Priority | Input/injection | Expected observation | Evidence |
| --- | --- | --- | --- | --- |
| D-01 | P1 | Android local DocumentsProvider returns valid, Core-generated synthetic save through `ACTION_OPEN_DOCUMENT` | Read-only trainer/party/box projection matches pinned Core baseline; original SHA unchanged | emulator/device |
| D-02 | P1 | Cloud provider streams slowly, returns short reads, non-seekable descriptor | No UI-thread blocking; bounded memory; eventual correct projection or clean refusal | device |
| D-03 | P1 | Provider returns different bytes on second open under same URI/document ID | Fingerprint mismatch, no projection displayed, no source mutation | provider fault |
| D-04 | P1 | Provider silently swaps underlying same-byte document behind unchanged URI | Test must **not** assert replacement detection from URI-derived identity alone; record limitation and no write authorization | provider fault |
| D-05 | P1 | Provider returns different authority/document ID with identical bytes between opens | Identity-aware session rejects mismatch; no projection | unit/provider |
| D-06 | P1 | Provider revokes read grant or throws on second open | Clean refusal, no cached stale result or crash; no write permission requested | device/fault |
| D-07 | P1 | Zero-length, truncated, 32 MiB boundary and over-limit streams | Bounded, deterministic refusal; stream handles closed on all exits | unit/device |
| D-08 | P1 | Retail-shaped Gen 3 hack without known negative signature | No claim of verified vanilla identity or write compatibility, even if Core projects data | desktop/negative |

## Activity lifecycle and cancellation — 6 cases

| ID | Priority | Action/failure injection | Expected observation | Evidence |
| --- | --- | --- | --- | --- |
| D-09 | P1 | Rotate Activity during picker and during slow parse | No old Activity publishes result; no unhandled exception; explicit resumption/reselect behavior | emulator |
| D-10 | P1 | Home, Back, and app task removal during second provider read | Cancellation/cleanup; no leaked descriptor or stale UI; predictable Back | emulator/device |
| D-11 | P1 | Rapid repeated picker taps, cancel picker, then select a different save | Only latest intentional selection shown; no duplicate pending picker/old projection | emulator |
| D-12 | P1 | Kill process after picker result and reopen app | No false retained URI permission, stale session, or restored private save bytes | emulator |
| D-13 | P2 | Slow cloud provider blocks then user cancels | Explicit cancellation control; provider stream disposed; UI usable afterward | device |
| D-14 | P2 | Permission failure, malformed bytes, I/O timeout and parser rejection | Distinct privacy-safe errors; no raw URI/trainer/save bytes in logs or telemetry | emulator/unit |

## Mobile UI and parity — 4 cases

| ID | Priority | Test | Expected observation | Evidence |
| --- | --- | --- | --- | --- |
| D-15 | P1 | Synthetic full party, sparse boxes and max 64 × 64 reported layout | Correct 0-based source-to-UI slot mapping, bounded/virtualized rendering, no long unresponsive text dump | emulator/perf |
| D-16 | P1 | Same-version desktop projection for Gen 1/3/4/5/6/7/8/9 consented fixtures (one per claimed format) | Trainer IDs, party/box counts, species, nickname, egg/shiny and slot indexes match independently; unknown profiles remain provisional | desktop/device |
| D-17 | P2 | TalkBack, font scaling 200%, keyboard/D-pad, portrait/landscape and small screen | Navigable logical labels/order, no clipped actions, sensible focus and scrolling | device/accessibility |
| D-18 | P2 | API 26/29/33/35 or 36, local and cloud provider, phone and tablet | Explicit tested support matrix; do not infer compatibility from `SupportedOSPlatformVersion` | device |

## Build and provenance — 4 cases

| ID | Priority | Trigger or build | Expected observation | Evidence |
| --- | --- | --- | --- | --- |
| D-19 | P1 | PR changes **only** `src/PKHeXMobile.ReadOnly/**` | Android workflow is scheduled after path-filter fix; foundation tests still run | CI |
| D-20 | P1 | PR changes **only** `src/PKHeXMobile.Domain/**` or dependency/build pin | Android compile is scheduled for every transitive input; no silent green without compilation | CI |
| D-21 | P1 | Clean build on two runners from locked SDK/workload/NuGet inputs | Record exact versions, dependency hashes, build provenance and APK SHA; explain any non-reproducible bytes | CI |
| D-22 | P1 | Install debug APK and launch on emulator, open picker, cancel and return | App installs, opens and survives picker/Back; record emulator API/ABI and result separately from compile | emulator |

**Count check:** 8 provider + 6 lifecycle + 4 UI + 4 build = **22** scenarios. These do not replace pass C's 25 scenarios (save envelopes, bank transactions and write safety). A scenario's inclusion here does not mean its code exists or has passed.

## Execution order

1. **Fix the Android workflow trigger gap** (D-19/D-20) before additional ReadOnly/Domain edits; otherwise successful foundation CI can mask an Android compile regression.
2. Add a local fake DocumentsProvider/emulator test harness (D-01, D-03, D-05–D-07, D-09–D-12, D-22). Keep fixture bytes synthetic or private.
3. Run cloud provider and actual device tests (D-02, D-04, D-06, D-13, D-18); record exact provider behavior, not blanket SAF guarantees.
4. Implement an accessible, virtualized read-only party/box browser (D-15/D-17) and independently verify every claimed format against same-version desktop (D-16).
5. Lock toolchain and artifact provenance (D-21), preserve GPL and third-party rights; only then consider a formal Android architecture gate. **Do not enable Save As, in-place writes, banks, custom hack editing or PokéBacon profiles from these tests.**

Current status: source evidence + successful compile at `7ad00f492982`; **0/22 device/CI acceptance rows independently executed by this audit**. Milestone 0 architecture and Milestone 1 independent read-only certification remain open.
