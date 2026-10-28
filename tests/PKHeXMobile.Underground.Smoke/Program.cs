using System.Buffers.Binary;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string why)
{
    if (!ok) throw new Exception(why);
}
static void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Unsafe Underground shape accepted.");
}
static ExpectedUndergroundReadOnly Expected(UndergroundReadOnly view) =>
    new(view.Gen4 is { } g ? new ExpectedUndergroundGen4(
        g.Scores.Select(s => new ExpectedUndergroundScore(s.Name, s.Value)).ToArray(),
        g.Pouches.Select(p => new ExpectedUndergroundPouch(p.Name, p.Length, p.Sha256,
            p.Items.Select(x => new ExpectedUndergroundItem(x.Index, x.Id, x.Size)).ToArray())).ToArray()) : null,
        view.Bdsp is { } b ? new ExpectedUndergroundBdsp(b.Length, b.Sha256,
            b.Items.Select(x => new ExpectedUndergroundBdspItem(x.Index, x.Count,
                x.HideNewRaw, x.FavoriteRaw)).ToArray()) : null);
static List<string> Diff(UndergroundReadOnly? view, ExpectedUndergroundReadOnly? expected)
{
    var errors = new List<string>();
    UndergroundProjectionVerifier.Compare(view, expected, errors);
    return errors;
}

var dp = new SAV4DP();
dp.UG_PeopleMet = uint.MaxValue;
dp.UG_GiftsGiven = 1_000_000;
dp.GetUGI_Traps()[0] = 255;
dp.GetUGI_Goods()[39] = 0xC1;
dp.GetUGI_Treasures()[5] = 0xB2;
dp.GetUGI_Spheres()[0] = 0x7E;
dp.GetUGI_Spheres()[40] = 0xFA;
var before = dp.General.ToArray();
var gen4 = UndergroundReadOnly.Project(dp);
Must(gen4 is { Gen4: not null, Bdsp: null } &&
    gen4.Gen4.Scores.Count == 13 &&
    gen4.Gen4.Scores[0].Value == uint.MaxValue &&
    gen4.Gen4.Scores[1].Value == 1_000_000 &&
    gen4.Gen4.Pouches.Count == 4 &&
    gen4.Gen4.Pouches[0].Items[0].Id == 255 &&
    gen4.Gen4.Pouches[3].Items[0].Size == 0xFA &&
    gen4.Gen4.Pouches[3].Length == 80 &&
    dp.General.SequenceEqual(before),
    "Gen4 score/pouch projection normalized bytes or lost fields.");
Must(Diff(gen4, Expected(gen4)).Count == 0, "Gen4 exact expectation failed.");
var expectedGen4 = Expected(gen4);
Must(Diff(gen4, expectedGen4 with
{
    Gen4 = expectedGen4.Gen4! with { Scores = [] },
}).Any(x => x.Contains("invalid score/pouch shape")), "Truncated Gen4 oracle passed.");
var changedPouches = expectedGen4.Gen4!.Pouches.ToArray();
changedPouches[3] = changedPouches[3] with { Sha256 = new string('0', 64) };
Must(Diff(gen4, expectedGen4 with
{
    Gen4 = expectedGen4.Gen4 with { Pouches = changedPouches },
}).Any(x => x.Contains("Sha256 mismatch")), "Gen4 hash mismatch passed.");
dp.GetUGI_Spheres()[40] ^= 0xFF;
Must(gen4.Gen4.Pouches[3].Items[0].Size == 0xFA &&
    UndergroundReadOnly.Project(dp)!.Gen4!.Pouches[3].Sha256 !=
    gen4.Gen4.Pouches[3].Sha256, "Gen4 projection retained mutable Core span.");
Reject<InvalidDataException>(() => UndergroundReadOnly.ProjectPouch("Goods", new byte[39], false));
Reject<InvalidDataException>(() => UndergroundReadOnly.ProjectPouch("Spheres", new byte[80], false));
Reject<InvalidDataException>(() => UndergroundReadOnly.ProjectPouch("Unknown", new byte[40], false));
if (gen4.Gen4.Pouches[0].Items is IList<UndergroundItem> readonlyItems)
    Reject<NotSupportedException>(() => readonlyItems[0] = new UndergroundItem(0, 0, null));

