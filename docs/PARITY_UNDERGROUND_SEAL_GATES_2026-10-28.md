# Underground, seal, sticker and capsule parity gates — pass U (2026-10-28)

**Status:** 36 unique proposed scenarios, **0 executed**, **0 independently desktop-certified**, **0 Android-provider-certified**. This is a test specification, not a pass report. Linked source findings: [pass U](UPSTREAM_UNDERGROUND_SEAL_REVIEW_2026-10-28.md).

## Execution protocol

Use synthetic, rights-cleared, game-version-specific fixtures with immutable input SHA-256 and exact PKHeX.Core 26.10.18 provenance. For any positive claim, compare raw fields and byte diffs against an **independently produced same-version desktop PKHeX** reference, not just a second call to the same adapter. On Android, test actual document-provider open/close, revocation, replaced content, cancellation, staging, backups and recovery. Never include ROMs, paid assets, console keys or personal saves in Git. Every failed, skipped and unsupported case must be recorded explicitly.

| Gate | Required scenario and pass condition | Current evidence |
| --- | --- | --- |
| G01 | Gen4 open, close, reopen distinct saves; assert correct save SHA and no stale static view. | Proposed; not executed |
| G02 | Open Gen4 editor on a non-Sinnoh save; no cast crash or state bleed. | Proposed; not executed |
| G03 | Navigate Save tab, Back and Cancel without editing; original bytes unchanged. | Proposed; not executed |
| G04 | Inject exception after each of five MAUI Save steps; original remains identical. | Proposed; not executed |
| G05 | Delete first goods entry with nonzero tail; expected 40-byte compacted layout. | Proposed; not executed |
| G06 | Delete middle and last goods entries; no stale tail. | Proposed; not executed |
| G07 | Delete traps entry with nonzero tail; all 40 bytes correct. | Proposed; not executed |
| G08 | Delete treasures entry with nonzero tail; all 40 bytes correct. | Proposed; not executed |
| G09 | Delete spheres entry; both 40-byte type and size halves correct. | Proposed; not executed |
| G10 | Malformed goods ID beyond localization table; no exception or mutation. | Proposed; not executed |
| G11 | Malformed traps ID; numeric raw fallback and no mutation. | Proposed; not executed |
| G12 | Malformed treasures ID; numeric raw fallback and no mutation. | Proposed; not executed |
| G13 | Malformed sphere ID and nonzero size; raw fallback and no mutation. | Proposed; not executed |
| G14 | Unknown localized label and duplicate display label; reject ambiguous name-to-ID. | Proposed; not executed |
| G15 | No-op Gen4 score edit with 1000000 and uint.MaxValue; preserve all 13 u32 fields. | Proposed; not executed |
| G16 | Read all 40+40+40+80 Gen4 pouch bytes; compare same-version desktop raw projection. | Proposed; not executed |
| G17 | Gen4 underground Cancel, modal dismiss and parse failure preserve source SHA. | Proposed; not executed |
| G18 | Gen4 seal 80 counts; 77 legal and 3 unreleased slots distinguished. | Proposed; not executed |
| G19 | Gen4 seal no-op edit preserves unusual stored count and all other bytes. | Proposed; not executed |
| G20 | BDSP underground exactly 999×12 bytes; read-only snapshot has no shared buffer. | Proposed; not executed |
| G21 | BDSP count-only edit with untouched flag raw u32=2; no unintended flag rewrite. | Proposed; not executed |
| G22 | BDSP raw negative count and Max+1 surfaced without silent normalization. | Proposed; not executed |
| G23 | BDSP invalid ID -1, 999 and type None rejected without mutation. | Proposed; not executed |
| G24 | BDSP over-max count rejected; never silently clamp on a write path. | Proposed; not executed |
| G25 | BDSP bulk fill/clear with injected failure after N entries; no partial publication. | Proposed; not executed |
| G26 | Unknown ROM-hack save matching vanilla size/header never permits export by confirmation. | Proposed; not executed |
| G27 | Independent same-version desktop PKHeX BDSP underground fixture; compare 999 entries and flags. | Proposed; not executed |
| G28 | Android SAF provider revocation, replacement, stale URI and interruption; backup restore exact bytes. | Proposed; not executed |
| G29 | BDSP stickers 200×12, compare raw obtained/current/total fields. | Proposed; not executed |
| G30 | BDSP sticker untouched obtained raw=2 survives unrelated edit or export refused. | Proposed; not executed |
| G31 | BDSP capsules exactly 99×20 affixes; detached view, no mutable Core Memory. | Proposed; not executed |
| G32 | BDSP affix ushort ID and signed X/Y/Z extrema round-trip independently. | Proposed; not executed |
| G33 | BDSP New/Favorite flags exposed separately; count-only Forge UI not labelled full parity. | Proposed; not executed |
| G34 | Build/redistribution audit: GPL notices, source availability, no unlicensed images/fonts/ROM assets. | Proposed; not executed |
| G35 | Desktop independent fixture provenance includes revision, SHA, game/build and explicit rights. | Proposed; not executed |
| G36 | Verify original SHA and byte diff after every no-op view, selection and refresh. | Proposed; not executed |

## Prioritized execution batches

1. **P0 corruption prevention:** G01–G15, G20–G26, G30. Implementer first isolates the upstream hazards with adjacent-data sentinels and fail-closed wrappers. Do not enable a write route to demonstrate a defect.
2. **Raw projection/parity:** G16–G19, G27, G29, G31–G33, G35–G36. Record independent desktop input/output evidence, not inferred parity from class names or screenshots.
3. **Android publication and licensing:** G28, G34. Confirm scoped-storage semantics and original-byte restore, with no imported third-party artwork.

## Acceptance record template

For each gate capture: gate ID; source and implementation SHA; game/build/layout; fixture provenance and rights; input SHA-256; independent desktop reference/version; exact command/device/API/provider; expected vs observed raw bytes/fields; output SHA-256; failure/rollback result; CI run URL; and final status (pass/fail/blocked/not-run). A green build or synthetic test alone cannot advance a save-export gate.

**Hard stop:** Milestone 0 remains incomplete; Milestone 1 lacks independent fixture/device certification. No vanilla or ROM-hack save writer is authorized by this matrix.
