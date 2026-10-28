# Party, box storage, slot-pointer and bulk-mutation audit — pass N (2026-10-28)

**Source-inspected, not runtime-certified.** 37 individually pinned findings (13 P0, 24 P1), 42 proposed, unexecuted acceptance gates. No save editing, desktop parity or custom-layout export is authorized. Audit docs only; implementation and tests belong to the separate watchdog.

## Immutable provenance and verification

- Official [PKHeX 7e26bc53215e4f7bc0041ca375f1042539cb81b3](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3); mobile package PKHeX.Core 26.10.18 source-release [bfe89c74a3f5265805e364b79bc04ad38ebbc5ea](https://github.com/kwsch/PKHeX/tree/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea).
- [PKForge 2df04430e532b78c247f45b936f3068d8f824987](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987) uses a forked Core; [PKHeX MAUI a6f7cfb78e1c96155baa67660fd7ae97895e375f](https://github.com/santacrab2/PKHeXMAUI/tree/a6f7cfb78e1c96155baa67660fd7ae97895e375f) uses an older Core package. Compare behavior, not README claims.
- Local implementation baseline [d1004d83b788fed982adf8d71b132a9dc11be6af](https://github.com/xbaconator9000x/PKHeX-Mobile/commit/d1004d83b788fed982adf8d71b132a9dc11be6af), with read-only occupied slot projection.
- **Seven official source files** were fetched at both official revisions. All seven have identical Git blob SHAs (source equality, **not** compiled DLL reproducibility):

| File | Identical blob SHA |
| --- | --- |
| [PKHeX.Core/Saves/Storage/SlotPointerUtil.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs) | `bec6274b9869d10998f990d95bf6afc4a92f43b2` |
| [PKHeX.Core/Saves/SaveFile.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs) | `285526fc0ded36187fc7968e25f595321cab74bd` |
| [PKHeX.Core/Saves/Storage/StorageSlotSource.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/StorageSlotSource.cs) | `a3be9b231cadaff6569b774da5558dedf8d0c719` |
| [PKHeX.Core/Saves/Util/BoxUtil.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/BoxUtil.cs) | `77640f314feac410c266577aab7378653cd95a67` |
| [PKHeX.Core/Saves/SAV8BS.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV8BS.cs) | `b73adce551eccc832d1aaef9ff312bf2b353b401` |
| [PKHeX.Core/Saves/Util/StorageUtil.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/StorageUtil.cs) | `cad784d9d3358f3d5c2a876e87f94567b35a819d` |
| [PKHeX.Core/Saves/Storage/EntityImportSettings.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/EntityImportSettings.cs) | `342c211dc5775c0d851948bf4ec55f60bba9303a` |

Finding references cover 16 distinct pinned source paths. They are not a complete editor inventory. The same-source check applies to the official source release; no independent binary build, game-save fixture or physical-device test was executed.

## High-severity deterministic source traces

**Pointer inversion:** `WithinRange(slot,min,max)` is defined at [lines 12–14](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs#L12-L14), but `UpdateSwap` calls it with `(boxStart,boxEnd,pointer)` at [29–32](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs#L25-L33) and `UpdateMove` repeats that order at [86–89](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs#L82-L90). For positive slots-per-box, `boxEnd <= boxStart` is false; these inspected branches cannot remap ordinary pointer values. This is an algebraic source trace, not a runtime save reproduction.

**Non-conserving MoveBox:** In [MoveBox](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L587-L630), for two distinct boxes A,B and `MoveBox(0,1)`, `i` visits only zero and copies A to destination box 1, while box 0 stays A. The resulting A,A loses B in this direct trace. The desktop box layout UI instead calls [SwapBox](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_BoxLayout.cs#L299-L319), so it does not validate MoveBox. Both APIs remain unsafe for this port until independently reproduced and repaired.

**Protected-slot boundary:** [SetBoxData](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L438-L451) explicitly skips protected slots, but [direct SetBoxSlotAtIndex](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L553-L583) does not check protection. The same omission appears in candidate mobile paths. Successful parsing, an empty target, a user risk acknowledgement or a passing Android compilation is not permission to write.

## Source-backed findings and implementer tickets

### N01 — P0: Swap pointer range inversion

WithinRange(slot,min,max) is called as WithinRange(boxStart,boxEnd,pointer), so its first comparison fails for ordinary positive box sizes. The inspected UpdateSwap cannot re-point team references. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs#L12-L35). **Implementer ticket:** Reproduce with protected team pointers, then fix/version upstream and independently verify pointer identities.

### N02 — P0: Move pointer range inversion

UpdateMove passes (rangeStart,rangeEnd,pointer) to the same reversed-argument helper; neither normal range matches. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/SlotPointerUtil.cs#L64-L92). **Implementer ticket:** Test moves both directions with multiple pointer arrays, including duplicate references.

### N03 — P0: MoveBox non-conserving permutation

For two distinct boxes A,B, MoveBox(0,1) iterates i=0 only and copies A to box 1, leaving A at box 0; the source trace yields A,A, losing B. Not runtime-reproduced. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L587-L630). **Implementer ticket:** Do not expose MoveBox; test every ordered pair for lossless permutations and raw byte conservation.

### N04 — P0: MoveBox skips lock guard

MoveBox validates indices but not IsBoxAbleToMove; SwapBox checks both regions for protected pointers before writing. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L587-L645). **Implementer ticket:** Refuse any protected-region move and prove unchanged bytes and metadata.

### N05 — P0: SortBoxes rewrites protected slots

SortBoxes excludes protected indices from the ordering but calls SetBoxSlotAtIndex on every slot, including protected ones, potentially reserializing unchanged entities. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L684-L712). **Implementer ticket:** Require exact protected-range byte identity after sort; do not equate unchanged species with unchanged bytes.

### N06 — P0: Direct slot setter has no protection guard

SetBoxSlotAtIndex forwards to SetBoxSlot without checking GetBoxSlotFlags; the caller must validate destination and source identity. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L553-L583). **Implementer ticket:** Gate every drag/import/duplicate/batch target against flags and authenticated layout.

### N07 — P1: SetBoxData is partial by design

SetBoxData skips protected slots, writes other slots and returns skipped count, not an all-or-nothing result. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L438-L451). **Implementer ticket:** Stage and reject mixed protected batches or explicitly present exact proposed partial diff.

