using System.Buffers.Binary;
using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
static List<string> Diff(Gen6SystemsReadOnly? a, ExpectedGen6SystemsReadOnly? b)
{
    var errors = new List<string>();
    Gen6SystemsProjectionVerifier.Compare(a, b, errors);
    return errors;
}

var xy = new SAV6XY();
var training = xy.SuperTrain.Data;
var power = xy.OPower.Data;
Must(training.Length == Gen6SystemsReadOnly.SuperTrainingBytes &&
    power.Length == Gen6SystemsReadOnly.PowerBytes, "Pinned Core Gen6 block layout changed.");
training[0] = 0xA5;
training[6] = 0xFC; // distribution reserved bits
training[7] = 0xFF; // counter above normal UI cap
BinaryPrimitives.WriteUInt32LittleEndian(training.Slice(8, 4), 0x7FC00001); // NaN payload
BinaryPrimitives.WriteUInt32LittleEndian(training.Slice(0xC8 + 47 * 4, 4), 0xFF800000); // -infinity
BinaryPrimitives.WriteUInt16LittleEndian(training.Slice(0x188, 2), 0xFABC);
training[0x18A] = 0xFE;
training[0x18B] = 0xFD;
BinaryPrimitives.WriteUInt16LittleEndian(training.Slice(0x248 + 47 * 4, 2), 0xBEEF);
training[0x308] = 0x25;
training[0x313] = 0x99;
BinaryPrimitives.WriteUInt32LittleEndian(training.Slice(0x314, 4), 0xDEADBEEF);
power[0] = 0xFF;
power[64] = 0xFE;
power[65] = 0xFD;
power[66] = 0xFC;
power[99] = 0xFB;
var beforeTraining = training.ToArray();
var beforePower = power.ToArray();
var view = Gen6SystemsReadOnly.Project(xy)!;
Must(view.SuperTraining.RegimenFlagsRaw[0] == 0xA5 &&
    view.SuperTraining.DistributionFlagsRaw == 0xFC &&
    view.SuperTraining.CounterRaw == 0xFF &&
    view.SuperTraining.RecordTime1Bits[0] == 0x7FC00001 &&
    view.SuperTraining.RecordTime2Bits[47] == 0xFF800000 &&
    view.SuperTraining.Holders1[0] == new Gen6TrainingHolder(0xFABC, 0xFE, 0xFD) &&
    view.SuperTraining.Holders2[47].Species == 0xBEEF &&
    view.SuperTraining.BagsRaw[0] == 0x25 &&
    view.SuperTraining.BagsRaw[11] == 0x99 &&
    view.SuperTraining.TutorialRaw == 0xDEADBEEF &&
    view.OPower.FlagsRaw[0] == 0xFF &&
    view.OPower.FlagsRaw[64] == 0xFE &&
    view.OPower.PointsRaw == 0xFD &&
    view.OPower.LevelsRaw[0] == 0xFC &&
    view.OPower.LevelsRaw[33] == 0xFB,
    "Gen6 raw projection lost sentinel bytes, float bits, holders, bags or O-Power flags.");
Must(training.SequenceEqual(beforeTraining) && power.SequenceEqual(beforePower),
    "Read-only projection changed the pinned Core backing blocks.");
Must(view.SuperTraining.RecordTime1Bits.Count == 48 &&
    view.SuperTraining.RecordTime2Bits.Count == 48 &&
    view.SuperTraining.Holders1.Count == 48 &&
    view.SuperTraining.Holders2.Count == 48 &&
    view.OPower.FlagsRaw.Count == 65 && view.OPower.LevelsRaw.Count == 34,
    "Gen6 raw field cardinality drifted.");
var expected = new ExpectedGen6SystemsReadOnly(view.SuperTraining, view.OPower);
Must(Diff(view, expected).Count == 0, "Exact synthetic Gen6 parity failed.");
Must(Diff(view, expected with { SuperTraining = expected.SuperTraining with { CounterRaw = 0 } })
    .Contains("Gen6Systems.SuperTraining mismatch"), "Counter difference was ignored.");
Must(Diff(view, expected with { SuperTraining = expected.SuperTraining with { RecordTime1Bits = [] } })
    .Contains("Gen6Systems invalid shape or raw fields"), "Truncated times accepted.");
Must(Diff(view, expected with { OPower = expected.OPower with { FlagsRaw = [] } })
    .Contains("Gen6Systems invalid shape or raw fields"), "Truncated flags accepted.");
Must(Diff(view, expected with { OPower = expected.OPower with { RawSha256 = "bad" } })
    .Contains("Gen6Systems invalid shape or raw fields"), "Malformed hash accepted.");
Must(Diff(view, expected with { OPower = expected.OPower with { PointsRaw = 0 } })
    .Contains("Gen6Systems.OPower mismatch"), "O-Power point difference was ignored.");
Reject<JsonException>(() => JsonSerializer.Deserialize<ExpectedGen6SystemsReadOnly>("{}"));
Reject<InvalidDataException>(() => Gen6SystemsReadOnly.ProjectTraining(new byte[0x317]));
Reject<InvalidDataException>(() => Gen6SystemsReadOnly.ProjectPower(new byte[101]));
if (view.SuperTraining.BagsRaw is IList<byte> writable)
    Reject<NotSupportedException>(() => writable[0] = 0);
if (view.OPower.FlagsRaw is IList<byte> writableFlags)
    Reject<NotSupportedException>(() => writableFlags[0] = 0);

// The pinned Core allocator incorrectly selects an occupied bag, and fails on
// an entirely empty list. Reproduce only on disposable synthetic saves.
var badAllocator = new SAV6XY();
Must(badAllocator.SuperTrain.GetOpenBagIndex() == -1 &&
    !badAllocator.SuperTrain.AddBag(8), "Pinned empty-bag defect changed.");
badAllocator.SuperTrain.SetBag(0, 7);
Must(badAllocator.SuperTrain.GetOpenBagIndex() == 0 &&
    badAllocator.SuperTrain.AddBag(8) &&
    badAllocator.SuperTrain.GetBag(0) == 8, "Pinned occupied-bag overwrite defect changed.");
Must(view.SuperTraining.BagsRaw[0] == 0x25, "Defect reproduction mutated detached view.");

training[0] ^= 0xFF;
power[64] ^= 0xFF;
Must(view.SuperTraining.RegimenFlagsRaw[0] == 0xA5 &&
    view.OPower.FlagsRaw[64] == 0xFE &&
    Gen6SystemsReadOnly.Project(xy)!.OPower.RawSha256 != view.OPower.RawSha256,
    "Gen6 projection retained mutable backing data.");
var oras = new SAV6AO();
Must(Gen6SystemsReadOnly.Project(oras) is not null &&
    Gen6SystemsReadOnly.Project(new SAV5BW()) is null,
    "Gen6 XY/ORAS dispatch leaked to another generation.");
var fake = new SaveReadOnlyResult("SAV6XY", "X", 6, "TEST", 0, 0,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint)
{ Gen6Systems = view };
var display = ReadOnlyProjectionPresenter.FormatTrainer(fake, false);
Must(display.Contains("Gen6 Super Training") &&
    display.Contains("O-Power") && display.Contains("no edit authorization"),
    "Android summary omitted Gen6 diagnostics or implied write support.");
Console.WriteLine("Gen6 XY/ORAS raw Super Training and O-Power projections, schema v16, immutable sentinels and Core bag allocator defect verified synthetically; no desktop/device certification.");
