using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static void MustBeImmutable<T>(IReadOnlyList<T> values, T element)
{
    if (values is not IList<T> list)
        return;
    try { list.Add(element); throw new Exception("Mutable storage snapshot"); }
    catch (NotSupportedException) { }
}

// Core-only synthetic projection; no desktop-matched fixture or real save.
var gen1 = new SAV1(LanguageID.English, GameVersion.RB);
var coreFlags = StorageFlagsReadOnly.Project(gen1);
Must(coreFlags.Boxes.Count == gen1.BoxCount &&
    coreFlags.Boxes.All(b => b.Slots.Count == gen1.BoxSlotCount),
    "Core flag projection must preserve all empty positions");
MustBeImmutable(coreFlags.Boxes, new StorageBoxFlagsReadOnly(0, []));
MustBeImmutable(coreFlags.Boxes[0].Slots, new StorageSlotFlagsReadOnly(0, 0, false));

// Distinct raw flags are preserved even for an empty slot; do not infer
// permission to edit from the presence/absence of Core protection bits.
var source = new StorageFlagsReadOnly(
[
    new StorageBoxFlagsReadOnly(0,
    [
        new StorageSlotFlagsReadOnly(0, 0, false),
        new StorageSlotFlagsReadOnly(1, (int)StorageSlotSource.BattleTeam1, true),
    ]),
    new StorageBoxFlagsReadOnly(1,
    [
        new StorageSlotFlagsReadOnly(0, (int)StorageSlotSource.Party1, false),
        new StorageSlotFlagsReadOnly(1, (int)StorageSlotSource.Locked, true),
    ]),
]);
Must(source.ProtectedCount == 2, "Protection summary lost flagged slots");
var actual = new SaveReadOnlyResult("SYNTHETIC", "TEST", 8, "TEST", 1, 2,
    0, 2, 2, [], [new BoxSnapshot(0, []), new BoxSnapshot(1, [])],
    new SaveFingerprint(1, "TEST")) { Storage = source };
var expectedStorage = new ExpectedStorageFlagsReadOnly(
[
    new ExpectedStorageBoxFlagsReadOnly(0,
    [
        new ExpectedStorageSlotFlagsReadOnly(0, 0, false),
        new ExpectedStorageSlotFlagsReadOnly(1, (int)StorageSlotSource.BattleTeam1, true),
    ]),
    new ExpectedStorageBoxFlagsReadOnly(1,
    [
        new ExpectedStorageSlotFlagsReadOnly(0, (int)StorageSlotSource.Party1, false),
        new ExpectedStorageSlotFlagsReadOnly(1, (int)StorageSlotSource.Locked, true),
    ]),
]);
var expected = new ExpectedSaveProjection("SYNTHETIC", "TEST", 8, "TEST",
    1, 2, 0, 2, 2, [], [new ExpectedBoxSnapshot(0, []), new ExpectedBoxSnapshot(1, [])])
    { Storage = expectedStorage };
Must(SaveProjectionVerifier.Compare(actual, expected, requireStorage: true).Count == 0,
    "Schema v6 synthetic matching flags rejected");

void MustRefuse(ExpectedStorageFlagsReadOnly altered, string message)
{
    Must(SaveProjectionVerifier.Compare(actual, expected with { Storage = altered },
        requireStorage: true).Count != 0, message);
}
ExpectedStorageFlagsReadOnly ChangeFirst(ExpectedStorageSlotFlagsReadOnly changed) =>
    expectedStorage with { Boxes =
    [
        expectedStorage.Boxes[0] with { Slots =
        [
            changed, expectedStorage.Boxes[0].Slots[1],
        ] },
        expectedStorage.Boxes[1],
    ] };
MustRefuse(ChangeFirst(expectedStorage.Boxes[0].Slots[0] with { RawFlags = 1 }),
    "Raw party bits mismatch not detected");
MustRefuse(ChangeFirst(expectedStorage.Boxes[0].Slots[0] with { OverwriteProtected = true }),
    "Fabricated protected flag accepted");
MustRefuse(ChangeFirst(expectedStorage.Boxes[0].Slots[0] with { RawFlags = 0x10000000 }),
    "Unreviewed flag bit accepted");
MustRefuse(ChangeFirst(expectedStorage.Boxes[0].Slots[0] with { SlotIndex = 1 }),
    "Duplicate/reordered physical index accepted");
MustRefuse(expectedStorage with { Boxes = [] }, "Missing boxes accepted");
MustRefuse(expectedStorage with { Boxes =
    [expectedStorage.Boxes[0], expectedStorage.Boxes[0]] },
    "Duplicate box index accepted");
MustRefuse(expectedStorage with { Boxes =
    [expectedStorage.Boxes[0] with { Slots = [expectedStorage.Boxes[0].Slots[0]] },
     expectedStorage.Boxes[1]] },
    "Truncated slots accepted");
MustRefuse(expectedStorage with { Boxes = [null!, expectedStorage.Boxes[1]] },
    "Null box accepted");
