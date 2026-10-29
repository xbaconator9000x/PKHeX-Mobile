# HGSS Pokéathlon, Safari and gadget parity gates — pass V (2026-10-28)

**All 36 cases are proposed, not executed.** Findings: [source audit pass V](UPSTREAM_HGSS_GADGETS_REVIEW_2026-10-28.md). Same-version desktop PKHeX independent fixtures and real Android provider tests remain missing. Core package is pinned to 26.10.18; newer Safari source APIs are **not present in this release**. No save editing/export authorized.

| Gate | Acceptance test and required evidence | Finding IDs | Result |
| --- | --- | --- | --- |
| V-G01 | **Safari release pin:** Assert seven Safari files and property absent in 26.10.18 but present at newer SHA. | V01 | Not run |
| V-G02 | **Safari upgrade:** Before enabling Safari, compare release and package, compile and run independent fixtures. | V01 | Not run |
| V-G03 | **Safari age flush:** Distinct 12+12 ages survive Save/reopen; reproduce wrong flush order. | V02,V23 | Not run |
| V-G04 | **Safari switcher:** Raw states 0–3 survive no-op; reject unknown-state edits. | V03 | Not run |
| V-G05 | **Safari auto-count no-op:** Thirty unsorted objects and holes survive untouched save byte-for-byte. | V04 | Not run |
| V-G06 | **Safari explicit sort:** Requested normalization has complete byte diff and rollback. | V04 | Not run |
| V-G07 | **Safari unknown enum:** 0xFF and 0x7F type bytes are preserved after no-op. | V05 | Not run |
| V-G08 | **Medal high bits:** 493 medal bytes with high bits survive no-op and unrelated edit. | V06,V22 | Not run |
| V-G09 | **Shop high bits:** 0xF001 and 0xAFFF preserve top four bits. | V07,V18 | Not run |
| V-G10 | **Data Card high bits:** 0xF8000001 and 0x88000000 preserve top five bits. | V08,V18 | Not run |
| V-G11 | **MAUI repeated All:** Repeated load/All/Save does not append or overflow 32 slots. | V09 | Not run |
| V-G12 | **Walker reserved flags:** Bits 27–31 survive any single course edit. | V10,V15,V27 | Not run |
| V-G13 | **Walker cancellation:** Unlock All then Back/Cancel/rotation preserves original hash. | V11 | Not run |
| V-G14 | **Pokégear cancellation:** Give All/Clear then Back/Cancel preserves original bytes. | V12 | Not run |
| V-G15 | **Apricorn index:** -1, 7 and large index reject without adjacent mutation. | V13 | Not run |
| V-G16 | **Caller index:** -1, roster count and huge index reject before Core. | V14 | Not run |
| V-G17 | **Forge language All:** JP/KR/INT masks and reserved high bits compared. | V15,V28 | Not run |
| V-G18 | **Points no-op:** Raw uint32 above 99,999 unchanged on unrelated edit. | V16 | Not run |
| V-G19 | **Counters no-op:** 0xFFFFFFFF in raw counters survives read/unrelated edits. | V17 | Not run |
| V-G20 | **Detached read:** No Core live slice escapes DTO; original hash unchanged. | V19 | Not run |
| V-G21 | **Course/event bounds:** 5 course, 10 self, 10 connection, 5 trainer cardinalities and boundary checks. | V20 | Not run |
| V-G22 | **Packed identity:** Participant and event species/form packed bits match independent desktop. | V21 | Not run |
| V-G23 | **Medal boundaries:** Species 0/494 reject; 1/493 correct and count masked bits. | V22 | Not run |
| V-G24 | **Self/link isolation:** Distinct 6+6 configurations and 12+12 ages do not cross-write. | V23 | Not run |
| V-G25 | **Safari invalid indices:** Map 12/255 and configuration 6 reject before accessor. | V24 | Not run |
| V-G26 | **Link-leader raw fields:** Signed 64-bit fields, ID32 and 16 name bytes match desktop. | V25 | Not run |
| V-G27 | **Area envelope:** Only exact 0x7A area blob accepted; ±1 and full save refused. | V26 | Not run |
| V-G28 | **Walker cardinality:** 27 visible names and 32 preserved flags after repeated binding. | V27 | Not run |
| V-G29 | **Walker language:** JP/KR/INT course availability matches pinned desktop. | V28 | Not run |
| V-G30 | **Encounter resource rights:** No binary or art import without provenance. | V29,V32 | Not run |
| V-G31 | **Pokégear edit persistence:** One selected caller changes exact slot after Save/reopen. | V30 | Not run |
| V-G32 | **Apricorn semantics:** Seven bytes, 0–99 UI and out-of-range raw preservation. | V13 | Not run |
| V-G33 | **SAF rollback:** Interrupted/stale/replaced provider retains original hash. | V31,V33 | Not run |
| V-G34 | **Licensing:** Verify upstream code notices and per-asset rights before copying. | V29,V32 | Not run |
| V-G35 | **Independent HGSS parity:** Privately held desktop-matched HGSS and negative hack fixtures per claimed format. | V33 | Not run |
| V-G36 | **Hack refusal:** Plausible vanilla header with custom HGSS layout remains non-exportable. | V01,V33 | Not run |

## Required evidence protocol

For every executed test, record exact source/package SHA, fixture provenance and consent, game/layout identity, input SHA-256 and length, expected/observed raw byte ranges, exception behavior, output SHA-256, independently matched same-version desktop baseline, and CI or device-run URL. Synthetic Core-generated fixtures can reproduce hazards but cannot alone certify desktop parity. Keep private saves, ROMs, paid assets, trainer identity and signing keys out of Git. For any future writer, require authenticated format, staged output, backup, independently reopened validation, unexpected-byte diff and tested rollback. Failures block only the affected capability/profile, never authorize fallback vanilla export.
