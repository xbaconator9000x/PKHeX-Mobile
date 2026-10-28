# Stat, Tera and Move Shop acceptance gates — pass P (2026-10-28)

**38 proposed scenarios; 0 executed or certified.** Based on [28 pinned source findings](UPSTREAM_STAT_TERA_MOVE_REVIEW_2026-10-28.md), of which 10 are P0 safety blockers. A compiled debug APK or synthetic test does not imply desktop parity, device certification or save-write safety.

For each eventual execution record exact Core source/package SHA and SDK, authenticated game/format/layout, fixture provenance/consent, original SHA-256, document/slot identity, expected and observed raw fields, changed byte ranges, checksum and side effects, independent same-version desktop PKHeX comparison, independent reopen, Android provider/API/device when relevant, and test log/CI artifact. For negative cases assert **no publication** and unchanged original and backup SHA-256.

## Read-only source integrity

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G01 | View an entity's stat page without interaction; assert all original entity/party bytes and source hash remain identical. | Not run |
| P-G02 | Navigate Stats→Box→Stats repeatedly; verify no derived party fields change. | Not run |
| P-G03 | Open two different entities; ensure each immutable snapshot is bound to its own slot and document fingerprint. | Not run |
| P-G04 | Replace source while a stat modal is open; stale save or Back must not affect new document. | Not run |
| P-G05 | Rotate/background/restore Android UI while inspecting; verify same fingerprint, no mutable Core instance escapes. | Not run |
| P-G06 | Malformed and unsupported ROM-hack entity is read-only with raw-unknown labels; no vanilla write route. | Not run |

## EV/IV generation matrix

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G07 | Gen1/2 EV per-stat values 0, 252, 255, 256, 65535 retain exact values and matching desktop readback. | Not run |
| P-G08 | Gen1/2 HP IV/DV and Special stat rules match pinned desktop, without invented independent HP IV. | Not run |
| P-G09 | Gen3/5 EV values 252, 253, 255 retained; 256 rejected without mutation. | Not run |
| P-G10 | Gen6/9 EV values 252 accepted; 253 rejected as invalid edit; 510 total accepted, 511 rejected. | Not run |
| P-G11 | EV total 508 vs 510 reported distinctly; no automatic 508 normalization. | Not run |
| P-G12 | Egg and level-100 EV legality messages match same pinned desktop context. | Not run |
| P-G13 | Random/suggested EV generation uses correct per-format path and explicit preview; cancellation byte-identical. | Not run |
| P-G14 | Hidden Power change compares all six stored IVs, HP type and power with pinned desktop. | Not run |
| P-G15 | All six stat orders are round-tripped in canonical HP/Atk/Def/Spe/SpA/SpD order. | Not run |

## Training and nature

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G16 | Gen7/8/9 stored IVs and six hyper flags separately projected, including 0x00 and 0x3F. | Not run |
| P-G17 | Hyper flags on level 49/50 Gen9 and level 99/100 Gen8 produce pinned legality outcomes. | Not run |
| P-G18 | Flawless IV with HT flag reports legality warning; no silent correction. | Not run |
| P-G19 | Gen8+ Nature and StatAlignment/mint remain independent and match desktop derived stats. | Not run |
| P-G20 | Gen1/2 nature UI absent; Gen3/4 PID-linked nature changes have explicit side-effect diff. | Not run |
| P-G21 | PLA AV/GV and Let's Go awakened values are distinguished from EVs and tested with correct maxima. | Not run |

## Tera raw and legality

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G22 | SV Tera original and override 0..17, 19 and 99 project both raw codes and effective type. | Not run |
| P-G23 | Raw Tera invalid values 18, 20, 255 are reported unsupported, never silently normalized. | Not run |
| P-G24 | Ogerpon all four forms and Terapagos enforce pinned species-specific rules. | Not run |
| P-G25 | Tera picker never treats enum Any as a valid stored type or conflates None/Stellar. | Not run |
| P-G26 | Tera edit cancel/back preserves original and override bytes exactly. | Not run |

## Move Shop

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G27 | PLA permit list is displayed in stable index order with move ID, purchased, mastered and permitted flag. | Not run |
| P-G28 | Invalid/unavailable permit indexes and non-PLA entity reject edits with zero changes. | Not run |
| P-G29 | Two concurrent Move Shop modals cannot share rows or apply to wrong entity. | Not run |
| P-G30 | Bulk Set Known/Set All/Clear show precise scope and can cancel with byte-identical rollback. | Not run |
| P-G31 | Exception at row N rolls back all purchased/mastered changes and preserves backup. | Not run |
| P-G32 | Purchased/mastered flags match same-version desktop for a consented PLA fixture. | Not run |

## Export boundary

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G33 | Save As requires authenticated format, original identity, backup and independent Core/desktop reopen. | Not run |
| P-G34 | Stat edits enumerate exact changed byte ranges, checksum and party-stat side effects. | Not run |
| P-G35 | SAF provider failure and concurrent external modification leave original and backup hashes unchanged. | Not run |
| P-G36 | Android release trimming and two independent document providers pass real-device inspection. | Not run |

## Provenance

| Gate | Required action / pass condition | State |
| --- | --- | --- |
| P-G37 | Pin Core package and source commit, verify source equality and independently generated desktop expectations. | Not run |
| P-G38 | Confirm GPLv3 obligations and no imported unlicensed sprites, game assets, ROMs or private saves. | Not run |

## Hard release boundary

These tests are **not passed**. The existing read-only viewer may only show accurately labelled diagnostic values, not assert full stat/Move Shop parity. No editing, Save As, in-place export or ROM-hack writes may be enabled based on this audit. Independent consented desktop-matched fixtures, safe layout authentication, rollback and physical SAF provider interruption tests remain mandatory.
