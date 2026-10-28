# Source-level comparison plan

> [Party, box storage, pointer and bulk-mutation source audit, pass N (2026-10-28)](UPSTREAM_STORAGE_SLOT_REVIEW_2026-10-28.md) — 37 pinned findings (13 P0), including source-level pointer range inversion, MoveBox permutation loss and protected-slot bypass; no runtime certification.
> [Storage, party and pointer execution gates, pass N (2026-10-28)](PARITY_STORAGE_SLOT_GATES_2026-10-28.md) — 42 proposed scenarios; none executed or certified.

> [Inventory, pouch, item flags and save persistence source audit, pass M (2026-10-28)](UPSTREAM_INVENTORY_REVIEW_2026-10-28.md) — 30 source-backed findings, 17 official files compared (16 matching, one changed) and fail-closed inventory write hazards.
> [Inventory execution gates, pass M (2026-10-28)](PARITY_INVENTORY_GATES_2026-10-28.md) — 38 proposed scenarios; none executed or certified.

> [Pokédex and acquisition source audit, pass L (2026-10-28)](UPSTREAM_POKEDEX_REVIEW_2026-10-28.md) — 29 findings including MAUI row shift, Forge Unbound no-op and Gen9 block side effects.
> [Pokédex and regional dex execution gates, pass L (2026-10-28)](PARITY_POKEDEX_GATES_2026-10-28.md) — 40 proposed cases; none executed or certified.

> [Ribbon, mark, memory, Pokérus and cosmetics source audit, pass K (2026-10-28)](UPSTREAM_RIBBON_MEMORY_REVIEW_2026-10-28.md) — 28 pinned findings; Core 111-entry ribbon enum, MAUI off-by-one and memory picker hazards, Forge typed awards and licensing obligations.
> [Ribbon and memory execution gates, pass K (2026-10-28)](PARITY_RIBBON_MEMORY_GATES_2026-10-28.md) — 36 proposed scenarios; none executed or certified.


> [Text encoding, trash bytes, Unicode and mobile editor lifecycle, pass J (2026-10-28)](UPSTREAM_TEXT_ENCODING_REVIEW_2026-10-28.md) — 26 source-backed findings, 15 identical pinned Core source blobs, with MAUI cancel/spin and string-integrity handoff.
> [Text encoding and lifecycle execution gates, pass J (2026-10-28)](PARITY_TEXT_ENCODING_GATES_2026-10-28.md) — 40 proposed acceptance scenarios; none executed or certified.

> [Entity identity, conversion and PKM field semantics, pass I (2026-10-28)](UPSTREAM_ENTITY_CONVERSION_REVIEW_2026-10-28.md) — 24 source-backed findings, eight same-source Git blobs verified against Core 26.10.18 and Gen 1/2 field caveats.
> [Entity conversion and format acceptance gates, pass I (2026-10-28)](PARITY_ENTITY_CONVERSION_GATES_2026-10-28.md) — 32 proposed tests; none executed or certified.


> [Mystery Gift, stored-card and event archive source audit, pass H (2026-10-28)](UPSTREAM_MYSTERY_GIFT_REVIEW_2026-10-28.md) — 20 pinned source findings including WA9 clone/context, Gen5 encryption, mobile gesture routing and gift delivery transactions.
> [Mystery Gift execution gates, pass H (2026-10-28)](PARITY_MYSTERY_GIFT_GATES_2026-10-28.md) — 27 proposed tests; none executed or certified.


> [Legality, encounter and batch source audit, pass G (2026-10-28)](UPSTREAM_LEGALITY_ENCOUNTER_REVIEW_2026-10-28.md) — 22 pinned, actionable findings, with global legality context and batch-write safety risks.
> [Legality/encounter/batch execution gates, pass G (2026-10-28)](PARITY_LEGALITY_ENCOUNTER_GATES_2026-10-28.md) — 30 proposed tests; none executed or certified.


> [Editor-dispatch, world-service and transfer audit, pass F (2026-10-28)](UPSTREAM_EDITOR_DISPATCH_REVIEW_2026-10-28.md) — 24 source-grounded findings with exact pinned routes, version-aware coverage and write-safety handoff.
> [Editor/transfer execution gates, pass F (2026-10-28)](PARITY_EDITOR_DISPATCH_GATES_2026-10-28.md) — 28 proposed cases; none executed or certified.

