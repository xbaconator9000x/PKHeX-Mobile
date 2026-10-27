# Android follow-up execution gates — pass E (2026-10-28)

**24 cases specified; 0/24 executed by this source-only audit.** Companion [pass E findings](UPSTREAM_ANDROID_FOLLOWUP_REVIEW_2026-10-28.md). The earlier [22-case device matrix](PARITY_ANDROID_DEVICE_GATES_2026-10-28.md) remains active; this is a targeted delta, not an additive claim that all cases are unique. CI success at the pinned implementation SHA proves the configured jobs passed, not these fault-injection/device/desktop scenarios.

## Evidence and release protocol

Each result must record: case ID, repo commit, pinned Core package and resolved hash, APK hash when relevant, .NET SDK/Android workload/Java versions, device API/ABI and provider version, sanitized provider capabilities (never a private URI), private fixture provenance/hash, independent same-version desktop expected fields, steps, actual result, redacted logs, recovery result and evidence tier. Keep ROMs, personal saves, private keys, proprietary artwork and fixtures outside public Git history. Tiers: source-inspected, unit-passed, desktop-compared, emulator-passed, device-passed, write-certified, blocked. A smoke test generated and verified by the same Core is not an independent desktop baseline.

## 24 targeted acceptance cases

| Group | ID | Priority | Input or failure injection | Expected acceptance result | Required evidence | Status |
| --- | --- | --- | --- | --- | --- | --- |
| CI | E-01 | P1 | PR touching only ReadOnly source | Android workflow actually starts and builds; record run SHA and job conclusion | CI | not executed |
| CI | E-02 | P1 | PR touching only Domain source | Android workflow starts; no transitive dependency gap | CI | not executed |
| CI | E-03 | P1 | PR changing only Core package pin/build props | Android build scheduled; resolved package version and lockfile checked | CI | not executed |
| CI | E-04 | P1 | Two clean runner builds from pinned SDK/workloads/dependencies | Record resolved versions and hashes; compare APK contents and explain byte differences | CI | not executed |
| CI | E-05 | P1 | Install uploaded debug APK on API 26 and 35/36 emulator | Launch, picker-cancel, Back, rotation; distinguish install success from compile | emulator | not executed |
| Provider | E-06 | P1 | Fake provider blocks synchronously in OpenInputStream | Cancel pressed; measure actual delay, no false prompt-cancellation claim, eventual resource cleanup | instrumented | not executed |
| Provider | E-07 | P1 | Provider blocks during first and second ReadAsync | Cancellation/revocation yields no stale projection, streams disposed, UI recoverable | instrumented | not executed |
| Provider | E-08 | P1 | Provider throws Java.Lang.SecurityException / malformed URI | No crash, no private URI leakage; distinguish access denied where safe | instrumented | not executed |
| Provider | E-09 | P1 | Provider returns A then B under same URI and document ID | Byte mismatch rejects result, original unchanged, no cached trainer shown | instrumented | not executed |
| Provider | E-10 | P1 | Same-byte replacement behind same URI/document ID | Explicitly demonstrate URI-derived identity cannot prove replacement; no write authority | instrumented | not executed |
| Provider | E-11 | P1 | Nonseekable short-read stream, null stream, zero length and >32 MiB | Bounded refusal, exactly two opens when appropriate, stream disposal | unit/provider | not executed |
| Lifecycle | E-12 | P1 | Rotate while picker open and during slow parse | No old Activity publishes private data; pending request state handled predictably | emulator | not executed |
| Lifecycle | E-13 | P1 | Home/Back/process kill and reopen during second open | No retained save bytes, leaked grant claims or stale UI; intentional reselect flow | emulator | not executed |
| Lifecycle | E-14 | P1 | Rapid picker taps, cancel, reselect different save | At most one pending picker and one active inspection; newest intentional selection wins | emulator | not executed |
| UI | E-15 | P1 | Sparse box, full box and 64 x 64 capped layout | Correct original slot numbering; bounded one-box display; no phantom occupancy | unit/emulator | not executed |
| UI | E-16 | P2 | Trainer/nickname contains U+202E, U+2066, newline, controls, surrogate pairs | No bidi label spoof, broken grapheme, unintended multiline, or unbounded text | unit | not executed |
| UI | E-17 | P2 | TalkBack, font 200%, narrow portrait, tablet landscape, D-pad | Logical navigation, no inaccessible slot labels or clipped controls | device | not executed |
| Parity | E-18 | P0 | Independent private Gen 1 and Gen 3 retail fixture manifests | Same Core 26.10.18 desktop-vs-mobile fields, exact hashes and negative near-misses | desktop | not executed |
| Parity | E-19 | P1 | Independent private Gen 4–9 and wrapped saves | Per-format read-only parity, wrapper metadata and unsupported variants recorded | desktop | not executed |
| Parity | E-20 | P0 | Retail-shaped CFRU/Unbound/Radical Red near-miss negatives | No vanilla authentication or write enablement; false-positive/negative inventory | desktop | not executed |
| Parity | E-21 | P0 | No-op serialize and controlled single-slot edit in isolated test-only harness | Record checksum/metadata changes, untouched regions and independent reparse; production writer remains locked | offline | not executed |
| Upstream | E-22 | P0 | MAUI BoxTab drag to missing destination and exception between swap writes | Never propagate -1 index or partially swap; no direct port of handler | unit | not executed |
| Upstream | E-23 | P0 | Forge SAF direct rwt write interrupted after truncate/partial bytes | Demonstrate it is not an atomic publish and reject as design for our writer | fault-injection | not executed |
| Rights | E-24 | P1 | Candidate font/sprite/DLL import request | Per-file rights, attribution, source availability and distribution clearance before import | audit | not executed |

## Execution ordering and stopping rules

1. Close E-01–E-05 and record actual dispatch/build/install results before claiming a reproducible or tested Android package. Keep debug and production release claims distinct.
2. Implement fake provider tests for E-06–E-11, then emulator lifecycle tests E-12–E-14. In particular, distinguish a cancellation token being set from a blocked synchronous ContentResolver open actually terminating.
3. Expand safe, read-only presentation for E-15–E-17 without copying unlicensed art. Verify privacy and slot indices before adding actions.
4. Independently compare same-version desktop expectations and negative formats E-18–E-20. E-21 is an isolated experimental test harness only, not authorization to ship save edits.
5. Use E-22–E-24 as no-copy/no-write architecture gates. Do not import Forge's direct truncating writer, MAUI's unchecked box mutation path, NC font or unverified sprite/DLL bundles.

**No writes or ROM-hack profiles are authorized by this matrix.** The roadmap's Milestone 0 architecture decision and Milestone 1 per-format read-only gate remain open.
