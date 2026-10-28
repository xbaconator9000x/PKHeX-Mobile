# Generation 6 Super Training, Secret Bases, O-Powers and mobile reuse — source audit pass X (2026-10-28)

**Status: source-only audit; NOT a certified Android feature or executable parity result.** 33 unique findings (13 P0 pre-write blockers), 50 finding-level immutable source references across 21 distinct pinned source paths; 36 proposed acceptance scenarios in [the companion gate matrix](PARITY_GEN6_SYSTEMS_GATES_2026-10-28.md). No test in this pass was executed against Android or independent save fixtures.

## Immutable source identity and release correspondence

- Official PKHeX audited SHA: `7e26bc53215e4f7bc0041ca375f1042539cb81b3` (desktop WinForms and Core).
- Actual mobile NuGet package: official PKHeX.Core **26.10.18**, corresponding source-release SHA `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`. Package-to-source binary reproducibility is **not** proven.
- PKForge SHA: `2df04430e532b78c247f45b936f3068d8f824987`; uses a forked PKHeX Core project reference, not the official mobile NuGet pin.
- PKHeX MAUI SHA: `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; source project has a different NuGet Core version. These are distinct dependencies, not interchangeable binaries.
- Compared the Git tree blob SHA of each of the following **12 selected official Core source paths** at the audited SHA and release SHA: **12/12 identical**. This proves source-file identity for these paths only, not the entire package.

| Core source path (relative to PKHeX) | Git blob SHA at both commits |
|---|---|
| [`PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs) | `4dbd38f6b0e9c100d2c16ef7c6a4ab1bbfa8a41e` |
| [`PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs) | `836f8b4affb7d37687c042795bd1f4e012a3fb38` |
| [`PKHeX.Core/Saves/Substructures/Gen6/SecretBase6Block.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase6Block.cs) | `0a0ece2546957ba1305f6ffca0dcb72ed2b036dc` |
| [`PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6.cs) | `6b83b49c75e0731695107692b4f4b7cc2aff1701` |
| [`PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6Value.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6Value.cs) | `f2c937d5ae07290196334e3c5c4e64f85fb2f4` |
| [`PKHeX.Core/Saves/Substructures/Gen6/Contest6.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/Contest6.cs) | `a93e74013657a1bef506f1187135a85a40fba2f3` |
| [`PKHeX.Core/Saves/Substructures/Gen6/Situation6.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/Situation6.cs) | `8708d9102f0b26f2ca7d9429f4642f0a513936a7` |
| [`PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6PKM.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6PKM.cs) | `7cf80660c998dac7eaeff1e956cd7884853d2e73` |
| [`PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6GoodPlacement.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6GoodPlacement.cs) | `b1be3f93c1aaacba9be1c174939410725fb5e74f` |
| [`PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6GoodStock.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6GoodStock.cs) | `e566d1cefc93e3924e225055a71ee8a8d0905318` |
| [`PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6Type.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6Type.cs) | `558c77c2a80c8eda1bbc5a7019286a86a6a84095` |
| [`PKHeX.Core/Saves/Substructures/Gen6/OPower/OPowerFlagState.cs`](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPowerFlagState.cs) | `a793b4f2e240b4ef26b0c53632a6f92872adc621` |

## Findings — implementer tickets and reproducible conditions

Priority P0 means **block any relevant write path** until the proposed acceptance case is executed. P1 means required parity/robustness evidence, not a claim of observed Android corruption. A source-level hazard is not an independently reproduced failure.

### G6-01 · P0 · Super Training bag allocation

`GetOpenBagIndex` returns the first **occupied** slot (`!= 0`) instead of the first empty slot; returns `-1` on a wholly empty bag list. Evidence: [C:st L199-225](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L199-L225). **Implementer action:** Never use `AddBag` for writes without an independently verified corrected allocator; add empty/partial/full sentinels.

### G6-02 · P0 · Super Training bag overwrite

