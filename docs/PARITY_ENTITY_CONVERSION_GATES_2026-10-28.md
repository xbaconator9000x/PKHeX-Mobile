# Entity format, conversion and PKM field acceptance gates — pass I (2026-10-28)

**Status: 32 proposed gates; zero executed by this source audit.** These tests trace to [24 source findings](UPSTREAM_ENTITY_CONVERSION_REVIEW_2026-10-28.md). Pinned source: PKHeX `7e26bc53215e4f7bc0041ca375f1042539cb81b3`; PKForge `2df04430e532b78c247f45b936f3068d8f824987`; PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; PKHeX-Mobile baseline `d399072f1f2507faf08cb30f35bc650c5500dff5`. Mobile NuGet Core is **[26.10.18]**, source tag `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`, not automatically equivalent to the audit reference binary.

| Gate | Finding(s) | Priority | Reproducible action and expected result | Required evidence | Status |
| --- | --- | --- | --- | --- | --- |
| IG-01 | I-01 | P0 | Fuzz every accepted entity length with zeros, random bytes, malformed checksum and truncated buffers; plausible size alone never yields trusted/importable state. | unit/fuzz | **not executed** |
| IG-02 | I-02 | P0 | Construct colliding PK6/PK7 bytes with both `prefer` contexts; preserve ambiguous result and require authenticated format metadata. | unit/Core | **not executed** |
| IG-03 | I-02 | P0 | Construct PK8/PB8 equal-size variants; context-free parse must not silently certify either identity. | unit/Core | **not executed** |
| IG-04 | I-02 | P0 | Construct PK9/PA9 (including ZA) ambiguous bytes; verify type/context and refusal without proof. | unit/Core | **not executed** |
| IG-05 | I-21,I-02 | P0 | Spoof `.pa9`, `.pb8`, MIME type and Android display name on mismatched bytes; none can authorize import/export. | negative/SAF | **not executed** |
| IG-06 | I-03 | P0 | Forge-style bank metadata `PB8` plus valid shorter PK6/7 bytes: reject rather than returning heuristic alternate type. | unit | **not executed** |
| IG-07 | I-04 | P1 | Migrate unambiguous and unresolved legacy entries; ambiguous metadata stays unresolved and raw bytes unchanged. | unit/bank | **not executed** |
| IG-08 | I-04 | P1 | Concurrent bank update during migration and second migration run: stale changes refused; idempotent metadata. | concurrency | **not executed** |
| IG-09 | I-05 | P0 | Run two sessions with conflicting `EntityConverter` policies concurrently; prove deterministic policy isolation/restoration. | concurrency/Core | **not executed** |
| IG-10 | I-05 | P0 | With incompatible conversion disallowed, a route with no official path refuses without invoking reflection; verify Core flags before/after. | unit/Core | **not executed** |
| IG-11 | I-06,I-20 | P1 | Same-type conversion preview returns a distinct owned object in mobile adapter; source byte hash unchanged after candidate mutation. | unit | **not executed** |
| IG-12 | I-07 | P0 | Gen9-to-Gen3 downgrade cannot enter ordinary edit/save workflow; require explicit experimental policy and no in-place output. | negative/unit | **not executed** |
| IG-13 | I-07,I-10 | P0 | Enumerate downgrade sanitizers; compare source/clone field diffs, restored source hash and independent target parse. | desktop/private fixture | **not executed** |
| IG-14 | I-08 | P1 | Forward transfer of an egg records hatch transformation, no silent source mutation, independent destination verification. | unit/desktop | **not executed** |
| IG-15 | I-08 | P1 | Toggle HOME rejuvenation, VC source version and Gen4→5 date retention in isolated sessions; assert documented deltas. | unit/desktop | **not executed** |
| IG-16 | I-09 | P1 | Table-drive every outbound/inbound explicit incompatible species/form arm; destination unchanged on refusal. | unit/Core | **not executed** |
| IG-17 | I-10 | P0 | Call compatibility sanitization on a clone with invalid item, move, name and EV/IV; assert original exact bytes and logged changes. | unit/Core | **not executed** |
| IG-18 | I-11 | P1 | Synthetic PK1 and PK2 render nature/ability N/A and gender with historical caveat; raw manifest remains unchanged. | unit/UI | **not executed** |
| IG-19 | I-12 | P1 | Gen1/2 DV test: four stored nibbles, derived HP, shared Special SpA/SpD and full range 0–15. | unit/Core | **not executed** |
| IG-20 | I-12 | P1 | Gen1/2 stat-experience test: five independent 0–65535 values, shared Special and no modern 252 EV clamp. | unit/Core | **not executed** |
| IG-21 | I-13 | P1 | Read-only details show raw IDs separately from localized labels; unsupported mechanics explicitly unavailable. | unit/UI | **not executed** |
| IG-22 | I-14 | P0 | Independent desktop 26.10.18 fixture oracle compares v2 field paths across claimed formats, with source/build hashes recorded. | private fixture/desktop | **not executed** |
| IG-23 | I-14 | P1 | Synthetic same-Core positive never flips a `certified` flag; missing independent oracle fails acceptance gate. | unit/CI | **not executed** |
| IG-24 | I-15 | P1 | MAUI stats-tab synthetic entity before/after bytes; confirm whether `ResetPartyStats()` changes party data and isolate our view. | unit/source reproduction | **not executed** |
| IG-25 | I-16 | P1 | MAUI Manaphy egg with nickname flag set: opening PK tab does not silently clear it in our implementation. | unit/source reproduction | **not executed** |
| IG-26 | I-17 | P1 | Open two distinct saves, switch tabs rapidly and recreate Android Activity; no static PKM/move-list state leakage. | device/concurrency | **not executed** |
| IG-27 | I-18 | P1 | PKSM bank import/export format matrix: PK1–PK8, PB7, PB8, PA8, PK9, PA9, hack-native tags; explicit supported/refused. | unit/bank | **not executed** |
| IG-28 | I-19 | P1 | PKSM tagged ingress and stored export compare decrypted/stored/party lengths, checksum and type with no assumed byte identity. | unit/bank | **not executed** |
| IG-29 | I-20 | P1 | Forge-style same-type preview plus downstream mutation cannot modify original PKM; verify source hash. | unit | **not executed** |
| IG-30 | I-22 | P1 | Inventory desktop tabs per entity context: moves/PP/relearn, AV/GV, stat nature, OT/HT, met, forms, ribbons; mark mobile state honestly. | source inventory | **not executed** |
| IG-31 | I-23 | P1 | Before any code or artwork import, verify upstream SHA, file licence, attribution, asset ownership and GPL compatibility; block NC font/media. | licence review | **not executed** |
| IG-32 | I-01,I-02,I-03,I-07,I-14,I-24 | P0 | No save write, export or PokéBacon profile authorization from entity parse, legality, file extension or successful conversion; require authenticated layout and independent transactional round-trip. | safety gate | **not executed** |

