# Retail-only official-Core build: executable next gates (2026-10-29)

**Companion:** [UPSTREAM_RETAIL_PROBE_HANDOFF_2026-10-29.md](UPSTREAM_RETAIL_PROBE_HANDOFF_2026-10-29.md). Baseline main [c8b3d76c7192aa32fd56e188056cb15129916c5b](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/c8b3d76c7192aa32fd56e188056cb15129916c5b). This document updates the *execution status* of [PARITY_PORT_COMPILE_RECOVERY_GATES_2026-10-29.md](PARITY_PORT_COMPILE_RECOVERY_GATES_2026-10-29.md), which recorded the earlier 23-error failed compile. No tests were run by this documentation audit; CI evidence below was inspected.

## Verified gate status, not claimed desktop parity

| Earlier gate | Current evidence at pinned source | Verdict |
| --- | --- | --- |
| C01 checkout and gitlinks | [PR #44 workflow run 40717515951](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40717515951), pinned PKForge 2df04430e532b78c247f45b936f3068d8f824987, fork 91e9c88667a6a191a000db68e2db19505eb87561, plugin 23ab1fc73f7be6ad427fbc900f9b99111cd2d560 | **PASS** |
| C02 original fork Android compile | [Upstream build run 40705395415](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40705395415) | **PASS** (not device test) |
| C03 official-Core retail-only Engine compile | [PR #44 run 40717515951](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40717515951), job 122919116802; log 'Build succeeded.' | **PASS for temporary checkout**; no production import |
| C04 official-Core Android compile | Same job, second 'Build succeeded.' | **PASS for temporary checkout**; no device test or APK redistribution |
| C05 hack dispatch | Same job: 18 synthetic negative assertions across four invalid payloads plus two explicit routes | **PARTIAL**; no valid retail positives, structural false-positive controls, Compass fixture or guard mutation test |
| C06 truthful CI verdict | [Workflow](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/c8b3d76c7192aa32fd56e188056cb15129916c5b/.github/workflows/pkforge-official-core-probe.yml) now exits nonzero when Engine, smoke or Android step fails; all succeeded in PR run | **PASS for observed run**; fault-injected verdict negative control not yet executed |
| C07 detached editor | No real synthetic retail edit/preview/cancel in this probe | **NOT RUN** |
| C08 Save As publication | No Android SAF destination writer or provider-fault matrix executed | **NOT RUN** |
| C09 licensing/asset-cleared package | Source project files inspected; no complete artifact asset ledger | **NOT RUN** |
| C10 desktop fixture and device parity | No independent desktop-oracle fixture or emulator/physical device run | **NOT RUN** |

**Interpretation:** passing compile and 18 negative assertions remove a *build blocker*, not a *save-safety blocker*. The earlier [28 consolidation gates](PARITY_PORT_CONSOLIDATION_GATES_2026-10-29.md) still apply. The [merged main foundation CI 40718090642](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40718090642) is green, but the official-Core compatibility workflow is only triggered on PR path changes/manual dispatch; do not mislabel that main run as a fresh compatibility probe.

## P0 implementation tickets — smallest discriminating tests first

All inputs must be generated synthetic fixtures or privately consented saves kept outside Git. Each test records fixture recipe, exact Core version, source SHA-256, expected outcome, and observed Core/save type. **No real-save write authority is granted by any single gate.**

| ID | Reproducible procedure | Required result / reason |
| --- | --- | --- |
| R01 — positive retail parser | Create a structurally valid retail fixture using official PKHeX.Core [26.10.18]; open it with the temporary PKForge official-Core retail Engine and the same-version desktop oracle | Both accept; exact generation, trainer, party, boxes, checksum status and version match; otherwise fail integration |
| R02 — guard mutation control | In disposable checkout, remove or disable only the retail hack-rejection guard; rerun a fixture designed to otherwise reach retail SaveUtil parsing, then restore guard | Control is demonstrably accepted/reaches unsafe parse while guarded version refuses. If both refuse, mark guard effectiveness **not demonstrated**, not pass |
| R03 — GBA marker threshold | From valid synthetic retail-like GBA envelopes, construct controlled 7-stamp and 8-stamp Unbound (0x01121999) and GS Chronicles (0x66290096) variants at sector+0xFF8; exercise Validate, auto, explicit retail and explicit hack routes | Eight markers refused for all entry points; seven must not be *misidentified* as a known hack, but may still be rejected for checksum/structure. No unsupported write session |
| R04 — CFRU structural collision | Build or privately provide verified vanilla FireRed and Radical Red/CFRU-like fixtures with **retail footer signatures**; exercise structural detector before SaveUtil | Retail accepted when authenticated; CFRU-like ambiguous structure refused, even when official Core would parse as SAV3FRLG; unknown layouts remain read-only |
| R05 — envelope transforms | Wrap synthetic positive and negative cases in RetroArch container and append known 0xFF/0x00 SRAM padding; exercise both original and trimmed parser paths | No transform bypasses a hack guard; original envelope/padding preserved for any eventual export; malformed containers refused |
| R06 — BDSP and Compass | Use valid synthetic retail BDSP and SV fixtures; create Luminescent revision-header and Compass TrainerSeed-key variants with pinned fork source as reference | Valid retail fixtures accepted; unsupported hacks refused. A normal retail SV with unrelated blocks must not be rejected by an overly broad single-key rule |
| R07 — mutation/cancel | Adapt PKForge EditorPanel on a cloned PKM/save candidate; change one field, preview legality, cancel | Original bytes, original Core object and Android source URI unchanged; zero published destination writes; matching desktop legality report |
| R08 — one-slot commit | Commit one edited party or box PKM to a detached save using official Core; reopen with independent Core instance | Expected selected slot, save checksums and exact approved block diff only; all other slots and protected bytes unchanged |
| R09 — distinct Save As | Android ACTION_CREATE_DOCUMENT to a destination distinct from original URI; stage candidate, rehash original, write, close, independently reopen destination | Source never opened for writing; destination byte/hash and desktop/Core parse match approved candidate; provider document alias to source refused |
| R10 — interrupted provider | Inject null descriptor, permission revocation, partial write, cancellation, source race, crash before flush/close/reopen, and destination readback mismatch | Original unchanged; never report success for incomplete destination; recover/discard guidance; no original-URI 'rwt' write |
| R11 — editor navigation | Launch adapted PKForge MAUI shell on Android emulator; open party/box, edit, rotate, background/foreground, system Back, cancel | No crash, accidental commit or lost immutable snapshot; record device/API level, ABI, Android provider and logs |
| R12 — code-only resource build | Remove unverified sprite, game-art, Showdown, icon, font, binary event/dex and plugin assets from distributable; build with neutral placeholders | Android build and key UI screens work; produce imported-source/asset manifest, notices and dependency licence ledger before shipping |
| R13 — MAUI selective feature | Only after R07–R10: adapt one PKHeX MAUI editor using official 26.10.18 Core, without static mutable global save or binary plugin dependencies | Same-generation screen opens and cancels; per-block desktop oracle diff; no asset or version mismatch |
| R14 — CI failure semantics | In a throwaway PR, deliberately fail Engine compile, runtime smoke and Android compile independently | Each failure causes the final compatibility workflow to fail; skipped steps cannot yield a green compatibility verdict |

### Concrete minimum acceptance for next implementation PR

**First PR (compile-to-real-app bridge):** R01, R02, R07 and R11, plus source/asset provenance for every imported file. This yields a *usable read/edit/cancel prototype*, not export.

**Second PR (safe export):** R04–R06 and R08–R10 with real provider fault injection and independent desktop fixtures. Export remains disabled until those pass; ROM-hack writes remain disabled without authenticated profiles.

**Later parity:** R12–R13 and all desktop-specific editors, game formats and device matrices. No parity percentage from screen counts, documentation or compiler success.

## Evidence hygiene

- CI [run 40717515951](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40717515951) at PR #44 head 5d62f9c2ea1f: pinned checkout, Engine compile, 18 negative assertions, Android compile and final verdict **passed**.
- [Main run 40718090642](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40718090642) at c8b3d76: foundation checks **passed**; no separate main official-Core probe.
- **Executed by this audit:** 0 new tests, 0 source integrations, 0 emulator/device checks, 0 writes. **Proposed here:** 14 targeted gates, all unexecuted unless prior CI evidence explicitly stated above.
- Source revision ledger: [PKForge 2df0443](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987), [PKHeX MAUI a6f7cfb](https://github.com/santacrab2/PKHeXMAUI/tree/a6f7cfb78e1c96155baa67660fd7ae97895e375f), [official desktop checkpoint 7e26bc5](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3), [official Core release bfe89c74](https://github.com/kwsch/PKHeX/tree/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea). Do not mix Core 26.7.7, official 26.10.18 and PKForge fork without explicit adapters and tests.

**Ownership:** audit docs only (UPSTREAM_*/PARITY_*). Implementation owns scripts, workflows, tests, app and dependency changes. Preserve README first line '# This is a test.', GPL obligations, no secrets, ROMs, paid assets, private saves or chat transcripts. The 30-hour target is aspirational, never a reason to skip a fail-closed gate.
