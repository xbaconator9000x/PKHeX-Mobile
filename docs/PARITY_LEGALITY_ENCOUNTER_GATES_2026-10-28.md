# Legality, encounter and batch acceptance matrix — pass G (2026-10-28)

**Proposed tests only.** None of the **30** cases below were executed or passed in this source-audit run. Each gate traces to an explicit [G-series source finding](UPSTREAM_LEGALITY_ENCOUNTER_REVIEW_2026-10-28.md). Static inspection and the Android CI compilation success do not certify parity, save format, legality or safe writing.

**Pinned identities:** PKHeX source `7e26bc53215e4f7bc0041ca375f1042539cb81b3`, PKForge `2df04430e532b78c247f45b936f3068d8f824987`, PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; mobile NuGet **PKHeX.Core 26.10.18** corresponds to source release `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`, *not* the newer source pin. Audit implementation baseline `ee599cfa9d3735ffadb2a12c8ae9baa6afafa1e0`. No production save writing may be enabled by a passing legality test alone.

## 30 independently checkable gates

| Gate | Finding(s) | Priority | Synthetic/consented input, action and expected result | Required evidence | Status |
| --- | --- | --- | --- | --- | --- |
| GG-01 | G-01,G-20 | P0 | Unsupported hack-shaped save with retail-looking Pokémon; legality output never unlocks writer, export or compatibility profile. | negative fixture | not executed |
| GG-02 | G-02 | P1 | Same PKM bytes analyzed with save.Personal versus PKM.PersonalInfo for cross-game form context; record structured differences. | desktop fixture | not executed |
| GG-03 | G-02 | P1 | Identical PKM analyzed with party, box and ignored slot-origin contexts; preserve all differences. | desktop fixture | not executed |
| GG-04 | G-03 | P1 | Malformed PKM and forced checker exception return explicit error/unparsed state, not green Valid. | unit | not executed |
| GG-05 | G-04 | P0 | Interleave two saves of different GB/VC eras, then repeat analysis in reversed order; identical per-save results. | concurrency | not executed |
| GG-06 | G-04,G-05 | P0 | Run two concurrent legality sessions with conflicting trainer/era/severity settings; no cross-session contamination. | concurrency | not executed |
| GG-07 | G-05 | P1 | Missing HOME tracker under default, Fishy and Invalid severities; match same-version desktop codes and validity. | desktop fixture | not executed |
| GG-08 | G-06 | P1 | Duplicate PID/trainer/encryption/gift/handler/clone sample across multiple boxes; compare bulk vs individual reports. | fixture | not executed |
| GG-09 | G-06 | P1 | Fusion and unique-item save-specific checks, plus invalid-checksum excluded slot; no false 'all slots passed' claim. | fixture | not executed |
| GG-10 | G-07 | P1 | Register/unregister custom checker, duplicate ID, concurrent analysis; stable checker inventory and deterministic output. | unit | not executed |
| GG-11 | G-08 | P2 | English vs non-English report: identical structured codes/severity/encounter with localized descriptions. | desktop fixture | not executed |
| GG-12 | G-09 | P1 | No encounter, partial encounter, secondary-check failure; no matched encounter reported as fully legal. | fixture | not executed |
| GG-13 | G-10 | P1 | Run encounter search on cloned entity, compare original bytes and handler trash before/after, restore PriorityList. | unit | not executed |
| GG-14 | G-10,G-12 | P1 | Concurrent encounter queries with different priority orders/species/forms remain deterministic and independent. | concurrency | not executed |
| GG-15 | G-11 | P1 | 37 Forge mainline IDs inventoried; GO/CXD exclusion shown as unsupported rather than 'unobtainable'. | unit | not executed |
| GG-16 | G-11,G-19 | P1 | Blank-save construction failure yields explicit unavailable result; Diamond/Pearl wild/egg parity remains verified. | unit | not executed |
| GG-17 | G-12 | P1 | MAUI encounter selection null/changed, conversion failure and duplicate semantic entries; no blank fallback treated as valid candidate. | unit | not executed |
| GG-18 | G-13 | P0 | Legality preview made on one document then same slot/entity bytes copied into a replaced document; refuse stale publish. | fault injection | not executed |
| GG-19 | G-13,G-14 | P0 | Preview candidate changes only authorized fields, records before/after legality and full-save hashes, rechecks on apply. | fixture | not executed |
| GG-20 | G-14 | P1 | Eight Forge safe-order suggestions each tested for no-op, improvement, regression and irreparable input. | unit | not executed |
| GG-21 | G-15 | P0 | MAUI-style empty instructions, protected slot and skipped filter leave all save bytes unchanged; no write-back loop. | fault injection | not executed |
| GG-22 | G-16 | P0 | Batch mutation failure after first, middle and last slot: staged export never publishes partial save; backup restore exact. | fault injection | not executed |
| GG-23 | G-17 | P2 | Change MAUI legality preference while open, re-evaluate and reopen; ensure displayed configuration matches actual parse. | device/unit | not executed |
| GG-24 | G-18 | P1 | Build pinned Forge tests with exact submodule SHAs and .NET SDK; report skipped/missing external corpus, no invented pass. | upstream build | not executed |
| GG-25 | G-19 | P1 | Cross-game encounter samples by generation, gift, egg and GO/CXD omissions; compare pinned same-engine results. | desktop fixture | not executed |
| GG-26 | G-20 | P0 | Desktop PKHeX 26.10.18 vs mobile 26.10.18 on independently produced consented fixture; capture package SHA and settings. | desktop fixture | not executed |
| GG-27 | G-20 | P1 | Run newer 7e26bc source separately, classify changed verdicts as version drift until same-version baseline established. | version diff | not executed |
| GG-28 | G-21 | P1 | Every proposed font/sprite/import has rights, author, SHA, license, distribution scope and GPL compatibility decision. | licence audit | not executed |
| GG-29 | G-22 | P1 | Read-only UI clearly says legality unsupported; cannot imply valid save, supported hack or safe edit/export. | UI/unit | not executed |
| GG-30 | G-01,G-22 | P0 | No writer reachable through Android, CLI or interop for unknown profile even if all PKM analyses are Valid. | negative fixture | not executed |

## Execution record template and refusal criteria

For each case, record: gate ID, exact implementation HEAD, PKHeX.Core NuGet package version and SHA-256, upstream source SHA, .NET/Android SDK versions, legality settings fingerprint, registered checker IDs, active save/profile identity, private fixture ID and hash, independent desktop result, command, exit code, test output digest and CI/device reference. Keep user saves and trainer data **outside** the repository. Do not publish screenshots of private data, ROMs, signing keys or paid assets.

Compare structured legality result codes, severities, move/relearn arrays, matched encounter identity and slot-origin context; localize descriptions separately. For batch tests, compare source and candidate whole-file hashes, untouched regions, every affected slot, checksum/block semantics, and restorable backup bytes. A negative test must assert **no original modification**, not merely an error message.

**Hard stop:** Unsupported or ambiguous ROM-hack saves remain non-writable even if every Pokémon appears legal. If the save identity changes between preview and publication, or a provider cannot guarantee safe staging and recovery, refuse. A user confirmation, file extension, checksum validity or a Forge/MAUI UI path cannot override the refusal.

**Counts:** 30 proposed; **0 executed**, **0 certified**. This is a test specification, not a parity percentage or release approval.