## Evidence and stop rules

1. For each run record upstream Git SHA, **actual resolved PKHeX.Core binary/package hash**, SDK, test command/exit status, CI URL and whether source-only, synthetic runtime, independent desktop fixture or physical-device test. The eight matching source blobs are **not** a package reproducibility proof.
2. Generate rights-cleared synthetic entities for collision, GB DV/stat-experience, aliasing, mutation and rejection tests. Store real saves and any private desktop baseline outside the public repository. For each independent desktop fixture record original SHA-256, desktop executable/Core version, target format/context, expected field manifest and observed mismatches without publishing private trainer/Pokémon data.
3. Distinguish raw byte equality, entity class/context equality, game-legality evaluation, transfer compatibility and *authenticated save-layout write permission*. No one of these proves the others. Assert every read-only UI interaction and rejected operation preserves source bytes.
4. For Android test SAF content URIs (not paths), document revision changes, cancellation, process recreation and stale selection. Never trust display filename, MIME or extension as a format authority.
5. **Hard stop:** unsupported hacks, ambiguous entity contexts, unverified conversions and unverified save containers remain read-only. No save writer until staged validation, exact identity checks, byte-preserving backup/rollback and independent same-version desktop round-trip are demonstrated.

## Suggested execution batches

- **Batch A / immediate P0:** IG-01–06, IG-09–10, IG-12–13, IG-17, IG-22, IG-32.
- **Batch B / safe presentation:** IG-18–21, IG-23–26, IG-30.
- **Batch C / transfers and bank:** IG-07–08, IG-11, IG-14–16, IG-27–29, IG-31.

All status cells intentionally remain **not executed**; a source finding or prior synthetic test is not automatically credit for these more specific acceptance scenarios.