MustRefuse(expectedStorage with { Boxes = null! }, "Null box list accepted");
Must(SaveProjectionVerifier.Compare(actual, expected with { Storage = null },
    requireStorage: true).Contains("Storage missing from expectation"),
    "Missing expected storage accepted");
Must(SaveProjectionVerifier.Compare(actual with { Storage = null }, expected,
    requireStorage: true).Contains("Storage missing from observed projection"),
    "Missing observed storage accepted");
Must(SaveProjectionVerifier.Compare(actual with { BoxSlotCount = 3 }, expected,
    requireStorage: true).Count != 0, "Inconsistent slot count accepted");

try
{
    JsonSerializer.Deserialize<ExpectedStorageSlotFlagsReadOnly>(
        """{"slotIndex":0,"rawFlags":0}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required overwriteProtected accepted");
}
catch (JsonException) { }

var detail = ReadOnlyProjectionPresenter.FormatBoxSlotProtection(actual, 0, 1);
Must(detail.Contains("0x00000040") && detail.Contains("yes") &&
    detail.Contains("not a write permission"), "Core protection details absent");
Must(ReadOnlyProjectionPresenter.FormatTrainer(actual, false)
    .Contains("2 Core-reported overwrite-protected"),
    "Protection summary absent");
try
{
    ReadOnlyProjectionPresenter.FormatBoxSlotProtection(actual, 2, 0);
    throw new Exception("Out-of-range box accepted");
}
catch (ArgumentOutOfRangeException) { }
try
{
    ReadOnlyProjectionPresenter.FormatBoxSlotProtection(actual, 0, 2);
    throw new Exception("Out-of-range slot accepted");
}
catch (ArgumentOutOfRangeException) { }
Console.WriteLine("Synthetic Core slot flag enumeration, immutable positions, protection display and strict schema v6 negatives passed.");
Console.WriteLine("No independent desktop-matched storage fixture, Android device or write/export operation was tested.");


// P0 upstream storage defects N01/N02/N03: isolated runtime reproduction
// against pinned PKHeX.Core 26.10.18. This intentionally operates only on
// synthetic in-memory objects. It is NOT a safe reorder implementation.
// If the pinned Core behavior changes, stop and review these assertions,
// then independently verify pointers, conservation and write safety.
var pointerSamples = new List<int> { 0, 1, 29, 30, 31, 59, 60, -1 };
var originalPointers = pointerSamples.ToArray();
SlotPointerUtil.UpdateSwap(0, 1, 30, pointerSamples);
Must(pointerSamples.SequenceEqual(originalPointers),
    "Pinned Core UpdateSwap pointer behavior changed; re-audit N01 before enabling storage writes.");
var movedPointers = new List<int>(originalPointers);
SlotPointerUtil.UpdateMove(0, 1, 30, movedPointers);
Must(movedPointers.SequenceEqual(originalPointers),
    "Pinned Core UpdateMove pointer behavior changed; re-audit N02 before enabling storage writes.");
// The independent correct swap oracle would exchange 0..29 with 30..59,
// leaving 60 and -1 unchanged. Pinned Core currently does not do that.
int[] correctSwap = [30, 31, 59, 0, 1, 29, 60, -1];
Must(!pointerSamples.SequenceEqual(correctSwap),
    "Expected to reproduce the pinned Core pointer-range inversion.");
Console.WriteLine("REPRODUCED (synthetic): pinned Core UpdateSwap and UpdateMove do not remap ordinary box pointers.");

// N03: two distinct synthetic box identities are necessary. An all-empty
// save would hide the non-conserving MoveBox behavior.
var reorder = new SAV9SV();
reorder.SetBoxSlotAtIndex(new PK9 { Species = 25, PID = 0x10203040 }, 0, 0);
reorder.SetBoxSlotAtIndex(new PK9 { Species = 133, PID = 0x50607080 }, 1, 0);
ushort sourceSpecies = reorder.GetBoxSlotAtIndex(0, 0).Species;
ushort targetSpecies = reorder.GetBoxSlotAtIndex(1, 0).Species;
Must(sourceSpecies == 25 && targetSpecies == 133,
    "Synthetic storage fixture failed to establish distinct original box identities.");
bool moveResult = reorder.MoveBox(0, 1);
ushort afterSource = reorder.GetBoxSlotAtIndex(0, 0).Species;
ushort afterTarget = reorder.GetBoxSlotAtIndex(1, 0).Species;
Must(moveResult && afterSource == 25 && afterTarget == 25,
    "Pinned Core MoveBox behavior changed; re-audit N03 before enabling storage writes.");
Must(afterSource != targetSpecies && afterTarget != targetSpecies,
    "Expected to reproduce non-conservation of the original target box.");
Console.WriteLine("REPRODUCED (synthetic): pinned Core MoveBox(0,1) duplicates source box and loses target identity.");
Console.WriteLine("These are upstream defect diagnostics, not verified fixes. No private save, Android provider, export or backup was touched.");
