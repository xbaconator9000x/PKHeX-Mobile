# Trainer, records and event-work acceptance gates — pass O (2026-10-28)

**36 proposed scenarios; none executed by this audit.** The [source audit](UPSTREAM_TRAINER_EVENT_REVIEW_2026-10-28.md) contains 30 pinned findings. Synthetic tests, desktop-matched fixtures, provider tests and real-device tests are separate evidence classes.

Every executed gate must record exact pinned Core SHA/package and SDK, fixture provenance/consent, authenticated save layout, original SHA-256, requested fields, changed byte ranges, checksum/block effects, independent reopen, same-version desktop output, provider identity and device/API when applicable. Negative cases must prove no bytes were published and original SHA-256 is unchanged.

## Isolation

| ID | Proposed action / required pass condition | State |
|---|---|---|
| O-G01 | Gen6 record then Cancel byte-identical | Not run |
| O-G02 | Gen6 fashion binding then Back byte-identical | Not run |
| O-G03 | Gen9 Max Money/LP then Cancel byte-identical | Not run |
| O-G04 | Replace save during Gen9 tabs; stale edits rejected | Not run |
| O-G05 | Gen9 fly/stakes/TM/bike/fashion then Cancel unchanged | Not run |
| O-G06 | Gen9 Blueberry bulk actions then Cancel unchanged | Not run |
| O-G07 | Multi-tab failure rolls back entire draft | Not run |
| O-G08 | Android rotation/background keeps draft isolated | Not run |

## Trainer and identity

| ID | Proposed action / required pass condition | State |
|---|---|---|
| O-G09 | Gen9 LastSaved date-only/time-only/combined desktop match | Not run |
| O-G10 | Unchanged OT raw trash bytes preserved | Not run |
| O-G11 | Gen6 formatted Max Cash persists or reports error | Not run |
| O-G12 | Gen6/7 invalid version selection rejected | Not run |
| O-G13 | Gen6 null country/region/language rejected | Not run |
| O-G14 | Gen7 AlolaTime sentinel 9,999,999 nonthrowing | Not run |
| O-G15 | Gen7 invalid later field leaves earlier fields unchanged | Not run |
| O-G16 | Gen9 map rotation persisted or explicitly unsupported | Not run |
| O-G17 | Gen7+ display trainer ID profile round-trip | Not run |

## Records and events

| ID | Proposed action / required pass condition | State |
|---|---|---|
| O-G18 | Record IDs 0,99,100,199 width and sign checks | Not run |
| O-G19 | Negative/overflow record values rejected | Not run |
| O-G20 | Event flag old/new array count mismatch rejected | Not run |
| O-G21 | Event work old/new count mismatch rejected | Not run |
| O-G22 | Same block type different game/version rejected | Not run |
| O-G23 | Malformed typed labels cannot authorize writes | Not run |
| O-G24 | Sparse event mutation changes only named indices | Not run |
| O-G25 | Gen2/Gen3/Let's Go work range boundary fixtures | Not run |
| O-G26 | BDSP system and event bank remain distinct | Not run |
| O-G27 | Z-A 64/128/192-bit event families coverage report | Not run |

## Export and format

| ID | Proposed action / required pass condition | State |
|---|---|---|
| O-G28 | Unbound/Radical Red/CFRU-like input cannot enter vanilla writer | Not run |
| O-G29 | Trainer TID/SID/money out-of-range returns explicit error | Not run |
| O-G30 | Provider failure backup SHA256 equals original | Not run |
| O-G31 | Save As staged and independently reopened before publish | Not run |
| O-G32 | SAF URI changed between open and commit rejected | Not run |
| O-G33 | Immutable bounded read-only trainer records with maxima | Not run |
| O-G34 | Independent Gen6/Gen7/SV desktop trainer fixtures | Not run |
| O-G35 | Independent Gen2/Gen3/7b/BDSP/ZA desktop event fixtures | Not run |
| O-G36 | Android release trim and device/provider lifecycle matrix | Not run |

## Release boundary

A bounded read-only trainer/record/event display can ship with explicit unsupported labels. Do not enable trainer editing, event flag toggles, bulk unlocks, Save As, in-place export or custom-hack writing from this checklist. Require independent same-version desktop fixtures, exact write diffs, backup restoration and Android SAF interruption/identity testing before any write authorization.
