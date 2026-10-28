# Port consolidation: executable parity and safety gates (2026-10-29)

**Source decision:** [UPSTREAM_PORT_CONSOLIDATION_DECISION_2026-10-29.md](UPSTREAM_PORT_CONSOLIDATION_DECISION_2026-10-29.md). **All gates below are proposed, not executed in this audit.** None authorizes save writing by itself. The 30-hour goal is a prioritization constraint, not permission to bypass data-loss prevention.

## Version/asset ledger required before import

| Key | Required evidence | Failure policy |
| --- | --- | --- |
| G00 | Record pinned source commit for [PKForge `2df0443`](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987), [MAUI `a6f7cfb`](https://github.com/santacrab2/PKHeXMAUI/tree/a6f7cfb78e1c96155baa67660fd7ae97895e375f), [official PKHeX `7e26bc5`](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3), and actual app `PKHeX.Core [26.10.18]` NuGet provenance | Block imported code without exact source provenance |
| G01 | Resolve PKForge's `external/PKHeX` and `external/PKHeX-Plugins` gitlink commits; compare forked Core to official Core with machine-readable diff summary of public APIs, save types, serializers, hack handlers, legality | No Core swap or fork-derived writes until divergence is understood |
| G02 | Build PKForge pinned source with recorded .NET/MAUI SDK, workload, architecture, dependency hashes and `dotnet test` result; build MAUI only if selectively importing it | Build failure is recorded, not papered over with “supported” |
| G03 | Enumerate imported source files and licenses; audit art, fonts, media, binary `deps`, game tables and transitive packages separately; ship code-only if rights unclear | No unverified asset/binary redistribution |

## P0 end-to-end safety gates

These tests should use **synthetic saves or expressly consented private fixtures**; never commit personal saves, game ROMs or keys. Every test records fixture provenance, Core version, pre/post SHA-256, exact expected bytes/slots, and a negative case. Use a pinned desktop PKHeX/Core oracle independent of the Android UI.

| ID | Reproducible action | Required assertion |
| --- | --- | --- |
| P0-01 | Import recognized synthetic save through Android `ACTION_OPEN_DOCUMENT`; separately reopen URI twice | Identical bytes, format, trainer, party and box projections; invalid or changed second read refuses editing |
| P0-02 | Import unrecognized, truncated, oversized, padded, and malformed saves; attempt edit | No write-capable session; specific reason; original unchanged |
| P0-03 | Import retail FireRed and synthetic CFRU-like ambiguous sector layouts | Retail allowed only if layout authenticated; CFRU ambiguity fails closed even when `SaveUtil` returns a retail save |
| P0-04 | Import known emulator container or padded SRAM accepted by adapted [SaveParser](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveParser.cs) | Container/padding envelope is recorded and preserved; no silent truncation of exported file |
| P0-05 | Edit a single PKM level/nickname on detached candidate, then cancel | Original source bytes, in-memory snapshot and provider document unchanged; no destination created |
| P0-06 | Commit a single box slot edit into candidate with `PKHeX.Core [26.10.18]` | Exact expected slot and checksum changes only, as independently determined by pinned desktop oracle; no other slot changes |
| P0-07 | Produce legality preview using adapted [LegalityAssistService](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/LegalityAssistService.cs) | Preview does not mutate source; official Core legality result and candidate PKM bytes agree with desktop; applying is separate |
| P0-08 | Export via Android **Save As** to a newly created distinct URI, then independently reopen | Source URI/hash unchanged; destination hash matches candidate, valid recognized format, desktop re-open succeeds |
| P0-09 | Choose destination URI equal to source or same underlying provider document identity | Refuse publishing; no truncation or write |
| P0-10 | Provider returns null descriptor, read-only permission, revoked URI, short write, or throws during Save As | No claim of success; original untouched; failed destination identified for cleanup/recovery |
| P0-11 | Provider changes source contents after preview, during backup creation, or before publishing | Re-read detects source identity/hash mismatch; no write; user must reopen |
| P0-12 | Crash/cancel/process death at each stage of create, stream write, flush, close, reopen and validation | Original untouched; incomplete destination is never advertised as valid; restart surfaces recovery/discard path |
| P0-13 | Try PKForge [`WriteAtomicallyAsync`](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Platforms/Android/AndroidSafFileAccess.cs) on a fault-injecting provider that truncates before failing | Test **demonstrates non-atomicity**; production integration must not call this method for original save replacement |
| P0-14 | Simulate candidate mutation by UI after validation or between staging and readback | Detached snapshot protects preflight; mismatched readback blocks publish |
| P0-15 | Independently reopen staged destination and compare with approved slot diff | No out-of-scope slot changes, no checksum/layout regression, no identity alias to source/backup |
| P0-16 | Import same fixture into official Core `26.10.18`, PKForge fork, and MAUI `26.7.7` adapters | Record version-specific differences; no claim of parity from a cross-version match alone |

## P1 reusable UI and feature gates

| ID | Test | Expected |
| --- | --- | --- |
| P1-01 | Build PKForge MAUI shell with no proprietary/non-cleared artwork | App launches and can navigate box and party with placeholder icons |
| P1-02 | Open box grid and editor; rotate device, background/foreground, use system Back | No lost unsaved state, unexpected save write, crash or stuck modal |
| P1-03 | Edit moves, EVs/IVs, form, origin, ribbons and held item across representative generations | Each field available only for applicable PKM types, matches desktop/Core after round trip |
| P1-04 | Exercise MAUI [SaveEditors dispatcher](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Save%20Editors.xaml.cs) for a supported Gen4 editor | Screen opens, backs out cleanly; detached data only; desktop-matched block diff |
| P1-05 | Edit trainer, inventory and Pokédex on synthetic save | Only expected blocks change, checksums valid, desktop reopens; no cross-generation field leakage |
| P1-06 | Run entire app/test build from clean checkout on CI, then install on Android emulator and physical device | Record SDK/NDK versions, app version, ABI, provider, device and failure logs |
| P1-07 | Validate optional plugin AutoMod and MAUI `deps` binary provenance | No bundled DLL without source/license and exact version evidence; disabled if unverified |
| P1-08 | Attempt hack saves without a version-authenticated profile | Read-only or refused; no official-game legality claim for unknown hack species/items |

## Priority order and deliverable ownership

**Implementation agent** owns code, app, tests and dependency changes; audit agent owns only `docs/UPSTREAM_*` and `docs/PARITY_*`. The first implementation PR should close **G00–G03, P1-01, P0-01 and P0-05**, or explain the exact build/fork blocker and pivot to current native app. Next PR closes **P0-06–P0-08, P0-09–P0-15** before any export feature is advertised. Later PRs broaden P1 feature coverage. All merges require passing checks on the actual PR head, not a prior main commit.

**Acceptance report format per run:** base SHA; imported file paths and upstream gitlinks; executed gate IDs (pass/fail/blocked); build/test commands and results; PR/merge SHA and CI URL; remaining P0 blockers. A source-only finding or XAML screen is **not** an executed test or shipped feature.

## Execution status at audit creation

- **Executed by this audit:** 0 of 4 provenance/build gates, 0 of 16 P0 tests, 0 of 8 P1 tests.
- **Source inspection:** pinned upstream projects and selected files reviewed; [main baseline CI](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40696463307) previously passed at `b64606b`.
- **No source import, Android device run, desktop comparison, Save As implementation, or safe-write certification performed.**