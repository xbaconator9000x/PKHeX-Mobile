# Acceptance-gated implementation roadmap

This describes completion criteria, **not dates or percent-complete estimates**.

## Milestone 0 — project foundation / technical decision

- [x] Create repository boundaries, GPLv3 licence and credits policy.
- [x] Document fail-closed save-write requirements and add a minimal domain contract.
- [x] Add starter code checks and CI.
- [ ] Pin exact commits and toolchains for PKHeX, PKForge and PKHeX MAUI.
- [ ] Audit code structures, save read/write, UI, assets, dependency rights and update paths.
- [ ] Build a file/class-level desktop feature inventory and mobile parity matrix.
- [ ] Choose original/adapted Android UI with evidence.

**Gate:** recorded architecture decision informed by source and reproducible test runs.

## Milestone 1 — genuine read-only save support

- [ ] Pin and integrate PKHeX.Core with tracked version provenance.
- [ ] Add Android document picker, input fingerprints and supported-format detection.
- [ ] Show real party, boxes and trainer info; reject unrecognized variants.
- [ ] Compare readings against the same pinned desktop PKHeX core across legitimate synthetic/consented fixtures.

**Gate:** verified save reads for every claimed game/format.

## Milestone 2 — save safety and edits

- [ ] Edit supported Pokémon and box slots through PKHeX.Core.
- [ ] Support backup and Save As without mutating the original.
- [ ] Stage and independently validate outputs, re-open after serialize.
- [ ] Compare intended and unintended changes, checksum behavior and round trips.
- [ ] Test concurrent file edits, interruption, invalid file access and recovery.

**Gate:** real file tests prove safe export/restore for each enabled profile.

## Milestone 3 — PKHeX desktop parity

- [ ] Pokémon details, stats, origins, ribbons, moves, memories and per-game data.
- [ ] Party/box operations, entity import/export, transfers and Showdown sets.
- [ ] Legality reporting identical to pinned core and correctly scoped.
- [ ] Trainer, bag, Pokédex, Mystery Gift, events, batch and generation-specific editors.
- [ ] Localization and accessibility.

**Gate:** feature-by-feature supported / adapted / missing checklist, no silent omissions.

## Milestone 4 — polished Android port

- [ ] Touch/Back, responsive phone/tablet views, accessibility and gamepad where useful.
- [ ] Scoped storage and optional emulator integrations within supported Android permissions.
- [ ] Physical-device testing, performance, recovery, signed APK builds and provenance.

**Gate:** verified device/emulator compatibility matrix and repeatable release.

## Milestone 5 — ROM-hack support & PokéBacon interoperability

- [ ] Versioned profiles with authenticated save structure and custom species IDs, moves, items and abilities.
- [ ] Independent compatibility checks; unknown games remain read-only.
- [ ] Specific verified support for Shattered and other hacks, each with round-trip fixtures.
- [ ] An optional versioned PokéBacon profile interchange.

**Gate:** positive and negative profile tests; byte preservation and failure-mode evidence.
