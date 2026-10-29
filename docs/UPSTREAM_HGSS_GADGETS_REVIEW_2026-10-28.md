# HGSS Pokéathlon, Safari and gadget source audit — pass V (2026-10-28)

**Source-level findings only; no runtime reproductions or independently matched desktop fixtures in this pass. No write authorization.** Immutable pins: PKHeX `7e26bc53215e4f7bc0041ca375f1042539cb81b3`; PKForge `2df04430e532b78c247f45b936f3068d8f824987`; PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`. The Android adapter pins official PKHeX.Core **26.10.18**, release source commit `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`, not the newer audit SHA.

## Critical Core release-drift gate

Compared **23** relevant Core file paths by Git tree blob SHA between the newer PKHeX audit commit and the pinned 26.10.18 source release: **15 identical, seven Safari substructure files absent in the release, and one changed** (`SAV4HGSS.cs`, which adds the Safari property). This is source-tree evidence, **not** a reproducible NuGet package-to-commit binary proof. The mobile project cannot assume the newer Safari API exists in its pinned package. No Core upgrade is authorized by this audit.

## Evidence accounting

**33 distinct findings** (15 P0, 18 P1), **86 finding-level immutable source links** across **31 distinct files**, and **36 proposed, unexecuted acceptance scenarios**. Sources were inspected at the exact SHAs above; the cited line ranges were checked against retrieved source. P0 means *blocker before write enablement*, not an independently reproduced Android defect. No source code or assets were copied.

## Findings and implementer tickets

### V01 — P0: Safari API missing from pinned release

Evidence: [H:282-284](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L282-L284), [SZ:6-18](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZone4.cs#L6-L18).

**Risk / parity implication:** Newer audit Core exposes Safari, but the seven Safari substructure files and property are absent in release 26.10.18.

**Implementation acceptance task:** Do not compile or advertise Safari support without a separately tested Core upgrade.

### V02 — P0: Safari age flush occurs before edits are staged

Evidence: [DS:106-138](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L106-L138), [SS:18-21](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneAreaSet4.cs#L18-L21).

**Risk / parity implication:** Desktop flushes age arrays, then updates the cached arrays from the UI; no later flush is shown before CopyChangesFrom. Source-level lost-edit risk.

**Implementation acceptance task:** Reproduce with distinct 12+12 age sentinels and verify Save/reopen.

### V03 — P0: Safari switcher high-bit state normalized

Evidence: [SZ:37-47](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZone4.cs#L37-L47), [DS:95-98](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L95-L98), [DS:127-132](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L127-L132).

**Risk / parity implication:** Getter exposes high-bit states 0–3 but setter writes only 0 or 1; untouched states 2/3 can be overwritten.

**Implementation acceptance task:** Preserve raw flags and refuse unknown-state edits.

### V04 — P0: Safari auto-count sorts all object slots

Evidence: [SA:54-85](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneArea4.cs#L54-L85), [SA:95-100](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneArea4.cs#L95-L100), [DS:106-120](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L106-L120).

**Risk / parity implication:** CleanEmpty sorts 30 objects in place; autoCount serialization can reorder unchanged records.

**Implementation acceptance task:** Require explicit normalization and full object-slot byte diff.

### V05 — P0: Safari unknown object type can be coerced

Evidence: [DA:47-70](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SafariArea4Editor.cs#L47-L70), [DA:127-132](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SafariArea4Editor.cs#L127-L132), [SO:21-35](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneObject4.cs#L21-L35).

**Risk / parity implication:** Unknown raw enum type is displayed using a clamped label and parsed back to that known label on save.

**Implementation acceptance task:** Preserve raw enum and dirty state; test 0xFF and 0x7F.

### V06 — P0: Pokéathlon medal high bits lost on no-op save

Evidence: [PM:13-16](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonMedalManager4.cs#L13-L16), [PM:21-35](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonMedalManager4.cs#L21-L35), [DP:299-324](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L299-L324).

**Risk / parity implication:** Desktop rebuilds each of 493 bytes from only five modeled checkbox bits, dropping upper three.

**Implementation acceptance task:** Merge modeled bits with original high bits; test 0xE1.

### V07 — P0: Pokéathlon daily-shop high bits lost

Evidence: [P:77-82](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L77-L82), [DP:265-296](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L265-L296).

**Risk / parity implication:** 16-bit shop word is rebuilt from only 12 visible checkboxes.

**Implementation acceptance task:** Preserve four reserved bits during unrelated edits.

### V08 — P0: Pokéathlon Data Card high bits lost

Evidence: [P:68-75](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L68-L75), [DP:273-296](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L273-L296).

**Risk / parity implication:** 32-bit Data Card mask is rebuilt from 27 checkboxes; upper five bits disappear.

**Implementation acceptance task:** Preserve high bits and compare exact input/output bytes.

### V09 — P0: MAUI Pokéwalker list duplicates on repeated load

Evidence: [MW:26-42](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L26-L42), [MW:56-62](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L56-L62), [H:297-309](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L297-L309).

**Risk / parity implication:** ReadWalkerCourseUnlockFlags appends without clearing; Unlock All calls it again. SaveWalker writes every row into a fixed 32-element span.

**Implementation acceptance task:** Test repeated Unlock All then Save; replace append with indexed rebinding.

### V10 — P0: MAUI Pokéwalker clears reserved bits

Evidence: [MW:35-53](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L35-L53), [H:297-309](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L297-L309).

**Risk / parity implication:** A fresh 32-bit bool array is filled only for visible courses (normally 27), then all 32 bits are written.

**Implementation acceptance task:** Read original 32 flags and merge only edited course bits.

### V11 — P0: MAUI Unlock All mutates supplied save immediately

Evidence: [MW:8-12](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L8-L12), [MW:45-62](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L45-L62), [H:316-333](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L316-L333).

**Risk / parity implication:** Button calls Core mutator before separate SaveWalker; cancellation semantics of parent are not established.

**Implementation acceptance task:** Use detached draft and prove Cancel/Back/document-switch no mutation.

### V12 — P0: MAUI Pokégear bulk actions mutate supplied save

Evidence: [MG:9-13](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeGear.xaml.cs#L9-L13), [MG:28-49](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeGear.xaml.cs#L28-L49), [H:227-249](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L227-L249).

**Risk / parity implication:** Give All/Clear calls Core mutators directly on supplied SAV before Save; whether parent supplies a safe clone is unverified.

**Implementation acceptance task:** Verify parent ownership and enforce transaction boundary.

### V13 — P0: Core Apricorn index is unchecked

Evidence: [H:284-288](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L284-L288), [FA:46-52](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/ApricornService.cs#L46-L52).

**Risk / parity implication:** Direct Get/SetApricornCount adds arbitrary index to General offset, potentially addressing neighboring data; Forge single-item route validates 0–6.

**Implementation acceptance task:** Validate 0–6 before any Core access; test negative and large indices.

### V14 — P0: Core Pokégear caller index is unchecked

Evidence: [H:208-220](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L208-L220), [DG:13-23](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/PokeGear4Editor.cs#L13-L23).

**Risk / parity implication:** GetCallerAtIndex and SetCallerAtIndex index General without roster bounds; an in-buffer invalid index can affect unrelated bytes.

**Implementation acceptance task:** Add explicit caller-count bounds and sentinel tests.

### V15 — P0: Forge Unlock All replaces full Walker word

Evidence: [FW:84-91](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PokewalkerService.cs#L84-L91), [H:297-333](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L297-L333).

**Risk / parity implication:** Forge uses language-derived SetAll mask, replacing 32 bits including reserved high five; SetCourse instead reads existing flags.

**Implementation acceptance task:** Preserve bits 27–31 except on explicitly approved full reset.

### V16 — P1: Pokéathlon points setter normalizes unusual raw value

Evidence: [P:60-75](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L60-L75), [DP:265-280](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L265-L280).

**Risk / parity implication:** Core clamps 32-bit points to 99,999 and desktop always saves UI value, potentially altering untouched noncanonical data.

**Implementation acceptance task:** Write only dirty fields; test raw 0xFFFFFFFF.

### V17 — P1: Pokéathlon global counters clamp raw values

Evidence: [PG:11-41](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonGlobalCounters4.cs#L11-L41), [PG:45-55](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonGlobalCounters4.cs#L45-L55), [DP:176-211](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L176-L211), [DP:327-339](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L327-L339).

**Risk / parity implication:** Multiple 32-bit counters have capped setters and desktop rewrites them.

**Implementation acceptance task:** Preserve unchanged raw uint32 and test no-op parity.

### V18 — P1: Flag setters clamp numerically, not bitwise

Evidence: [P:68-82](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L68-L82).

**Risk / parity implication:** Math.Min on mask values is not a bitwise merge of modeled and reserved flags.

**Implementation acceptance task:** Keep raw and modeled masks separate; no implicit normalization.

### V19 — P1: Nested Pokéathlon structs expose live memory

Evidence: [P:13-37](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L13-L37), [PE:12-20](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonEventData4.cs#L12-L20), [PX:11-21](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonConnection4.cs#L11-L21).

**Risk / parity implication:** Course, medal, self-event and connection records wrap mutable backing memory.

**Implementation acceptance task:** Detach bounded immutable snapshots before Android presentation.

### V20 — P1: Course/event cardinality and offsets differ

Evidence: [P:15-58](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/Pokeathlon4.cs#L15-L58), [PC:8-22](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonCourseRecord4.cs#L8-L22), [PE:8-20](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonEventData4.cs#L8-L20), [PX:7-21](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonConnection4.cs#L7-L21).

**Risk / parity implication:** Five course records, ten self events, ten connection events and five trainers per connection have different layouts.

**Implementation acceptance task:** Test all cardinalities, offsets and invalid indices.

### V21 — P1: Participant identity has special packed semantics

Evidence: [PP:8-26](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonParticipant4.cs#L8-L26), [PR:24-29](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonEventRecord4.cs#L24-L29), [PS:3-12](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/SpeciesForm10.cs#L3-L12).

**Risk / parity implication:** Course participant fields and event species/form entries use different packed widths; not standard PKM structures.

**Implementation acceptance task:** Keep full raw word and test form/gender/shiny/reserved bits.

### V22 — P1: Medal table is 493 bytes and 1-based species

Evidence: [PM:11-25](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Pokeathlon/PokeathlonMedalManager4.cs#L11-L25), [DP:299-324](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L299-L324).

**Risk / parity implication:** Species 1–493 map to bytes 0–492; five low bits are modeled per species.

**Implementation acceptance task:** Reject species 0/494; test total masked medal count.

### V23 — P1: Safari has separate self/link area and age tables

Evidence: [SZ:8-18](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZone4.cs#L8-L18), [SS:9-21](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneAreaSet4.cs#L9-L21), [DS:9-16](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L9-L16).

**Risk / parity implication:** Two sets each contain six configurations and twelve age bytes.

**Implementation acceptance task:** Use stable self/link IDs and independent sentinel arrays.

### V24 — P1: Safari index access lacks semantic guards

Evidence: [SS:18-19](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneAreaSet4.cs#L18-L19), [SS:34-42](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneAreaSet4.cs#L34-L42), [ST:3-17](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneType4.cs#L3-L17).

**Risk / parity implication:** Age and configuration arrays are indexed directly; invalid enum/index values can throw or select unintended data.

**Implementation acceptance task:** Validate map 0–11 and config 0–5 before accessor.

### V25 — P1: Safari link leader has full-width metadata

Evidence: [SL:10-24](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneLinkLeader4.cs#L10-L24), [SZ:12-16](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZone4.cs#L12-L16).

**Risk / parity implication:** Two signed 64-bit fields, trainer ID, version/language and 16 name bytes must be preserved.

**Implementation acceptance task:** Detach numeric values and fingerprint raw name bytes.

### V26 — P1: Safari area file is not a full save

Evidence: [SA:21-23](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen4/Safari/SafariZoneArea4.cs#L21-L23), [DA:76-119](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SafariArea4Editor.cs#L76-L119).

**Risk / parity implication:** Desktop imports/exports exactly one 0x7A-byte area, not 0x5FC Safari block or a complete save.

**Implementation acceptance task:** Test exact size, ±1 byte and round-trip as an isolated area.

### V27 — P1: Pokéwalker uses 27 courses but 32 flags

Evidence: [FW:28-55](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PokewalkerService.cs#L28-L55), [H:297-315](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L297-L315), [MW:35-53](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L35-L53).

**Risk / parity implication:** Visible course count differs from stored bit count; localized name length is a separate dimension.

**Implementation acceptance task:** Keep 27 visible rows, 32 raw bits and no duplicate rows.

### V28 — P1: Pokéwalker language masks differ

Evidence: [FW:33-55](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PokewalkerService.cs#L33-L55), [FW:84-91](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PokewalkerService.cs#L84-L91), [H:312-330](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L312-L330).

**Risk / parity implication:** JP, KR and international course masks differ; Forge unlocks language-possible courses while Core All uses JP mask.

**Implementation acceptance task:** Test all language variants; do not infer legality from unlocked bit.

### V29 — P1: Forge encounter table and art require provenance

Evidence: [FW:37-62](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PokewalkerService.cs#L37-L62), [FC:43-71](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/PKForge.App.csproj#L43-L71).

**Risk / parity implication:** Forge loads encounter_walker4.pkl and bundles sprite/artwork assets; build inclusion is not proof of redistribution rights.

**Implementation acceptance task:** Use licensed data APIs; no asset import without rights review.

### V30 — P1: MAUI Pokégear enum binding may be a no-op

Evidence: [MG:13-28](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeGear.xaml.cs#L13-L28), [H:214-220](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV4HGSS.cs#L214-L220).

**Risk / parity implication:** ComboBox binds SelectedItem to '.' on enum array elements; no indexed setter is shown.

**Implementation acceptance task:** Use indexed editable models and test save/reopen of one changed caller.

### V31 — P1: Desktop clone/cancel does not certify SAF writes

Evidence: [DP:34-78](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Pokeathlon/SAV_Pokeathlon4.cs#L34-L78), [DS:22-55](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/Safari/SAV_Safari4.cs#L22-L55), [FT:65-76](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/WorldEventsMenu.cs#L65-L76).

**Risk / parity implication:** Desktop clone-and-copy and Forge mutation helper are design references, not Android provider recovery evidence.

**Implementation acceptance task:** Keep write gate closed pending staged backup/readback and interruption tests.

### V32 — P1: GPL code and artwork rights must be separated

Evidence: [KC:4-18](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/PKHeX.Core.csproj#L4-L18), [FC:39-71](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/PKForge.App.csproj#L39-L71), [MC:96-104](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/PKHeX.MAUI.csproj#L96-L104), [ML:1-6](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt#L1-L6).

**Risk / parity implication:** Core declares GPL-3.0-or-later and mobile forks carry GPLv3 text, but bundled artwork/fonts/assets need independent provenance.

**Implementation acceptance task:** Preserve code notices; keep text-only UI until asset rights are verified.

### V33 — P1: HGSS parity remains source-only

Evidence: [DM:692-735](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen4/SAV_Misc4.cs#L692-L735), [FG:22-54](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/HgssGadgetsEditor.cs#L22-L54), [MW:27-62](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen4/Misc/MiscPokeWalker.xaml.cs#L27-L62).

**Risk / parity implication:** Source inspection and green builds are not independent same-version desktop fixtures or Android device certification.

**Implementation acceptance task:** Keep HGSS edits/export disabled and execute companion gates.

## Architecture and licensing disposition

- **Engine:** retain the exact official Core 26.10.18 pin. The newer Safari types cannot be invoked without a separately approved and fixture-tested upgrade. HGSS Pokéathlon and Pokéwalker source presence does not establish certified read support.
- **Android boundary:** bounded, detached immutable raw projections; no retained Core spans or live save references. Preserve 32 Walker bits, 493 full medal bytes, all reserved flag bits, complete Safari object records, and signed 64-bit link-leader fields. Read-only status must not be used to authorize writes.
- **UI parity:** desktop provides Pokeathlon and Safari editors; Forge has Apricorn/Pokéwalker flows; MAUI has Pokégear/Pokéwalker pages. Existence of an editor is not functional parity. A text-first native Android interface avoids importing game artwork while source and licensing decisions remain provisional.
- **Rights:** [Core package licence](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/PKHeX.Core.csproj#L11-L18) states GPL-3.0-or-later; PKForge [LICENSE](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/LICENSE) and MAUI [LICENSE.txt](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt) contain GPLv3 licence text. These facts do not prove rights to third-party sprites, fonts, images, game data or event archives. Retain attribution and audit every asset separately.
- **Priority:** reproduce the age-flush and reserved-bit losses with synthetic sentinels; block unsupported Core APIs; verify clone/cancel and provider recovery; then implement read-only HGSS projections and independently match desktop fixtures. Unknown ROM-hack layouts remain read-only and cannot be exported as retail.

See [the 36 execution gates](PARITY_HGSS_GADGETS_GATES_2026-10-28.md). None are certified.
