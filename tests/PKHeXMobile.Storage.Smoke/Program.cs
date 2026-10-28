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
