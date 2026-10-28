using System.Buffers.Binary;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected fail-closed HGSS rejection.");
}
static ExpectedHgssReadOnly Expected(HgssReadOnly view) =>
    new(view.Gadgets with
    {
        ApricornCounts = view.Gadgets.ApricornCounts.ToArray(),
        PokegearCallerIds = view.Gadgets.PokegearCallerIds.ToArray(),
    }, view.Pokeathlon with
    {
        CourseBestScores = view.Pokeathlon.CourseBestScores.ToArray(),
        EventBestScores = view.Pokeathlon.EventBestScores.ToArray(),
        GlobalCountersRaw = view.Pokeathlon.GlobalCountersRaw.ToArray(),
    });
static List<string> Diff(HgssReadOnly? actual, ExpectedHgssReadOnly? expected)
{
    var errors = new List<string>();
    HgssProjectionVerifier.Compare(actual, expected, errors);
    return errors;
}

var save = new SAV4HGSS();
var general = save.General;
var callerCount = save.GetPokeGearRoloDex().Length;
Must(callerCount is > 0 and <= 128, "Pinned Core caller count changed.");
BinaryPrimitives.WriteUInt32LittleEndian(general.Slice(0xE704, 4), uint.MaxValue);
BinaryPrimitives.WriteUInt32LittleEndian(general.Slice(0xE708, 4), 1234567);
BinaryPrimitives.WriteUInt32LittleEndian(general.Slice(0xE70C, 4), 0xF8000001);
general[0xE558] = 255;
general[0xE55E] = 127;
general[0xC0EC] = 0xFE;
general[0xC0EC + callerCount - 1] = 0xFD;

var p = save.Pokeathlon.Data;
p[0xDC] = 0xE1; // one modeled medal plus three reserved bits
p[0xDC + 492] = 0xFF;
BinaryPrimitives.WriteUInt16LittleEndian(p.Slice(6, 2), ushort.MaxValue);
BinaryPrimitives.WriteUInt16LittleEndian(p.Slice(4 * 0x2C + 6, 2), 65000);
BinaryPrimitives.WriteUInt16LittleEndian(p.Slice(0xAEC + 9 * 2, 2), 12345);
BinaryPrimitives.WriteUInt32LittleEndian(p.Slice(0xB00, 4), uint.MaxValue);
BinaryPrimitives.WriteUInt32LittleEndian(p.Slice(0xB00 + 28 * 4, 4), 987654321);
BinaryPrimitives.WriteUInt32LittleEndian(p.Slice(0xB74, 4), uint.MaxValue);
BinaryPrimitives.WriteUInt32LittleEndian(p.Slice(0xB78, 4), 0xF8000001);
BinaryPrimitives.WriteUInt16LittleEndian(p.Slice(0xB7C, 2), 0xF001);
var before = save.General.ToArray();

var projection = HgssReadOnly.Project(save)!;
Must(projection.Gadgets.WalkerSteps == uint.MaxValue &&
    projection.Gadgets.WalkerWatts == 1234567 &&
    projection.Gadgets.WalkerFlagsRaw == 0xF8000001 &&
    projection.Gadgets.ApricornCounts[0] == 255 &&
    projection.Gadgets.ApricornCounts[6] == 127 &&
    projection.Gadgets.PokegearCallerIds[0] == 0xFE &&
    projection.Gadgets.PokegearCallerIds[^1] == 0xFD &&
    projection.Pokeathlon.PointsRaw == uint.MaxValue &&
    projection.Pokeathlon.DataCardFlagsRaw == 0xF8000001 &&
    projection.Pokeathlon.DailyShopFlagsRaw == 0xF001 &&
    projection.Pokeathlon.ModeledMedalCount == 6 &&
    projection.Pokeathlon.MedalEntriesWithReservedBits == 2 &&
    projection.Pokeathlon.CourseBestScores[0] == ushort.MaxValue &&
    projection.Pokeathlon.CourseBestScores[4] == 65000 &&
    projection.Pokeathlon.EventBestScores[9] == 12345 &&
    projection.Pokeathlon.GlobalCountersRaw[0] == uint.MaxValue &&
    projection.Pokeathlon.GlobalCountersRaw[28] == 987654321 &&
    save.General.SequenceEqual(before),
    "HGSS raw reader normalized reserved bits, counters, caller IDs or mutated Core.");
