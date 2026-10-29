# World-state RTC, roamer, honey-tree and Hall of Fame acceptance gates — pass T (2026-10-28)

**40 unique proposed cases; zero executed/certified.** Source tickets: [UPSTREAM_WORLD_STATE_REVIEW_2026-10-28.md](UPSTREAM_WORLD_STATE_REVIEW_2026-10-28.md).

For every test pin PKHeX.Core binary/source revision, same-version desktop PKHeX 26.10.18, exact game/format/language, source SHA-256, raw field offsets, intended byte-change allowlist, checksums, staged output, backup and rollback identity. Use synthetic or privately consented fixtures only; never commit game saves, ROMs, commercial assets or keys. Independent desktop re-open is distinct from same-Core synthetic re-open.

| Gate | Area | Observable acceptance requirement | State |
| --- | --- | --- | --- |
| T-R01 | RTC | RS/E raw initial and elapsed RTC bytes read without modification. | Proposed — not executed |
| T-R02 | RTC | Days 65535 retained; -1 and 65536 refused. | Proposed — not executed |
| T-R03 | RTC | Hour 24/255, minute 60/255, second 60/255 refused. | Proposed — not executed |
| T-R04 | RTC | Invalid final RTC input after valid earlier inputs causes zero mutation. | Proposed — not executed |
| T-R05 | RTC | Berry Fix invalid/empty/overflow text fails without crash. | Proposed — not executed |
| T-R06 | RTC | Berry Fix 734 threshold and greater elapsed days preserve unrelated data. | Proposed — not executed |
| T-R07 | RTC | Reset and arm-reset are distinct authenticated actions. | Proposed — not executed |
| T-R08 | RTC | Cancel, Back, process death and provider revoke preserve source. | Proposed — not executed |
| T-R09 | RTC | RS/E RTC raw bytes independently match desktop PKHeX 26.10.18. | Proposed — not executed |
| T-O01 | Roamer | Immutable read of PID, IV32, HP, status, level, species and active flag. | Proposed — not executed |
| T-O02 | Roamer | Negative and >31 IV inputs rejected before any Core setter. | Proposed — not executed |
| T-O03 | Roamer | RS/FRLG glitched IV calculation never mutates live bytes. | Proposed — not executed |
| T-O04 | Roamer | Emerald vs RS/FRLG catch-IV difference matches desktop. | Proposed — not executed |
| T-O05 | Roamer | Current HP parity and derived shiny semantics verified. | Proposed — not executed |
| T-O06 | Roamer | Failed reroll/HP calculation restores PID/IV/HP exactly. | Proposed — not executed |
| T-O07 | Roamer | Gen3/4 Method1, Gen5 independent and X/Y no-PID semantics separated. | Proposed — not executed |
| T-O08 | Roamer | Modal opened on A cannot edit B or stale A. | Proposed — not executed |
| T-O09 | Roamer | Synthetic RNG tests distinguished from desktop-matched legality. | Proposed — not executed |
| T-H01 | Honey tree | Exactly 21 indices 0–20; -1 and 21 rejected. | Proposed — not executed |
| T-H02 | Honey tree | Index 21 adjacent-data sentinel never published. | Proposed — not executed |
| T-H03 | Honey tree | Invalid group/slot displays raw diagnostic, no crash. | Proposed — not executed |
| T-H04 | Honey tree | Noncanonical SubTable survives no-op inspect/export. | Proposed — not executed |
| T-H05 | Honey tree | Trainer ID 1935328924 produces three distinct Munchlax trees. | Proposed — not executed |
| T-H06 | Honey tree | Illegal Munchlax placement refused before write. | Proposed — not executed |
| T-H07 | Honey tree | Timer 1080 and 1440 match desktop; 1441 refused. | Proposed — not executed |
| T-H08 | Honey tree | No silent clamp on invalid tree requests. | Proposed — not executed |
| T-H09 | Honey tree | Tree-switch and Cancel preserve all raw bytes. | Proposed — not executed |
| T-H10 | Honey tree | DPPt and BDSP versions authenticated separately. | Proposed — not executed |
| T-H11 | Honey tree | Invalid species ID uses numeric fallback. | Proposed — not executed |
| T-H12 | Honey tree | 21 raw 8-byte trees match independent desktop fixture. | Proposed — not executed |
| T-F01 | Hall of Fame and safety | Hall two-sector read is detached and byte-preserving. | Proposed — not executed |
| T-F02 | Hall of Fame and safety | 49/51 entries rejected before SetEntries. | Proposed — not executed |
| T-F03 | Hall of Fame and safety | 50×6 packed species/level and nickname trash preserved. | Proposed — not executed |
| T-F04 | Hall of Fame and safety | Hall Cancel preserves both sectors. | Proposed — not executed |
| T-F05 | Hall of Fame and safety | Party import and Shift-all remain explicitly unsupported. | Proposed — not executed |
| T-F06 | Hall of Fame and safety | Source identity, backup, staging and rollback gate every export. | Proposed — not executed |
| T-F07 | Hall of Fame and safety | Unknown ROM-hack layout never writable as vanilla. | Proposed — not executed |
| T-F08 | Hall of Fame and safety | Provider revocation and stale document fail closed. | Proposed — not executed |
| T-F09 | Hall of Fame and safety | Desktop PKHeX 26.10.18 independently reopens claimed outputs. | Proposed — not executed |
| T-F10 | Hall of Fame and safety | GPL source and separately licensed art inventories verified. | Proposed — not executed |

**Execution priority:** T-H01/H02 (index 21), T-R03/R04 (RTC partial input), T-O02/O03/O06 (packed IV, purity, rollback), T-H04 (subtable), T-F02 (entry count), then T-F06/F07/F09 (export, hack rejection, independent desktop). All edits and exports remain disabled pending their relevant gates. Milestones 0 and 1 are not complete.
