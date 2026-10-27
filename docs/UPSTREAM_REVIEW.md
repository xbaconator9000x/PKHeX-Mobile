# Source-level comparison plan

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
