# Editor dispatch, world services and transfer acceptance gates — pass F (2026-10-28)

This is a **test plan, not an execution report**. Evidence is anchored in [source audit pass F](UPSTREAM_EDITOR_DISPATCH_REVIEW_2026-10-28.md), with immutable upstream commits `7e26bc53215e4f7bc0041ca375f1042539cb81b3` (PKHeX), `2df04430e532b78c247f45b936f3068d8f824987` (PKForge), `a6f7cfb78e1c96155baa67660fd7ae97895e375f` (PKHeX MAUI) and implementation baseline `f4b9a8b250f1d8a6b1c9a13a64fce6c95190822e`. These tests must run against independently generated, rights-cleared synthetic/consented private fixtures; never commit game saves or ROMs. The mobile package Core 26.10.18 and the newer desktop source must not be silently equated.

## 28 proposed acceptance cases

| Gate ID | Source finding(s) | Priority | Component | Reproducible input / action and required outcome | Evidence required | Status |
| --- | --- | --- | --- | --- | --- |
| FG-01 | F-01, F-02 | P1 | Trainer routing | For each desktop typed trainer route, verify corresponding mobile capability or explicit unsupported message; include 7b, SWSH, BDSP, LA, SV, ZA, BR. | source/unit | not executed |
| FG-02 | F-02 | P1 | Visibility | On every MAUI supported SaveFile type, compare visible Trainer Info to a real navigation action; assert no silent tap. | source/unit | not executed |
| FG-03 | F-03 | P1 | Dex routing | Exercise 14 desktop Pokédex switch arms with positive/negative revision fixtures; mark mobile unsupported until matching behavior exists. | desktop fixture | not executed |
| FG-04 | F-04 | P1 | DLC raids | SV/SWSH revision 0/1/2 expose correct raid origins and reject unavailable DLC edits. | desktop fixture | not executed |
| FG-05 | F-05 | P1 | Event flags | LGPE, BDSP, ZA and classic flag/work editor dispatch with exact data widths; refuse unknown save types. | desktop fixture | not executed |
| FG-06 | F-06 | P1 | Editor coverage | Inventory every desktop subeditor control and gate by game/revision, distinguishing UI route from functional save round trip. | audit | not executed |
| FG-07 | F-07 | P1 | MAUI Gen9 timestamp | Set non-midnight LastSaved, change time only, save, reopen, compare full timestamp; no unrequested midnight reset. | upstream unit | not executed |
| FG-08 | F-08 | P2 | MAUI Gen9 LP | MaxLP uses documented LP cap (not assumed money cap); verify boundary and overflow cases. | upstream unit | not executed |
| FG-09 | F-09 | P1 | MAUI picker validation | Null language and -1 version selection leave source bytes unchanged and return validation feedback. | upstream unit | not executed |
| FG-10 | F-10 | P1 | Inventory writes | For each claimed pouch, change one item and verify destination save block after serialization and independent reparse. | desktop fixture | not executed |
| FG-11 | F-11 | P1 | World menu gates | 8 Forge menu options each has positive and negative game support case; no unsupported button offered. | unit | not executed |
| FG-12 | F-12 | P1 | Clock support | R/S/E positive, FRLG and other Gen3 negative; unchanged clock bytes and no-op behavior verified. | fixture | not executed |
| FG-13 | F-13 | P1 | Roamers | RS/E/FRLG, DP/Pt/HGSS, BW, XY cases: verify species/PID/IV/flags; XY reroll refused. | fixture | not executed |
| FG-14 | F-14 | P1 | Entralink | BW vs B2W2 forest, powers, Funfest and encrypted end-access round trips; reject cross-game writes. | fixture | not executed |
| FG-15 | F-15 | P1 | HGSS gadgets | Apricorn/Pokéwalker count and language-sensitive course constraints; non-HGSS refuses. | fixture | not executed |
| FG-16 | F-15 | P1 | Honey trees | DP/Pt tree values, legal Munchlax and illegal trainer/tree combination, no unrelated changes. | fixture | not executed |
| FG-17 | F-16 | P0 | Multi-field rollback | Inject exceptions after item, flag and work mutation; in-memory baseline and original on-disk hash unchanged. | fault injection | not executed |
| FG-18 | F-17 | P0 | Hack risk bypass | For any CFRU/Unbound/RadicalRed-shaped unsupported save, user confirmation never unlocks write/export. | negative fixture | not executed |
| FG-19 | F-18 | P1 | Showdown preview | Distinguish parsed-only hack sets from stock legality verdict; no green 'legal' without authenticated game rules. | unit | not executed |
| FG-20 | F-19 | P1 | Showdown target race | After preview, occupy a target; placement cannot overwrite occupied party/box or silently redirect without consent. | unit | not executed |
| FG-21 | F-19 | P1 | Showdown sparse party | Party with gaps and capacity boundary retains all prior Pokémon and reports actual appended slot. | unit | not executed |
| FG-22 | F-20 | P1 | Bulk export collisions | Two same-named entities, pre-existing file, full disk, provider failure: no silent overwrite and manifest lists committed outputs. | fault injection | not executed |
| FG-23 | F-21, F-23 | P1 | Android portrait | Install APK, rotate, use 200% font and TalkBack, navigate all six-column slots and inspect source index correctly. | device | not executed |
| FG-24 | F-23 | P1 | Android lifecycle | Picker pending, rotation, process kill and cancelled descriptor open: no stale private data, no phantom result. | device | not executed |
| FG-25 | F-22 | P1 | Asset rights | For each proposed image/font/DLL import require provenance, source/rights, licence compatibility and attribution manifest. | audit | not executed |
| FG-26 | F-23, F-24 | P0 | Same-Core baseline | Independently produced Gen1/Gen3/Gen4–9 fixtures with SHA-256 and matching Core 26.10.18 desktop readout; no same-Core self-oracle. | desktop | not executed |
| FG-27 | F-17, F-24 | P0 | Write gate | No writer enabled until exact layout identity, backup restore, staged serialization, independent reparse, provider failure tests pass. | safety | not executed |
| FG-28 | F-21–F-24 | P1 | Architecture | Document why native Android + pinned Core adapter is retained over landscape Forge and MAUI, including versioned PokéBacon boundary. | decision | not executed |

## Execution protocol and stop conditions

1. Capture exact repo commit, resolved SDK, Core package and matching desktop engine, fixture provenance and SHA-256 before testing. Record whether the test is static, unit, emulator, physical-device or independent desktop comparison.
2. For mutation tests, keep fixtures outside the repository and use a disposable isolated working copy. Measure intended and unintended field/block differences, checksums, wrappers and full-file hashes. Restore from a verified original-byte backup.
3. For a failed test, retain only sanitized logs, assertion names and reproducible synthetic parameters; never upload private trainer/party content or document URIs.
4. **Stop before any production save writer** unless exact supported retail/hack layout, stale-document protection, staging, backup, readback and same-version independent desktop round trips all pass. No user opt-in overrides unknown ROM-hack identity.
5. Update statuses only after recording real test commands, exit codes, CI URLs and fixture identifiers. Source inspection or successful APK compilation alone does not mark a case passed.

**Counts:** 28 planned cases; **0 executed in this audit pass**. No parity certification or release gate is claimed.