Must(Diff(projection, Expected(projection)).Count == 0, "HGSS exact synthetic oracle failed.");

var expected = Expected(projection);
Must(Diff(projection, expected with
{
    Gadgets = expected.Gadgets with { WalkerFlagsRaw = 1 },
}).Contains("HGSS.Gadgets mismatch"), "Reserved Walker bits not compared.");
Must(Diff(projection, expected with
{
    Pokeathlon = expected.Pokeathlon with { DataCardFlagsRaw = 1 },
}).Contains("HGSS.Pokeathlon mismatch"), "Reserved Data Card bits not compared.");
Must(Diff(projection, expected with
{
    Gadgets = expected.Gadgets with { ApricornCounts = [1, 2] },
}).Contains("HGSS.Gadgets invalid shape"), "Truncated apricorn oracle passed.");
Must(Diff(projection, expected with
{
    Pokeathlon = expected.Pokeathlon with { GlobalCountersRaw = [] },
}).Contains("HGSS.Pokeathlon invalid shape"), "Truncated counters oracle passed.");
Must(Diff(projection, expected with
{
    Pokeathlon = expected.Pokeathlon with { RawSha256 = "not-a-hash" },
}).Contains("HGSS.Pokeathlon invalid shape"), "Malformed digest oracle passed.");
Reject<InvalidDataException>(() => HgssReadOnly.ProjectGadgets(new byte[SAV4HGSS.GeneralSize - 1], callerCount));
Reject<InvalidDataException>(() => HgssReadOnly.ProjectGadgets(new byte[SAV4HGSS.GeneralSize], 0));
Reject<InvalidDataException>(() => HgssReadOnly.ProjectGadgets(new byte[SAV4HGSS.GeneralSize], 129));
Reject<InvalidDataException>(() => HgssReadOnly.ProjectPokeathlon(new byte[Pokeathlon4.SIZE - 1]));
Reject<InvalidDataException>(() => HgssReadOnly.ProjectPokeathlon(new byte[Pokeathlon4.SIZE + 1]));

general[0xE558] ^= 0xFF;
p[0xDC] = 0;
Must(projection.Gadgets.ApricornCounts[0] == 255 &&
    projection.Pokeathlon.ModeledMedalCount == 6 &&
    HgssReadOnly.Project(save)!.Pokeathlon.RawSha256 != projection.Pokeathlon.RawSha256,
    "HGSS projection retained mutable Core memory.");
if (projection.Gadgets.ApricornCounts is IList<byte> mutable)
    Reject<NotSupportedException>(() => mutable[0] = 0);
if (projection.Pokeathlon.GlobalCountersRaw is IList<uint> mutableCounters)
    Reject<NotSupportedException>(() => mutableCounters[0] = 0);

var sample = new SaveReadOnlyResult("SAV4HGSS", "HG", 4, "TEST", 0, 0,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint) { Hgss = projection };
var display = ReadOnlyProjectionPresenter.FormatTrainer(sample, false);
Must(display.Contains("HGSS gadgets") &&
    display.Contains("Pokéathlon") &&
    display.Contains("No editing, export"),
    "HGSS Android presentation missing or implied editing.");
Must(HgssReadOnly.Project(new SAV4DP()) is null,
    "HGSS diagnostics leaked into another Gen4 format.");
Console.WriteLine("Synthetic HGSS raw Walker, apricorn, Pokégear and Pokéathlon projections verified; no independent desktop/device certification.");
