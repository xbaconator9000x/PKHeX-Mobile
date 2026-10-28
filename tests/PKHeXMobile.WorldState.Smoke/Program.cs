using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}

static void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected a fail-closed exception.");
}

// Pinned Core's Gen3 roamer is backed by mutable Memory<byte>.
// The read-only projection must detach it, and never use IVsGlitch on live data.
var raw = new byte[Roamer3.SIZE];
var roamer = new Roamer3(raw, true);
roamer.IV32 = 0x12345678;
roamer.PID = 0x89ABCDEF;
roamer.Species = 25;
roamer.HP_Current = 117;
roamer.CurrentLevel = 40;
roamer.Status = 0x05;
roamer.IsActive = true;
var original = raw.ToArray();
var view = WorldStateReadOnly.ProjectRoamer(roamer);
Must(raw.SequenceEqual(original) && view.PID == 0x89ABCDEF &&
    view.Species == 25 && view.CurrentHp == 117 && view.Level == 40 &&
    view.Status == 5 && view.IsActive && view.IsGlitched &&
    view.RawIv32 == 0x12345678, "Gen3 roamer raw projection changed source or lost fields.");
Must(view.IVs.SequenceEqual(roamer.IVs) &&
    view.EncounterGlitchIVs.SequenceEqual(roamer.IVsGlitch) &&
    raw.SequenceEqual(original), "Pure glitch-IV calculation disagrees with pinned Core.");
if (view.IVs is IList<int> mutable)
    Reject<NotSupportedException>(() => mutable[0] = 99);
if (view.EncounterGlitchIVs is IList<int> mutableGlitch)
    Reject<NotSupportedException>(() => mutableGlitch[0] = 99);
roamer.IV32 ^= 0xFF;
Must(view.RawIv32 == 0x12345678 &&
    WorldStateReadOnly.ProjectRoamer(roamer).RawIv32 != view.RawIv32,
    "Gen3 roamer snapshot retained mutable source memory.");
Reject<InvalidDataException>(() =>
    WorldStateReadOnly.ProjectRoamer(new Roamer3(new byte[Roamer3.SIZE - 1], false)));

// Runtime reproduction of pinned Core's negative-IV adjacent-bit contamination.
// This intentionally mutates only a synthetic test record; production has no IV writer.
var unsafeRoamer = new Roamer3(new byte[Roamer3.SIZE], false);
unsafeRoamer.IV_ATK = -1;
Must((unsafeRoamer.IV32 & ~(31u << 5)) != 0,
    "Pinned Core negative-IV contamination no longer reproduces; revisit mitigation.");

// Official Core's SAV4Sinnoh getter accepts tree index 21, outside 0–20.
// Our projection rejects it before Core. Synthetic General-buffer sentinel
// proves the extra index maps to a separate eight-byte region.
var dp = new SAV4DP();
var tree0 = dp.GetHoneyTree(0);
tree0.Data[5] = 0xFE; // intentionally noncanonical subtable
tree0.Data[6] = 2;
dp.SetHoneyTree(tree0, 0); // synthetic fixture setup only
var before = dp.General.ToArray();
var world = WorldStateReadOnly.Project(dp);
Must(world is { Roamer: null } && world.HoneyTrees.Count == 21 &&
    world.HoneyTrees[0].SubTable == 0xFE &&
    world.HoneyTrees[0].Group == 2 &&
    dp.General.SequenceEqual(before),
    "Sinnoh read changed raw subtable or omitted a tree.");
Must(world.HoneyTrees.Select(t => t.Index).SequenceEqual(Enumerable.Range(0, 21)),
    "Honey tree indices were reordered.");
Reject<ArgumentOutOfRangeException>(() => WorldStateReadOnly.ProjectHoneyTree(dp, -1));
Reject<ArgumentOutOfRangeException>(() => WorldStateReadOnly.ProjectHoneyTree(dp, 21));
var index21 = dp.GetHoneyTree(21); // source defect reproduction ONLY
var tree20Before = dp.GetHoneyTree(20).Data.ToArray();
index21.Data[0] ^= 0xFF;
dp.SetHoneyTree(index21, 21); // synthetic adjacent-data sentinel
Must(!dp.General.SequenceEqual(before) &&
    dp.GetHoneyTree(20).Data.SequenceEqual(tree20Before) &&
    WorldStateReadOnly.Project(dp)!.HoneyTrees[0].RawSha256 == world.HoneyTrees[0].RawSha256,
    "Pinned Core index-21 adjacent-data behavior did not reproduce.");

// Core Group setter normalizes SubTable even when Group is unchanged.
// Our projection must preserve noncanonical values and never call setters.
var normalize = new HoneyTreeValue(new byte[] { 0, 0, 0, 0, 0, 0xFE, 2, 0 });
normalize.Group = normalize.Group;
Must(normalize.SubTable != 0xFE,
    "Pinned Core no-op Group normalization no longer reproduces.");
Must(world.HoneyTrees[0].SubTable == 0xFE,
    "Immutable projection changed after unrelated Core mutation.");

var emerald = new SAV3E();
var emeraldBefore = emerald.Large.ToArray();
var emeraldWorld = WorldStateReadOnly.Project(emerald);
Must(emeraldWorld is { Roamer: not null } && emeraldWorld.HoneyTrees.Count == 0 &&
    emerald.Large.SequenceEqual(emeraldBefore),
    "SAV3E roamer projection changed Core memory or lost its record.");

var sample = new SaveReadOnlyResult("SAV4DP", "D", 4, "TEST", 0, 0,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint)
{
    WorldState = world,
};
var display = ReadOnlyProjectionPresenter.FormatTrainer(sample, false);
Must(display.Contains("21 entries") &&
    display.Contains("no edit authorization") &&
    display.Contains("No editing, export"),
    "World-state presentation implied write authorization or omitted tree count.");
Console.WriteLine("Pinned-Core Gen3 roamer and Gen4 honey-tree defects reproduced on synthetic memory; immutable read-only projections and index guards passed. No desktop/device certification.");