### N08 — P1: Empty-slot search ignores flags

NextOpenBoxSlot checks IsPKMPresent but not IsOverwriteProtected; an empty protected slot can be returned. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L479-L505). **Implementer ticket:** Test protected-empty slots, full storage and invalid starting indices.

### N09 — P1: PartyData setter compacts

PartyData setter discards species-zero entries and writes members sequentially using default import settings; sparse physical layout is not retained. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L181-L205). **Implementer ticket:** Compare no-op and sparse party bytes, dex, records and handler fields.

### N10 — P1: Empty party setter changes count

SetPartySlotAtIndex of a blank at an occupied index sets PartyCount to that index without shifting later members. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L212-L233). **Implementer ticket:** Test clearing first/middle/last slots versus DeletePartySlot.

### N11 — P1: Party deletion shifts members

DeletePartySlot moves subsequent members down and blanks the final slot with explicit None settings. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L268-L280). **Implementer ticket:** Verify six-position delete permutations, party count and tail bytes.

### N12 — P1: Import defaults are process-wide

SaveFile static update settings govern adaptation, Pokédex and record side effects; default slot writes can change unrelated fields. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L283-L369). **Implementer ticket:** Use explicit operation settings and test concurrent document policy isolation.

### N13 — P1: BoxData eagerly reads all slots

BoxData allocates BoxCount*BoxSlotCount entities; a phone box grid should not rely on eager full-box projection for each scroll. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L420-L436). **Implementer ticket:** Benchmark bounded page reads and stale document cancellation.

### N14 — P1: ClearBoxes is not transactional

ClearBoxes checks protection but clears eligible slots one by one; this method has no rollback if later work fails. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L730-L763). **Implementer ticket:** Stage candidate and test failure after each deletion.

### N15 — P1: Compression bypasses flag check