> [Android follow-up source audit, pass E (2026-10-28)](UPSTREAM_ANDROID_FOLLOWUP_REVIEW_2026-10-28.md) — 16 source-backed findings, updated Android cancellation/box/CI status and upstream transaction risks.
> [Android follow-up execution gates, pass E (2026-10-28)](PARITY_ANDROID_FOLLOWUP_GATES_2026-10-28.md) — 24 targeted unexecuted acceptance scenarios; no device/desktop certification.
> [Android prototype, lifecycle, build and UI source audit, pass D (2026-10-28)](UPSTREAM_ANDROID_PROTOTYPE_REVIEW_2026-10-28.md) — verified first Android build, CI trigger gap, URI identity limitations, lifecycle and source-level UI comparisons.
> [Android device, lifecycle and reproducibility gates, pass D (2026-10-28)](PARITY_ANDROID_DEVICE_GATES_2026-10-28.md) — 22 proposed provider, lifecycle, UI and build acceptance scenarios; none claimed device-executed.
> [Pinned I/O, container and bank integrity audit, pass C (2026-10-28)](UPSTREAM_IO_BANK_REVIEW_2026-10-28.md) — source-backed SAF publication, bank replacement, backup durability and emulator-envelope findings.
> [Container/SAF/bank acceptance gates, pass C (2026-10-28)](PARITY_IO_BANK_GATES_2026-10-28.md) — 25 proposed deterministic negative/positive scenarios; none claimed executed.
> [Fork delta and source-level disposition, pass B (2026-10-28)](UPSTREAM_FORK_DELTA_2026-10-28.md) — all 21 fork-side changed paths, checksum caveats and pinned package drift.
> [Parity verification and test gates, pass B (2026-10-28)](PARITY_VERIFICATION_PLAN_2026-10-28.md) — source-level MAUI/Forge defects, Android/provider scenarios and independent test protocol.
> [Pinned source audit, pass A (2026-10-27)](UPSTREAM_SOURCE_AUDIT_2026-10-27.md) — verified fork divergence, Android write hazards, licensing and implementer tickets.
>
> [Desktop/mobile parity crosswalk, pass A (2026-10-27)](PARITY_MATRIX_2026-10-27.md) — 28 representative capability rows, source links and untested gates.
>
> [Initial pinned source findings (2026-10-27)](UPSTREAM_CHECKPOINT_2026-10-27.md) — does not yet satisfy the full build or parity audit.

This is a work plan, **not a completed source audit**. Exact references to commits, classes, tests and reproducible observed behavior are still required.

| Project | Role | Current knowledge | Key verification |
|---|---|---|---|
| [PKHeX](https://github.com/kwsch/PKHeX) | Official-game save editing reference | Desktop WinForms; PKHeX.Core; GPLv3 | Exact editor inventory; formats; save writes; legality |
| [PKForge](https://github.com/sofianeelhor/PKForge) | Android adaptation candidate | MAUI and PKHeX.Core; advertises bank, backups and selected hacks | Actual coverage, safe writer, assets, adapter architecture |
| [PKHeX MAUI](https://github.com/santacrab2/PKHeXMAUI) | Android adaptation candidate | Mobile PKHeX-based UI; explicitly warns against ROM-hack use | Parity, writes, dependencies, update cost |
| PKHeX Mobile | Our new project | Pinned official Core read-only projection, bounded two-open document service, offline comparison harness and compiled native Android read-only prototype; no save write path | Independent real-save and device/provider tests still open |

## Required evidence

- Pin upstream commit SHAs and build each with a reproducible SDK/toolchain.
- Map each desktop PKHeX editor, tab, operation and format to matching mobile implementation, if any.
- Inspect PKHeX.Core adapter, I/O, output validation, backups and concurrency behavior.
- Verify legality and generation transfers against the exact same pinned engine.
- Study emulator folder access, Android document providers, layout and accessibility.
- Review source licences and separately licensed app artwork before import.
- Record missing features and estimate continuing upstream maintenance cost.
- Choose adaptation or fresh presentation layer only after these comparisons.

Reject architectures that trade away save safety, attribution or future desktop parity.
