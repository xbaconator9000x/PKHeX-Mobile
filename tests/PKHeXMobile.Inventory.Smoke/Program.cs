using System.Buffers.Binary;
using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static void RefuseMutation<T>(IReadOnlyList<T> values, T candidate)
{
    if (values is not IList<T> list) return;
    try { list.Add(candidate); throw new Exception("Mutable inventory projection"); }
    catch (NotSupportedException) { }
}

// Synthetic item blocks: not desktop-matched saves or ROM data.
var sv = new byte[0xBB80];
var svId = new PlayerBag9(sv).Pouches[0].Items[0].Index;
Must(svId is > 0 and < 3000, "Invalid synthetic SV item ID");
int offset = svId * InventoryItem9.SIZE;
BinaryPrimitives.WriteUInt32LittleEndian(sv.AsSpan(offset), 0);
BinaryPrimitives.WriteInt32LittleEndian(sv.AsSpan(offset + 4), 0);
BinaryPrimitives.WriteUInt32LittleEndian(sv.AsSpan(offset + 8), 7);
BinaryPrimitives.WriteUInt32LittleEndian(sv.AsSpan(offset + 12), 0xAABBCCDD);
var svOriginal = sv.ToArray();
var bag = new PlayerBag9(sv);
var inventory = InventoryReadOnly.Project(bag)!;
var first = inventory.Pouches[0].Entries[0];
Must(first.ItemId == svId && first.Count == 0 && first.Flags == 7 &&
    first.Padding == 0xAABBCCDD && first.PouchCode == 0 &&
    first.CoreItemType == nameof(InventoryItem9), "SV flags/padding or zero-count record lost");
Must(sv.SequenceEqual(svOriginal), "SV read changed source bytes");
RefuseMutation(inventory.Pouches, new InventoryPouchReadOnly("Fake", 0, []));
RefuseMutation(inventory.Pouches[0].Entries,
    new InventoryEntryReadOnly(0, 0, 0, "Fake", null, null, null));
bag.Pouches[0].Items[0].Count = 99;
Must(inventory.Pouches[0].Entries[0].Count == 0, "Snapshot retains mutable Core item");

var za = new byte[0xBB80];
var zaId = new PlayerBag9a(za).Pouches[0].Items[0].Index;
Must(zaId is > 0 and < 3000, "Invalid synthetic ZA item ID");
offset = zaId * InventoryItem9a.SIZE;
BinaryPrimitives.WriteUInt32LittleEndian(za.AsSpan(offset), 0);
BinaryPrimitives.WriteInt32LittleEndian(za.AsSpan(offset + 4), 3);
BinaryPrimitives.WriteUInt32LittleEndian(za.AsSpan(offset + 8), 0x1F);
BinaryPrimitives.WriteUInt32LittleEndian(za.AsSpan(offset + 12), 0x12345678);
var zaOriginal = za.ToArray();
var zaInventory = InventoryReadOnly.Project(new PlayerBag9a(za))!;
var zaFirst = zaInventory.Pouches[0].Entries[0];
Must(zaFirst.ItemId == zaId && zaFirst.Count == 3 &&
    zaFirst.Flags == 0x1F && zaFirst.Padding == 0x12345678 &&
    zaFirst.CoreItemType == nameof(InventoryItem9a), "ZA held/shop flags lost");
Must(za.SequenceEqual(zaOriginal), "ZA read changed source bytes");

static ExpectedInventoryReadOnly Expect(InventoryReadOnly source) => new(
    source.Pouches.Select(p => new ExpectedInventoryPouchReadOnly(
        p.Type, p.MaxCount, p.Entries.Select(e => new ExpectedInventoryEntryReadOnly(
            e.SlotIndex, e.ItemId, e.Count, e.CoreItemType,
            e.PouchCode, e.Flags, e.Padding)).ToArray())).ToArray());
var expected = Expect(inventory);
var actualSave = new SaveReadOnlyResult(
    "SYNTHETIC", "TEST", 9, "TEST", 1, 2, 0, 0, 0, [], [],
    new SaveFingerprint(1, "TEST")) { Inventory = inventory };