CompressStorage copies raw occupied bytes forward and updates pointers but does not inspect overwrite-protection flags. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/StorageUtil.cs#L10-L53). **Implementer ticket:** Verify linked/locked slots and raw erased tails are preserved or refuse compression.

### N16 — P1: Import can clear before validating success

LoadBoxes may call ClearBoxes before ImportPKMs, then report no files if import returns zero. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/BoxUtil.cs#L148-L175). **Implementer ticket:** Test zero compatible files and later invalid input; require no partial publication.

### N17 — P1: Box dump skips filename collisions

DumpBoxes/DumpBox skip an entity if its filename already exists, so exported file count may omit Pokémon. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/BoxUtil.cs#L23-L90). **Implementer ticket:** Use collision-safe IDs and a verified manifest; never silently omit.

### N18 — P1: Box dump is normalized party-format data

DumpBoxes calls ForcePartyData and WriteDecryptedDataParty; it is not a raw encrypted box-slot backup. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/BoxUtil.cs#L48-L57). **Implementer ticket:** Distinguish interchange entity from byte-exact backup and test desktop parity.

### N19 — P1: Slot flags have distinct meanings

StorageSlotSource encodes party, battle-team, starter and locked bits; protection includes Locked, Starter and battle-team flags. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Storage/StorageSlotSource.cs#L9-L51). **Implementer ticket:** Expose raw flags plus typed reason and avoid conflating membership with permission.

### N20 — P1: BDSP derives flags from team pointers

SAV8BS uses TeamSlots.IndexOf and a team-lock bit to build flags, making box identity dependent on external references. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SAV8BS.cs#L145-L164). **Implementer ticket:** Create locked/unlocked BDSP team fixtures and check pointer conservation after reorder.

### N21 — P1: Desktop layout edits clone before commit

WinForms SAV_BoxLayout edits a cloned save and CopyChangesFrom only on Save; Cancel closes without copying. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_BoxLayout.cs#L12-L38). **Implementer ticket:** Use an isolated Android draft with byte-identical Cancel.

### N22 — P1: Desktop reorders with SwapBox

WinForms up/down calls SwapBox and undoes UI movement when it refuses a locked/team slot, not MoveBox. [Pinned source](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.WinForms/Subforms/Save%20Editors/Gen6/SAV_BoxLayout.cs#L273-L319). **Implementer ticket:** Test UI and Core separately; do not treat this as coverage for MoveBox.

### N23 — P1: Box metadata varies by generation

Forge and desktop distinguish editable names, wallpapers, fixed images and generation-specific name lengths. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/BoxLayoutService.cs#L24-L49). **Implementer ticket:** Build per-save capability map; test unsupported wallpaper/name edits.

### N24 — P0: Forge duplicate lacks destination flag guard

DuplicateSlot validates coordinates, occupancy and party capacity but does not check protected flags before SetBoxSlotAtIndex. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveEngineSession.cs#L543-L562). **Implementer ticket:** Refuse protected empty destinations, not only occupied ones.

### N25 — P0: Forge move has partial multi-write paths

MoveSlot performs source/destination writes sequentially, without an explicit flag check or local rollback in this method. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveEngineSession.cs#L565-L653). **Implementer ticket:** Inject second-write failures, stale targets and locked slots; restore both slots.

### N26 — P0: Forge living-dex inflate unbounded

PlaceLivingDex inflates a supplied GZip to MemoryStream before count/length checks and places entities sequentially; no explicit expansion cap or atomic staging. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveEngineSession.cs#L664-L690). **Implementer ticket:** Bound inflation and record lengths; fuzz truncated bundles and reject partial placement.

### N27 — P1: Forge sort clones but ignores protection

SortBoxes clones sources before mutation, a useful safeguard, but rewrites selected boxes without checking flags or deduplicating selected box indices. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveEngineSession.cs#L693-L741). **Implementer ticket:** Test duplicate box selection and team/locked slots; require exact unchanged protected bytes.

### N28 — P1: Forge bulk cancellation can leave live partial state

BulkBoxActions applies candidates one by one and checks cancellation between iterations; caller rollback is essential. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/BulkBoxActions.cs#L22-L61). **Implementer ticket:** Cancel after each iteration and verify baseline restoration and no provider write.

### N29 — P0: Forge risk confirmation is not layout authentication

Mutation funnel rolls back on exceptions but permits user confirmation to retry a suspected ROM-hack write. [Pinned source](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/ViewModels/BoxBrowserViewModel.cs#L427-L535). **Implementer ticket:** Our port must reject unsupported custom layouts even after risk confirmation.

### N30 — P0: MAUI drag swap can partially mutate

BoxTab drag handler writes two slots sequentially; catch writes a fallback into the target rather than restoring both originals. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L144-L190). **Implementer ticket:** Inject failure on second write; both source and destination must remain intact.

### N31 — P0: MAUI remote/local state can diverge

Inject/delete sends remote bytes before local mutation and catch repeats local write; no remote readback or rollback is shown. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L218-L276). **Implementer ticket:** Keep live injection disabled pending authenticated target, readback and recovery tests.

