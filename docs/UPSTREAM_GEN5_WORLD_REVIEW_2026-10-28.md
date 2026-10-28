# Gen5 world systems source audit — pass W (2026-10-28)

**Source-only, not runtime certified.** No desktop fixture match, Android device run, write/export authorization or hack-layout authentication is claimed. P0 is a blocker **before enabling writes**, not proof of a reachable Android defect.

## Exact pins and source drift

- PKHeX [`7e26bc53215e4f7bc0041ca375f1042539cb81b3`](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3).
- PKForge [`2df04430e532b78c247f45b936f3068d8f824987`](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987).
- PKHeX MAUI [`a6f7cfb78e1c96155baa67660fd7ae97895e375f`](https://github.com/santacrab2/PKHeXMAUI/tree/a6f7cfb78e1c96155baa67660fd7ae97895e375f).
- Mobile Core is exact NuGet `[26.10.18]`, release source [`bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`](https://github.com/kwsch/PKHeX/tree/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea). **20/20** inspected Core Gen5 files had identical Git blob SHA in the newer audit tree and this release (0 changed, 0 absent); not a package binary reproducibility proof.

**Evidence accounting:** 35 distinct findings (19 P0, 16 P1); 22 distinct source paths cited in the findings; 42 proposed, unexecuted scenarios in [matching parity gates](PARITY_GEN5_WORLD_GATES_2026-10-28.md). Findings span Core, desktop WinForms, PKForge and MAUI; no code or assets copied.

## Findings and implementer tickets

### W01 — P0: Entree species setter erases raw bit 10

Evidence: [C:14–17](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Entree/EntreeSlot.cs#L14-L17).

**Risk / acceptance:** Getter masks 10 bits but setter clears 11. Prove no-op preservation of a bit-10 sentinel before any slot edit.

### W02 — P0: Forest Slots decrypts mutable backing storage

Evidence: [C:25–49](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Entree/EntreeForest.cs#L25-L49).

**Risk / acceptance:** Inspect a detached copy; verify input and seed unchanged after success, exception and cancellation.

### W03 — P0: Forge Forest mutation lacks finally

Evidence: [F:98–143](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/EntralinkService.cs#L98-L143).

**Risk / acceptance:** Unlock/fill/clear call StartAccess then EndAccess without finally; inject faults and require exact encrypted rollback.

### W04 — P0: Forge fill mutates 530 slots before completion

Evidence: [F:108–133](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/EntralinkService.cs#L108-L133).

**Risk / acceptance:** Use deterministic staged edits, validation and atomic publication rather than partial randomized mutation.

### W05 — P0: MAUI Forest move uses SelectedIndex as move ID

Evidence: [M:139–150](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscForest.xaml.cs#L139-L150).

**Risk / acceptance:** The read uses ComboItem.Value; sparse/filtered move lists must round-trip the actual move ID.

### W06 — P0: MAUI Forest assumes species/moves exist

Evidence: [M:60–99](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscForest.xaml.cs#L60-L99).

**Risk / acceptance:** First() and personal-table access can fail on unknown/custom IDs. Display raw values read-only.

### W07 — P1: MAUI Forest random fill assumes nonempty pool

Evidence: [M:113–129](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscForest.xaml.cs#L113-L129).

**Risk / acceptance:** Source.Remove decreases the pool without an exhaustion check; test empty and short lists with no partial writes.

### W08 — P1: MAUI Forest selection assumes nonempty area

Evidence: [M:46–62](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscForest.xaml.cs#L46-L62).

**Risk / acceptance:** Index zero and currentIndex are dereferenced without an empty-selection guard.

### W09 — P0: Medal State setter erases reserved bits 4–7

Evidence: [C:48–57](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Medal5.cs#L48-L57).

**Risk / acceptance:** State setter retains bit 3 only. Prove upper nibble survives a state change and a no-op.

### W10 — P0: MAUI medal navigation may write during load

Evidence: [M:25–80](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscMedals.xaml.cs#L25-L80).

**Risk / acceptance:** SelectedIndex/IsChecked assignments coexist with live write handlers without a shown load guard; prove navigation byte-identical.

### W11 — P1: Malformed medal dates can throw

Evidence: [C:72–87](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Medal5.cs#L72-L87).

**Risk / acceptance:** Raw month/day are passed directly to DateOnly. Represent invalid dates without normalization or exceptions.

### W12 — P0: Desktop medal import validates only size

Evidence: [D:72–89](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/SAV_Medals5.cs#L72-L89).

**Risk / acceptance:** Length-matched arbitrary bytes enter draft Medal storage. Validate contents and cancellation before import.

### W13 — P0: Unity Tower country zero indexes prior region

Evidence: [C:18–29](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/UnityTower5.cs#L18-L29).

**Risk / acceptance:** country=0 computes GeonetOffset minus 16. Reject zero; assert adjacent sentinels unchanged.

### W14 — P0: Unity Tower subregion can cross country stride

Evidence: [C:18–29](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/UnityTower5.cs#L18-L29).

**Risk / acceptance:** subregion >=64 indexes into a neighboring country. Validate per-country bounds.

### W15 — P0: Desktop Unity Tower clears before row validation

Evidence: [D:91–115](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/SAV_UnityTower.cs#L91-L115).

**Risk / acceptance:** ClearAll runs before parsing rows. Stage and validate every row before any mutation.

### W16 — P0: MAUI Forest City import writes directly

Evidence: [M:30–48](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscCityForest.xaml.cs#L30-L48).

**Risk / acceptance:** Length-only validation followed by SetData lacks independent format checks and rollback; SAF path semantics unverified.

### W17 — P1: MAUI Forest City export concatenates path strings

Evidence: [M:17–28](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscCityForest.xaml.cs#L17-L28).

**Risk / acceptance:** Folder + filename is not Android document-provider creation; require URI-based export/readback.

### W18 — P0: Musical NARC trusts signed raw length

Evidence: [C:6–21](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/MusicalShow5.cs#L6-L21).

**Risk / acceptance:** Negative or oversized Length feeds Raw.Slice. Reject malformed payloads before inspection.

### W19 — P0: Musical prop access lacks index bounds

Evidence: [C:25–42](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Musical5.cs#L25-L42).

**Risk / acceptance:** Bit offset derives from arbitrary prop. Reject negative/out-of-range and preserve adjacent data.

### W20 — P0: MAUI Musical read-to-write checkbox binding

Evidence: [M:15–31](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscMusical.xaml.cs#L15-L31).

**Risk / acceptance:** Selection sets IsChecked, whose handler writes live SAV; require load suppression and byte-identical navigation.

### W21 — P0: Desktop Global Link TryParse defaults to zero

Evidence: [D:87–123](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/SAV_GlobalLink5.cs#L87-L123).

**Risk / acceptance:** Invalid item or quantity text silently writes zero; validate all rows before committing any.

### W22 — P1: Global Link boolean setters normalize unknown bytes

Evidence: [C:49–67](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/GlobalLink5.cs#L49-L67).

**Risk / acceptance:** No-op on stored nonzero values must not rewrite them as 1.

### W23 — P1: Join Avenue entity getters alias mutable memory

Evidence: [C:19–45](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Join%20Avenue/JoinAvenue5.cs#L19-L45).

**Risk / acceptance:** Visitor/fan/occupant/assistant views require bounded detached projection.

### W24 — P1: Join Avenue physical slots differ from count fields

Evidence: [C:8–30](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Join%20Avenue/JoinAvenue5.cs#L8-L30).

**Risk / acceptance:** Capacities 8/12/8/4 are independent of stored CountVisitor/CountFan; never trust counts as offsets.

### W25 — P1: Join Avenue date setters mask invalid values

Evidence: [C:5–31](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/Join%20Avenue/JoinAvenueDate5.cs#L5-L31).

**Risk / acceptance:** Preserve invalid raw dates on read and reject invalid writes rather than wrap.

### W26 — P1: Desktop Join Avenue stages multiple editors

Evidence: [D:60–72](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/Join%20Avenue/SAV_JoinAvenue.cs#L60-L72).

**Risk / acceptance:** SaveAll operates on clone before CopyChangesFrom; still requires independent validation and exception rollback.

### W27 — P1: Key System stores XOR magic, not plain flags

Evidence: [C:46–77](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/KeySystem5.cs#L46-L77).

**Risk / acceptance:** Decode with Crypto and preserve raw unknown values; do not serialize all flags as booleans.

### W28 — P0: Funfest unlock spans event-work blocks

Evidence: [C:73–108](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/FestaBlock5.cs#L73-L108).

**Risk / acceptance:** EventWork flags are modified beyond Festa. Require multi-block diff allowlists and rollback.

### W29 — P1: MAUI Pass Power writes selected index

Evidence: [M:107–131](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscEntree.xaml.cs#L107-L131).

**Risk / acceptance:** Prove index equals enum value for every choice or persist ComboItem.Value.

### W30 — P1: Desktop DLC contains empty load stubs

Evidence: [D:99–105](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/SAV_DLC5.cs#L99-L105).

**Risk / acceptance:** LoadMusical and LoadMemoryLink are empty; tab presence is not parity evidence.

### W31 — P1: Desktop DLC alternate-size import zero-pads

Evidence: [D:144–168](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen5/SAV_DLC5.cs#L144-L168).

**Risk / acceptance:** Array.Resize can pad accepted alternate payloads; require explicit reversible envelope handling.

### W32 — P1: MAUI medal labels are hardcoded English

Evidence: [M:7–22](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscMedals.xaml.cs#L7-L22).

**Risk / acceptance:** Use localized IDs/labels without changing stored medal indices.

### W33 — P1: Forge GetState provides a cleanup pattern

Evidence: [F:59–87](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/EntralinkService.cs#L59-L87).

**Risk / acceptance:** Read path correctly uses finally around Forest decryption; reuse discipline with detached data.

### W34 — P0: Unity Tower save-country lacks region validation

Evidence: [C:101–105](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen5/UnityTower5.cs#L101-L105).

**Risk / acceptance:** Saved country/region are cast to bytes and sent to unbounded setter; reject invalid fields.

### W35 — P1: MAUI Forest City import assumes local filesystem path

Evidence: [M:35–47](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/Gen5/Misc/MiscCityForest.xaml.cs#L35-L47).

**Risk / acceptance:** FileInfo/File.ReadAllBytes on picker FileName is not proven for Android SAF; use provider streams.

## Desktop/mobile parity and adoption decisions

| System | Source-level desktop coverage | Fork-side implementation | Required mobile approach |
| --- | --- | --- | --- |
| Entree Forest and Funfest | Core `EntreeForest`/`FestaBlock5`, WinForms `SAV_Misc5` | PKForge `EntralinkService` offers a reusable state projection and safer read cleanup; MAUI `MiscForest` directly edits live slots | Detach and bound all data; reproduce bit/crypto hazards; no mutation before transactional proof |
| Join Avenue | WinForms `SAV_JoinAvenue` handles visitor, fan, occupant, assistant collections | No certified equivalent established by inspected mobile pages | Read-only 8/12/8/4 collection with invalid-date and unknown-byte preservation |
| Medals | WinForms `SAV_Medals5`, Core `MedalList5` | MAUI `MiscMedals` writes directly from handlers | Never port event handlers without load suppression and reserved-bit regression |
| Unity Tower | WinForms `SAV_UnityTower` | No validated mobile equivalent in this pass | Bounds-check locale country/region, avoid clear-and-rewrite |
| Musical/DLC/Global Link | WinForms `SAV_DLC5`, `SAV_GlobalLink5` | MAUI `MiscMusical` is partial, not full DLC parity | Explicit missing-feature labels, bounded raw payloads, staged import/export |

**Licensing:** [PKHeX.Core project](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/PKHeX.Core.csproj), [PKForge GPLv3](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/LICENSE), [MAUI GPLv3](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt). PKForge [engine project](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PKForge.Engine.csproj) references its [forked PKHeX submodule](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/.gitmodules); MAUI uses a separately versioned Core package. GPL source can be adapted with attribution/obligations; artwork, fonts and data assets require **separate** rights checks. No ROMs, saves, game art, credentials or private data belong in this repo.

## Priorities

1. **P0:** Reproduce Entree bit-10, Medal reserved-bit, Geonet out-of-bounds and Musical length/index cases with synthetic adjacent-byte sentinels on pinned Core.
2. **P0:** Implement bounded immutable Gen5 read-only projections, with Forest encryption cleanup and no-op SHA-256 assertions.
3. **P0:** Design staged, allowlisted, rollback-capable multi-block transactions for Forest/Funfest/Geonet; keep writes disabled until same-version desktop and Android provider gates pass.
4. **P1:** Adapt only proven PKForge presentation/state code, not its forked Core or unverified UI bindings; verify each reused source path and GPL notice.
5. **P1:** Compare independently generated PKHeX desktop 26.10.18 fixtures; distinguish source review from tested parity.

Milestone 0 remains open; no claimed game format is write-certified.
