# Generation 6 systems parity and safety execution gates — pass X (2026-10-28)

**Proposed only: 36 uniquely numbered cases, zero executed/certified by this audit.** References [source findings](UPSTREAM_GEN6_SYSTEMS_REVIEW_2026-10-28.md) G6-01..G6-33. Use official PKHeX.Core 26.10.18 and independently matched desktop PKHeX 26.10.18 fixtures. Never store ROMs, commercial assets, private saves or keys in the repository.

## Test protocol

For each case: construct a bounded, detached synthetic byte array with sentinel bytes before/after the affected substructure; record SHA-256 of original; invoke the **specific pinned** API only on an isolated candidate; compare exact changed offsets, untouched region and exceptions; repeat through the mobile projection. For claims of game semantics, use an independently sourced same-version desktop oracle and consented private fixture. A Core-generated synthetic save alone is not independent parity. For Android document-provider cases, use controllable provider failure injection, source-identity changes, readback and rollback. Report `PASS`, `FAIL`, `BLOCKED` separately; never treat a proposed case as passed.

| ID | Findings | Required reproducible scenario / expected gate |
|---|---|---|
| X-01 | G6-01/G6-02 | Empty 12-bag list: AddBag must choose slot 0 without touching others; compare pinned Core behavior on clone. |
| X-02 | G6-01/G6-02 | Partially filled bag list with holes: new bag must occupy first zero; all occupied sentinels unchanged. |
| X-03 | G6-01/G6-02 | Full 12-bag list: reject insertion without modifying any byte. |
| X-04 | G6-03 | Desktop bag deletion from slot 0/middle/11: last compacted slot zeroed and untouched flags preserved. |
| X-05 | G6-04 | Read-only 96 float times including NaN/Infinity/subnormal/negative: exact raw bits survive projection. |
| X-06 | G6-05 | Change returned holder object on cloned source; prove read-only projection never exposes live memory. |
| X-07 | G6-06/G6-07 | 48 raw holder records, 32 named stages, six distribution bits; compare independent PKM regimen projection. |
| X-08 | G6-08 | Set IsNew with byte1=0xA5 sentinel; prove current setter changes adjacent byte, then enforce byte preservation. |
| X-09 | G6-09 | Locations -1,0,1,2,85,86: read-only never normalizes raw bytes. |
| X-10 | G6-10 | Nonzero trash bytes after name/message terminators: unchanged display must preserve exact raw data. |
| X-11 | G6-11 | Flip participant bit 5 and compare IsEgg/IsShiny views against same-version desktop and game-specific oracle. |
| X-12 | G6-12 | Set IV_SPD to 31/32/63 on clone with upper-bit sentinels; future adapter must reject overflow. |
| X-13 | G6-13 | Set Gender 0..4 on clone with form sentinel; future adapter rejects values outside 0..3. |
| X-14 | G6-14 | Import 0x310 into 0x3E0 other base; compare prefix and preserve all 0xD0 tail bytes. |
| X-15 | G6-14 | Reject import sizes 0x30F, 0x311, 0x3DF and 0x3E1 without changing source. |
| X-16 | G6-15 | Participant 1 invalid while participants 0 and 2 valid: all-or-nothing candidate rollback. |
| X-17 | G6-16 | Delete first/middle/last of 30 other bases; exact shift, last slot cleared, outside region identical. |
| X-18 | G6-17 | Grant 173 goods; compare all 200 entries, reserved bytes and adjacent block sentinels. |
| X-19 | G6-18 | O-Power flags 0,1,2,0xFF: no-op save must preserve unknown flags. |
| X-20 | G6-19 | Clear/Unlock O-Power: verify byte0 and 99 other bytes against expected explicit operation. |
| X-21 | G6-20 | Invalid negative/65 enum indices and >3 levels rejected before touching Core. |
| X-22 | G6-21 | Contest raw counts 0,999,1000,0xFFFFFFFF and untouched 0x48+ reserved bytes. |
| X-23 | G6-22 | Situation mirrored map/rotation/coords: detect mismatch without silent normalization. |
| X-24 | G6-22 | Injected failure after writing X before Y/Z: entire candidate rolls back. |
| X-25 | G6-23 | Switch from document A to B while Gen6 tab open; all callbacks refuse stale A references. |
| X-26 | G6-24 | Inject exception at each of five SavePage6 callbacks; original document and draft unchanged. |
| X-27 | G6-25 | Maison field 20 malformed after 19 valid: zero mutations and actionable parse error. |
| X-28 | G6-26 | Coordinate input 1/18,17/18,-1/18 and large values: no-op save bit-identical. |
| X-29 | G6-27 | Fashion two-way bound entry edited then Cancel: no bytes modified in original. |
| X-30 | G6-28 | Unlock all accessories then Cancel/rotate: no source changes; staged candidate only. |
| X-31 | G6-29 | Edit record 63 only; compare record 64 with independent desktop fixture. |
| X-32 | G6-30 | Inventory all PKForge and MAUI routes for XY and ORAS Super Training, Secret Base and O-Powers. |
| X-33 | G6-31 | Compare PKForge cosmetics per-PKM support against save-level ORAS contest scope; no false parity. |
| X-34 | G6-32 | RunMutationAsync + SAF: injected cancellation, write failure, identity change, rollback and backup durability. |
| X-35 | G6-33 | Every imported source/asset path has licence, attribution and reproducible origin manifest. |
| X-36 | G6-01/G6-33 | Independent desktop PKHeX 26.10.18 fixtures and negative ROM-hack layout gate before any write. |

## Release gate and implementation handoff

**P0 before any write:** X-01..X-04, X-08, X-11..X-13, X-25..X-30, X-34 and X-36 must have isolated reproductions, corrected implementation and independent fixture/rollback evidence. Other cases remain parity/robustness gates and cannot be silently waived. A passing CI compile or read-only synthetic smoke test is not equivalent to any write gate. Document per-format/region support; unknown or ROM-hack layouts remain read-only.

**Suggested test ownership:** domain/ReadOnly synthetic tests for X-01..X-24; Android provider lifecycle for X-25..X-30 and X-34; cross-version desktop fixture harness for X-31..X-33 and X-36; licensing/provenance verification for X-35. The implementation watchdog owns all code and tests; this audit owns only `docs/UPSTREAM_*` and `docs/PARITY_*` plus the upstream index.