### N32 — P1: MAUI party handlers use global save

PartyTab uses static sav and selected item after an action sheet, without document-generation token checks. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/PartyTab.xaml.cs#L45-L90). **Implementer ticket:** Replace document during action sheet; stale operation must refuse.

### N33 — P0: MAUI batch writes every collected slot

Batch editor warns about zero instructions without returning and later writes every collected slot, although protection is checked only in processing. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BatchEditor.xaml.cs#L87-L155). **Implementer ticket:** Reject empty batch and stage only changed authorized slots; verify protected raw bytes.

### N34 — P1: MAUI random team assumes six free slots

copyboxdata indexes empties[0..5] without checking capacity and runs inside Task.Run against global sav. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L302-L340). **Implementer ticket:** Test zero to five free slots, cancellation and stale document replacement.

### N35 — P1: MAUI share is not verified export

ShareDrop writes a filename-derived cache file and invokes the share sheet; no manifest or recipient verification exists in this method. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L285-L301). **Implementer ticket:** Use collision-safe staged names and distinguish share request from completed export.

### N36 — P1: Current mobile snapshot omits slot protection metadata

Local read-only party/box snapshots contain occupied Pokémon but no per-slot team/lock/starter flags or box layout/unlock state. [Pinned source](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/d1004d83b788fed982adf8d71b132a9dc11be6af/src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs#L6-L35). **Implementer ticket:** Add bounded typed read-only metadata and independent desktop fixtures before editing.

### N37 — P1: Code GPL does not automatically license all art

Official, Forge and MAUI ship GPLv3 license texts, but resource trees, binaries and proprietary game assets need individual provenance. [Pinned source](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt#L1-L16). **Implementer ticket:** Track code, dependency and asset rights separately; retain GPL notices and source obligations.


## Architecture, parity and licensing disposition

Model each slot as a typed read-only projection: exact document generation and SHA-256, authenticated game/layout (when available), party/box/slot index, occupancy, raw `StorageSlotSource`, derived lock reason, linked team pointer and source-slot digest. Treat `BoxesUnlocked`, box name, wallpaper and fixed artwork as separate capabilities. Do not interpret the current read-only projection as authorization to edit.

For future edits, clone an immutable baseline; validate all source/destination coordinates, protected references and target identity; stage changes; independently reopen with same-version Core; compare raw untouched ranges and pointers; prove backup readback and rollback before provider publication. Unknown ROM-hack layouts must remain read-only without a user-confirmation bypass.

The [official GPLv3 license](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/LICENSE#L1-L16), [Forge GPLv3 license](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/LICENSE#L1-L16) and [MAUI GPLv3 license](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt#L1-L16) cover the projects' declared code licensing, **not** automatic reuse rights for each sprite, resource, prebuilt DLL or commercial asset. Preserve GPL notices/source obligations and audit each imported binary/asset independently. No proprietary files were copied in this audit.

## Remaining gates

All 42 [storage acceptance scenarios](PARITY_STORAGE_SLOT_GATES_2026-10-28.md) are proposed, not executed. Blockers: same-version desktop fixtures for protected/team slots and reordering, independent byte-level save round trips, authenticated custom layouts, Android provider interruptions and physical-device verification. Prioritize N01–N06, N24–N26, N29–N31 and N33 before considering any writer.