var bdsp = new SAV8BS();
var raw = bdsp.Underground.Data;
Must(raw.Length == 999 * 12, "Pinned BDSP Underground layout changed.");
BinaryPrimitives.WriteInt32LittleEndian(raw.Slice(0, 4), -17);
BinaryPrimitives.WriteUInt32LittleEndian(raw.Slice(4, 4), 2);
BinaryPrimitives.WriteUInt32LittleEndian(raw.Slice(8, 4), uint.MaxValue);
BinaryPrimitives.WriteInt32LittleEndian(raw.Slice(998 * 12, 4), 1_000_000);
var rawBefore = raw.ToArray();
var view = UndergroundReadOnly.Project(bdsp);
Must(view is { Gen4: null, Bdsp: not null } &&
    view.Bdsp.Items.Count == 999 &&
    view.Bdsp.Items[0].Count == -17 &&
    view.Bdsp.Items[0].HideNewRaw == 2 &&
    view.Bdsp.Items[0].FavoriteRaw == uint.MaxValue &&
    view.Bdsp.Items[998].Count == 1_000_000 &&
    bdsp.Underground.Data.SequenceEqual(rawBefore),
    "BDSP raw flags/counts were normalized during read.");
Must(Diff(view, Expected(view)).Count == 0, "BDSP strict projection failed.");
var expectedBdsp = Expected(view);
var entries = expectedBdsp.Bdsp!.Items.ToArray();
entries[0] = entries[0] with { HideNewRaw = 1 };
Must(Diff(view, expectedBdsp with
{
    Bdsp = expectedBdsp.Bdsp with { Items = entries },
}).Any(x => x.Contains("Items[0] mismatch")), "BDSP flag mismatch passed.");
Must(Diff(view, expectedBdsp with
{
    Bdsp = expectedBdsp.Bdsp with { Items = [] },
}).Any(x => x.Contains("invalid shape")), "Truncated BDSP oracle passed.");
Reject<InvalidDataException>(() => UndergroundReadOnly.ProjectBdsp(new byte[999 * 12 - 1]));
Reject<InvalidDataException>(() => UndergroundReadOnly.ProjectBdsp(new byte[999 * 12 + 1]));
if (view.Bdsp.Items is IList<UndergroundBdspItem> readonlyBdsp)
    Reject<NotSupportedException>(() => readonlyBdsp[0] = new UndergroundBdspItem(0, 0, 0, 0));
bdsp.Underground.Data[0] ^= 0xFF;
Must(view.Bdsp.Items[0].Count == -17 &&
    UndergroundReadOnly.Project(bdsp)!.Bdsp!.Sha256 != view.Bdsp.Sha256,
    "BDSP projection retained mutable Core table.");

// Synthetic reproduction of pinned Core's whole-table flag normalization.
var unsafeSave = new SAV8BS();
BinaryPrimitives.WriteUInt32LittleEndian(unsafeSave.Underground.Data.Slice(4, 4), 2);
var coreItems = unsafeSave.Underground.ReadItems();
Must(!coreItems[0].HideNewFlag, "Core noncanonical flag semantics changed.");
unsafeSave.Underground.WriteItems(coreItems);
Must(BinaryPrimitives.ReadUInt32LittleEndian(unsafeSave.Underground.Data.Slice(4, 4)) == 0,
    "Core untouched-flag rewrite defect no longer reproduces.");

var sample = new SaveReadOnlyResult("SAV8BS", "BD", 8, "TEST", 0, 0,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint)
{
    Underground = view,
};
var display = ReadOnlyProjectionPresenter.FormatTrainer(sample, false);
Must(display.Contains("999 entries") && display.Contains("raw 32-bit flags") &&
    display.Contains("No editing, export"),
    "Android text omitted Underground diagnostics or export prohibition.");
Console.WriteLine("Gen4 13 scores/200 pouch bytes and BDSP 999x12 raw Underground tested with synthetic pinned Core; no desktop/device certification.");