`AddBag` uses the inverted allocator and calls `SetBag` at that index, so a nonempty slot may be overwritten; no shift or backup occurs in the method. Evidence: [C:st L180-197](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L180-L197); [C:st L199-225](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L199-L225). **Implementer action:** Reproduce existing-slot preservation with detached synthetic 12-byte bag arrays; block this helper in write adapters.

### G6-03 · P0 · Desktop Super Training save

WinForms compacts selected bags into lower indices but never explicitly clears the old tail before copying the cloned save to origin; removed bag IDs can remain. Evidence: [D:ds L118-135](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SuperTrain.cs#L118-L135). **Implementer action:** Use a full 12-slot candidate initialized to zero and compare exact tail bytes before any edit gate.

### G6-04 · P1 · Training record time validity

Core accepts arbitrary `float` values for 48+48 times, including nonfinite values; desktop parses text with `float.TryParse` and no finite/range check. Evidence: [C:st L88-135](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L88-L135); [D:ds L184-200](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SuperTrain.cs#L184-L200). **Implementer action:** Preserve raw IEEE-754 bits in read-only view; reject NaN, infinities and invalid ranges in any future editor.

### G6-05 · P1 · Training holder aliasing

`GetHolder1/2` return `SuperTrainingSpeciesRecord` over `Raw.Slice`, not detached copies; setting fields mutates the backing save block immediately. Evidence: [C:st L138-173](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L138-L173). **Implementer action:** Snapshot holder bytes before projection; mutation must occur only in isolated candidate buffers.

### G6-06 · P1 · Training capacity distinctions

Core defines 48 record positions but only 32 normally released stages and six distribution flags; desktop lists only 32 stages. Evidence: [C:st L11-29](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L11-L29); [C:st L276-290](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SuperTrainBlock.cs#L276-L290); [D:ds L23-28](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SuperTrain.cs#L23-L28). **Implementer action:** Expose the full raw 48 record slots separately from 32 named/released stages; no implicit truncation.

### G6-07 · P1 · Per-Pokémon vs save training

Desktop has distinct save-level `SAV_SuperTrain` and PKM-level `SuperTrainingEditor` using `ISuperTrainRegimen`; they are not interchangeable parity items. Evidence: [D:ds L8-23](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SuperTrain.cs#L8-L23); [D:dp L8-15](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/PKM%20Editors/SuperTrainingEditor.cs#L8-L15); [D:dp L130-156](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/PKM%20Editors/SuperTrainingEditor.cs#L130-L156). **Implementer action:** Track two independent feature rows, fixtures and UI paths.

### G6-08 · P0 · Secret Base flag width

`SecretBase6.IsNew` reads byte 0 but writes a 16-bit value at offset 0, potentially clearing byte 1 when toggled. Evidence: [C:sb L39-43](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L39-L43). **Implementer action:** Preserve adjacent byte 1 with sentinels; do not expose the setter until field semantics are independently proven.

### G6-09 · P1 · Secret Base location normalization

`BaseLocation` setter remaps 1/2 to 0 and clamps out-of-range values; a display round-trip must not invoke this setter on untouched saves. Evidence: [C:sb L45-61](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L45-L61). **Implementer action:** Project signed raw location; test untouched 1, 2, -1 and 85/86 without normalization.

### G6-10 · P1 · Secret Base string preservation

Trainer and six message setters use `StringConverterOption.ClearZero`, so reserializing unchanged displayed text can erase original trailing/trash bytes. Evidence: [C:sb L78-100](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L78-L100). **Implementer action:** Expose escaped text separately from raw byte fingerprints; no read-to-write string normalization.

### G6-11 · P0 · Secret Base participant alias bit

`SecretBase6PKM.IsEgg` and `IsShiny` read and write the **same bit 5** at byte `0x2D`; independent controls would alias. Evidence: [C:sp L45-54](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6PKM.cs#L45-L54); [D:db L207-215](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SecretBase.cs#L207-L215). **Implementer action:** Keep both as a single raw-bit observation until game semantics are established; verify with desktop and game-native fixtures.

### G6-12 · P0 · Secret Base IV overflow

`IV_SPD` setter ORs the supplied `value` without masking to five bits; values above 31 can alter upper flags in the same byte. Evidence: [C:sp L45-54](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6PKM.cs#L45-L54). **Implementer action:** Test 31, 32, 63, negative and neighboring flags; future editor must clamp before mutation.

### G6-13 · P0 · Secret Base gender bit overflow

`Gender` setter shifts the entire supplied byte and ORs it without masking to the two-bit field, potentially altering form bits. Evidence: [C:sp L23-27](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6PKM.cs#L23-L27). **Implementer action:** Validate `0..3` and preserve upper form bits with all-value sentinels.

### G6-14 · P1 · Secret Base import subtype semantics

`Read` accepts only `0x310` or `0x3E0`; loading a self-size base into another base copies only the prefix, while other-to-other copies the full payload. Evidence: [C:sb L128-146](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L128-L146); [C:sb L215-225](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L215-L225); [D:db L385-408](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_SecretBase.cs#L385-L408). **Implementer action:** Require explicit source/destination subtype and preservation of the other-base tail on self-size import.

### G6-15 · P1 · Secret Base participant copy boundary

`GetParticipant` returns a detached 0x34-byte array but `SetParticipant` copies it into live save data; `SetTeam` iterates writes with no local transaction. Evidence: [C:sb L189-222](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6.cs#L189-L222). **Implementer action:** Retain detached editing, but validate all three members before a single candidate commit; rollback on the second-member failure.

### G6-16 · P1 · Secret Base deletion shift

`DeleteOther` compacts 30 entries and clears the final 0x3E0-byte slot; one deletion changes many offsets and requires whole-region scope verification. Evidence: [C:sbb L44-81](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase6Block.cs#L44-L81). **Implementer action:** Use first/middle/last deletion fixtures with full region fingerprints and unaffected-prefix sentinels.

### G6-17 · P1 · Secret Base decoration bulk writes

`GiveAllGoods` overwrites 173 four-byte entries with count 25/new flag; 200 entries exist, and 27 are not in the used subset. Evidence: [C:sbb L19-36](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase6Block.cs#L19-L36); [C:good L10-20](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/SecretBase/SecretBase6GoodStock.cs#L10-L20). **Implementer action:** Prove preservation of the 27 trailing entries and reserved stock bytes before implementing bulk grant.

### G6-18 · P1 · O-Power nonbinary flag preservation

Core reads raw O-Power flag bytes as an enum; desktop checks only equality to `Unlocked` and rewrites every flag as locked/unlocked on Save, normalizing unknown values. Evidence: [C:op L26-44](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6.cs#L26-L44); [D:do L59-90](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_OPower.cs#L59-L90). **Implementer action:** Compare all 65 raw flag bytes before/after no-op Save; preserve unknown bytes unless explicitly edited.

### G6-19 · P1 · O-Power bulk asymmetry

`UnlockAll` fills all 65 flags and both level tables with 3, while `ClearAll` clears from byte 1 onward, preserving byte 0. Evidence: [C:op L46-56](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6.cs#L46-L56). **Implementer action:** Test enable flag and level/points separately; never treat Clear and Unlock as symmetric inverses.

### G6-20 · P1 · O-Power typed index validation

Core casts enum indices directly to span offsets and accepts arbitrary level bytes; invalid enum values can throw, while out-of-range values need domain checks. Evidence: [C:op L32-44](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6.cs#L32-L44); [C:optype L3-11](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/OPower/OPower6Type.cs#L3-L11). **Implementer action:** Guard enums, lengths and level domain before calling Core; distinguish safe exception from validated semantics.

### G6-21 · P1 · Contest item cap normalization

ORAS contest block reads a raw uint count but setter caps values above 999, so a read/unchanged-write would normalize unusual values. Evidence: [C:contest L6-25](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/Contest6.cs#L6-L25). **Implementer action:** Keep uint raw projection; test `0,999,1000,0xFFFFFFFF` and reserved trailing bytes.

### G6-22 · P1 · Situation mirrored offsets

`Situation6` writes map, rotation and X/Y/Z to both primary and mirrored offsets; it accepts cast integers/floats without a local full-block transaction. Evidence: [C:situation L8-60](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Substructures/Gen6/Situation6.cs#L8-L60). **Implementer action:** Read both mirror copies; test mismatch handling and five-field atomic rollback with adjacent sentinels.

### G6-23 · P0 · MAUI global save ownership

Gen6 tab and pages capture `MainPage.sav` and store editor pages in static fields; switching documents can leave stale references. Evidence: [M:mtab L6-21](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerTab6.xaml.cs#L6-L21); [M:mmaison L6-10](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6Maison.xaml.cs#L6-L10); [M:mtrainer L7-12](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6.xaml.cs#L7-L12). **Implementer action:** Bind immutable per-document identity and version; dispose old pages and refuse save after document switch.

### G6-24 · P0 · MAUI multi-page partial save

`SavePage6.OnNavigatedTo` sequentially invokes five save methods on shared live `SAV`, then calls `PopModalAsync` without awaited rollback boundary. Evidence: [M:mtab L37-51](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerTab6.xaml.cs#L37-L51). **Implementer action:** Build all page edits into one validated candidate; inject failure at every step and prove original unchanged.

### G6-25 · P0 · MAUI Maison parsing

`SaveMaison` loops over 20 records using `ushort.Parse`; a bad later field throws after earlier fields have already been written. Evidence: [M:mmaison L10-29](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6Maison.xaml.cs#L10-L29). **Implementer action:** Parse/validate all 20 before mutation; test malformed last field and reserved-stat preservation.

### G6-26 · P0 · MAUI coordinate precision

Gen6 BadgeMap loads coordinates divided by 18, but saves `(int)Number * 18`; fractional values are truncated before multiplication and no-op save can change location. Evidence: [M:mbadge L17-39](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6BadgeMap.xaml.cs#L17-L39). **Implementer action:** Round-trip nonmultiples of 18 and negative/fractional coordinates; preserve float bits when unchanged.

### G6-27 · P0 · MAUI appearance live two-way binding

Appearance page binds writable Fashion properties directly to the save-backed object using `BindingMode.TwoWay`; Cancel can leave prior mutations in memory. Evidence: [M:mappear L12-17](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6Appearance.xaml.cs#L12-L17); [M:mappear L25-45](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6Appearance.xaml.cs#L25-L45). **Implementer action:** Bind to a detached DTO, not the save; verify Cancel and document-switch preserve every fashion byte.

### G6-28 · P0 · MAUI appearance bulk mutation

`GiveAllAccessories` calls `xy.Blocks.Fashion.UnlockAllAccessories()` directly from a click handler rather than a staged transaction. Evidence: [M:mappear L96-100](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6Appearance.xaml.cs#L96-L100). **Implementer action:** Stage bulk unlock on a clone; cancel/exception and unrelated-byte tests before allowing edits.

### G6-29 · P1 · MAUI trainer record coupling

Saving the PM field writes both record 63 and 64, even when only one visible field is edited; the equivalence must be validated for each game. Evidence: [M:mtrainer L117-124](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/SaveEditors/TrainerEditors/gen6/TrainerEditor6.xaml.cs#L117-L124). **Implementer action:** Compare both records against desktop 26.10.18 and use explicit dual-write policy only if proven.

### G6-30 · P1 · PKForge Gen6 world feature gap

The inspected `WorldEventsMenu.Options` lists clocks, roamers, resets, Entralink, HGSS gadgets, honey trees and key items, but no named Gen6 Super Training/Secret Base/O-Power route. This is a **menu-level gap only**, not proof the whole fork lacks a feature. Evidence: [F:fworld L52-64](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/WorldEventsMenu.cs#L52-L64). **Implementer action:** Trace all dispatch routes before marking absent; design separate XY/ORAS capability flags.

### G6-31 · P1 · PKForge reusable cosmetics UI

PKForge gates cosmetic groups by entity capability and routes through `ISaveEngineSession` edits; useful reusable pattern, but it is Pokémon cosmetics, not ORAS save-level contests. Evidence: [F:fcos L5-45](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/CosmeticsEditor.cs#L5-L45); [F:fcos L80-106](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/CosmeticsEditor.cs#L80-L106). **Implementer action:** Consider adapting capability-gated touch controls with original Core adapter and tested transactions.

### G6-32 · P1 · PKForge mutation boundary

`WorldEventsMenu.WriteAsync` funnels edits through `RunMutationAsync`, catching selected exceptions; this is a design reference, not proof of atomic provider publication. Evidence: [F:fworld L66-81](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/WorldEventsMenu.cs#L66-L81). **Implementer action:** Audit implementation of `RunMutationAsync` and SAF publication under injected failure before importing.

### G6-33 · P1 · Asset and UI licensing boundary

PKForge editor panel renders Skia surfaces and sprite artwork; GPLv3 source reuse does not automatically authorize copying separately sourced art, icons or fonts. Evidence: [F:fpanel L5-17](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/BoxBrowserPage.EditorPanel.cs#L5-L17); [F:fpanel L65-83](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Views/BoxBrowserPage.EditorPanel.cs#L65-L83). **Implementer action:** Reuse code only with provenance and GPL notices; independently clear assets or replace with original text/vector UI.

## Source-grounded desktop/mobile crosswalk

| Scope | Desktop PKHeX | PKForge pinned paths | PKHeX MAUI pinned paths | Disposition |
|---|---|---|---|---|
| Save-level Super Training | `SAV_SuperTrain`, 48 records / 12 bags | No matching named route in inspected WorldEvents menu | No matching named page in inspected Gen6 TrainerTab | Implement safe read-only raw projection; do not infer absence globally |
| Per-Pokémon Super Training | `SuperTrainingEditor`, PKM regimen flags | Capability-gated Pokémon editing patterns | Generic PKM tab paths require separate field audit | Separate from save-level editor |
| ORAS Secret Bases | `SAV_SecretBase`, 30 other bases, 28 placements | No named entry in inspected WorldEvents menu | No named page in inspected Gen6 TrainerTab | Candidate high-value new editor, write-gated |
| XY/ORAS O-Powers | `SAV_OPower`, 65 flags and level tables | WorldEvents options do not list this editor | Gen6 TrainerTab does not list this editor | Preserve nonbinary raw flags and all offsets |
| Gen6 trainer/records/Maison | Desktop trainer save editors | Generic read-only trainer record view exists | Gen6 Trainer, BadgeMap, Maison, Appearance, Multiplayer pages | MAUI offers reusable page layout but live-state/partial-write defects prohibit direct copying |
| Android UX | WinForms controls not touch-native | Capability-gated menus and `RunMutationAsync` integration | MAUI tabbed pages and two-way bindings | Favor detached DTOs and versioned document session; audit all imported source/assets |

## Architecture, licences, and next actions

**Reuse recommendation:** keep official pinned PKHeX.Core as format engine; borrow/adapt PKForge's capability-gated touch-menu concepts and MAUI's Gen6 page coverage only behind immutable read models and candidate transactions. Do **not** import the global `MainPage.sav`/static page ownership or directly bind UI to live Core objects. The existing native Android shell and the MAUI UI code cannot be combined by copying XAML without an explicit UI migration/build decision. GPLv3 source adaptation requires preserved copyright notices, corresponding source and modification attribution; separately licensed visual assets must be individually cleared. No assets or upstream code were copied by this audit.

**Prioritized handoff:** (1) fail-closed tests for bag allocator and compaction, Secret Base participant packed fields, and MAUI partial writes; (2) read-only Gen6 projection with detached raw fingerprints and XY/ORAS capability gates; (3) independent same-version desktop fixtures and negative hack-layout controls; (4) per-editor write transactions with SAF backup/rollback tests; (5) mobile adaptation decision and asset provenance manifest. Do not enable export, edit, or ROM-hack write profiles from these source findings alone.

**Evidence limits:** Selected files were read at pinned SHAs, and Core Git blob identity was compared. No Core package reproducibility build, desktop UI run, Android device run, independent game save, or acceptance scenario execution occurred in this audit. No code or test files were modified.