var reference = new ExpectedSaveProjection(
    "SYNTHETIC", "TEST", 9, "TEST", 1, 2, 0, 0, 0, [], [])
{ Inventory = expected };
Must(SaveProjectionVerifier.Compare(actualSave, reference,
    requireInventory: true).Count == 0, "Schema v5 positive mismatch");
void MustDetect(ExpectedInventoryReadOnly changed, string path)
{
    Must(SaveProjectionVerifier.Compare(actualSave, reference with { Inventory = changed },
        requireInventory: true).Any(e => e.Contains(path, StringComparison.Ordinal)),
        "Schema v5 missed " + path);
}
var pouch0 = expected.Pouches[0];
var item0 = pouch0.Entries[0];
ExpectedInventoryReadOnly ChangeEntry(ExpectedInventoryEntryReadOnly entry) =>
    expected with { Pouches = expected.Pouches.Select((p, i) =>
        i == 0 ? p with { Entries = p.Entries.Select((e, j) =>
            j == 0 ? entry : e).ToArray() } : p).ToArray() };
MustDetect(ChangeEntry(item0 with { Count = 42 }), ".Count mismatch");
MustDetect(ChangeEntry(item0 with { ItemId = item0.ItemId + 1 }), ".ItemId mismatch");
MustDetect(ChangeEntry(item0 with { Flags = 0 }), ".Flags mismatch");
MustDetect(ChangeEntry(item0 with { Padding = 0 }), ".Padding mismatch");
MustDetect(ChangeEntry(item0 with { PouchCode = 4 }), ".PouchCode mismatch");
MustDetect(ChangeEntry(item0 with { CoreItemType = "Other" }), ".CoreItemType mismatch");
MustDetect(ChangeEntry(item0 with { SlotIndex = 9 }), "invalid slot order");
MustDetect(ChangeEntry(item0 with { Flags = null }), "incomplete raw item fields");
MustDetect(expected with { Pouches = null! }, "Pouches missing");
MustDetect(expected with { Pouches = [] }, "Pouches count out of range");
MustDetect(expected with { Pouches = expected.Pouches.Select((p, i) =>
    i == 0 ? p with { Entries = null! } : p).ToArray() }, "missing entries");
Must(SaveProjectionVerifier.Compare(actualSave, reference with { Inventory = null },
    requireInventory: true).Contains("Inventory missing from expectation"),
    "Schema v5 accepted absent expectation");
Must(SaveProjectionVerifier.Compare(actualSave with { Inventory = null }, reference,
    requireInventory: true).Contains("Inventory missing from observed projection"),
    "Schema v5 accepted absent observed inventory");
try
{
    JsonSerializer.Deserialize<ExpectedInventoryEntryReadOnly>(
        """{"slotIndex":0,"itemId":1,"count":0,"coreItemType":"InventoryItem9","flags":0,"padding":0}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required pouch code was accepted");
}
catch (JsonException) { }
var display = actualSave with { Inventory = zaInventory };
Must(ReadOnlyProjectionPresenter.FormatTrainer(display, false)
    .Contains("not a raw-block inventory audit"), "Inventory scope warning missing");
Must(ReadOnlyProjectionPresenter.InventoryPouchLabels(display).Count == zaInventory.Pouches.Count,
    "Pouch selector lost Core pouches");
var detail = ReadOnlyProjectionPresenter.FormatInventoryPouch(display, 0, 2);
Must(detail.Contains("ID " + zaId) && detail.Contains("flags 0x0000001F") &&
    detail.Contains("padding 0x12345678"), "Raw ZA flags not displayed");
try
{
    ReadOnlyProjectionPresenter.FormatInventoryPouch(display, zaInventory.Pouches.Count);
    throw new Exception("Invalid pouch selection accepted");
}
catch (ArgumentOutOfRangeException) { }
Must(sv.SequenceEqual(svOriginal) && za.SequenceEqual(zaOriginal),
    "Inventory presentation or comparison modified source bytes");
Console.WriteLine("Synthetic SV/ZA raw item fields, immutable zero-count snapshots, schema v5 mismatches and bounded pouch presentation passed.");
Console.WriteLine("No independent desktop fixture, Android device test or save export was performed.");
