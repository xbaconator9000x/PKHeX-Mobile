using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static ExpectedDaycareReadOnly Expect(DaycareReadOnly actual) =>
    new(actual.Available, actual.Facilities.Select(f =>
        new ExpectedDaycareFacilityReadOnly(f.FacilityIndex, f.EggAvailable, f.SeedHex,
            f.Slots.Select(s => new ExpectedDaycareSlotReadOnly(s.SlotIndex, s.Occupied,
                s.RawLength, s.RawSha256, s.ExperienceGained)).ToArray())).ToArray())
    {
        Gen2Egg = actual.Gen2Egg is { } egg
            ? new ExpectedDaycareEggReadOnly(egg.RawLength, egg.RawSha256)
            : null,
    };

static IReadOnlyList<string> Compare(DaycareReadOnly? actual, ExpectedDaycareReadOnly? expected,
    bool requireGen2Egg = false)
{
    var errors = new List<string>();
    DaycareProjectionVerifier.Compare(actual, expected, errors, requireGen2Egg);
    return errors;
}

static void MustNotMutate<T>(IReadOnlyList<T> source, T candidate)
{
    if (source is not IList<T> list) return;
    try { list.Add(candidate); }
    catch (NotSupportedException) { return; }
    throw new Exception("A mutable daycare collection escaped.");
}

// Genuine pinned-Core Gen7 UInt128 seed and stored parent bytes.
var usum = new SAV7USUM();
usum.Daycare.GetDaycareSlot(0).Span[0] = 0xA5;
usum.Daycare.SetDaycareOccupied(0, true); // fixture setup only
usum.Daycare.Seed = (UInt128.One << 120) | 0x42;
usum.Daycare.IsEggAvailable = true;
var before = usum.Data.ToArray();
var view = DaycareReadOnly.Project(usum);
Must(view.Available && view.Facilities.Count == 1, "Gen7 facility missing.");
var facility = view.Facilities[0];
Must(facility.SeedHex?.Length == 32 && facility.SeedHex.StartsWith("0100") &&
    facility.EggAvailable == true && facility.Slots[0].Occupied &&
    !facility.Slots[1].Occupied, "Gen7 high seed bits, egg or occupancy lost.");
Must(facility.Slots[0].RawLength == usum.Daycare.GetDaycareSlot(0).Length,
    "Gen7 bounded parent length wrong.");
Must(usum.Data.SequenceEqual(before), "Read-only Gen7 projection mutated Core.");
MustNotMutate(view.Facilities, facility);
MustNotMutate(facility.Slots, facility.Slots[0]);
usum.Daycare.GetDaycareSlot(0).Span[0] ^= 0xFF;
Must(view.Facilities[0].Slots[0].RawSha256 == facility.Slots[0].RawSha256,
    "Detached snapshot changed with live Core memory.");
Must(DaycareReadOnly.Project(usum).Facilities[0].Slots[0].RawSha256 !=
    facility.Slots[0].RawSha256, "Parent digest did not track changed fixture bytes.");

// Gen1 has one daycare parent, not two. Gen2 has two parents and a
// separate egg; this projection does not pretend the egg is a third parent.
var red = new SAV1(LanguageID.English, GameVersion.RB);
var redBefore = red.Data.ToArray();
var redView = DaycareReadOnly.Project(red);
Must(redView.Available && redView.Facilities.Count == 1 &&
    redView.Facilities[0].Slots.Count == 1 &&
    red.Data.SequenceEqual(redBefore), "Gen1 single-slot daycare failed.");
var crystal = new SAV2();
crystal.GetDaycareEgg().Span[0] = 0x5A; // synthetic Core-only egg buffer
crystal.IsEggAvailable = true;
var crystalBefore = crystal.Data.ToArray();
var crystalView = DaycareReadOnly.Project(crystal);
Must(crystalView.Facilities.Count == 1 &&
    crystalView.Facilities[0].Slots.Count == 2 &&
    crystalView.Facilities[0].EggAvailable == true &&
    crystalView.Gen2Egg?.RawLength == crystal.SIZE_STORED &&
    crystal.Data.SequenceEqual(crystalBefore), "Gen2 daycare parents or separate egg lost.");
var eggHash = crystalView.Gen2Egg!.RawSha256;
crystal.GetDaycareEgg().Span[0] ^= 0xFF;
Must(crystalView.Gen2Egg.RawSha256 == eggHash &&
    DaycareReadOnly.Project(crystal).Gen2Egg!.RawSha256 != eggHash,
    "Gen2 egg fingerprint was not detached from mutable Core memory.");
var crystalExpected = Expect(crystalView);
Must(Compare(crystalView, crystalExpected, true).Count == 0,
    "Matching Gen2 egg v12 parity must pass.");
Must(Compare(crystalView, crystalExpected with { Gen2Egg = null }, true)
    .Any(e => e.Contains("Gen2Egg missing")), "v12 accepted omitted Gen2 egg.");
Must(Compare(crystalView, crystalExpected with { Gen2Egg = null }).Count == 0,
    "v11 backward compatibility unexpectedly requires Gen2 egg.");
Must(Compare(crystalView, crystalExpected with
{
    Gen2Egg = crystalExpected.Gen2Egg! with { RawSha256 = new string('0', 64) },
}, true).Any(e => e.Contains("Gen2Egg.RawSha256")), "Gen2 egg mismatch not detected.");
Must(Compare(crystalView, crystalExpected with
{
    Gen2Egg = crystalExpected.Gen2Egg! with { RawLength = 513 },
}, true).Any(e => e.Contains("invalid length")), "Oversized Gen2 egg expectation accepted.");
Must(Compare(crystalView, crystalExpected with
{
    Gen2Egg = crystalExpected.Gen2Egg! with { RawSha256 = "not-hex" },
}, true).Any(e => e.Contains("invalid length/digest")), "Malformed Gen2 egg hash accepted.");

// Gen4's Core seed uses 32 bits, unlike Gen5/6's 64 and Gen7's 128.
var diamond = new SAV4DP();
((IDaycareRandomState<uint>)diamond).Seed = 0xF1234567;
var diamondBefore = diamond.Data.ToArray();
var diamondView = DaycareReadOnly.Project(diamond);
Must(diamondView.Facilities[0].SeedHex == "F1234567" &&
    diamond.Data.SequenceEqual(diamondBefore), "Gen4 UInt32 daycare seed lost.");

// ORAS exposes two separate facilities; never use the unbounded indexer fallback.
var oras = new SAV6AO();
var multi = (IDaycareMulti)oras;
multi[1].GetDaycareSlot(1).Span[0] = 0x5C;
multi[1].SetDaycareOccupied(1, true);
var orasBytes = oras.Data.ToArray();
var orasView = DaycareReadOnly.Project(oras);
Must(orasView.Facilities.Count == 2 &&
    !orasView.Facilities[0].Slots[1].Occupied &&
    orasView.Facilities[1].Slots[1].Occupied &&
    orasView.Facilities[0].Slots[1].RawSha256 !=
    orasView.Facilities[1].Slots[1].RawSha256, "ORAS couples were conflated.");
Must(oras.Data.SequenceEqual(orasBytes), "ORAS projection mutated save.");

// BW exposes a no-op seed setter: do not imply a persisted seed.
var bw = new SAV5BW();
var bwView = DaycareReadOnly.Project(bw);
Must(bwView.Facilities.Count == 1 && bwView.Facilities[0].SeedHex is null &&
    bwView.Facilities[0].Slots.All(s => s.ExperienceGained is not null),
    "BW incorrectly reports a seed or omits deposited EXP.");
var b2w2 = new SAV5B2W2();
Must(DaycareReadOnly.Project(b2w2).Facilities[0].SeedHex?.Length == 16,
    "B2W2 persisted 64-bit seed not reported.");

// BDSP must never clear a parent on a read. Use only read-only Core getters.
var bdsp = new SAV8BS();
bdsp.Daycare.Seed = 0xFEDCBA9876543210;
bdsp.Daycare.IsEggAvailable = true;
var bdspBefore = bdsp.Data.ToArray();
var bdspView = DaycareReadOnly.Project(bdsp);
Must(bdspView.Facilities.Count == 1 &&
    bdspView.Facilities[0].SeedHex == "FEDCBA9876543210" &&
    bdspView.Facilities[0].EggAvailable == true &&
    bdspView.Facilities[0].Slots.All(s => s.RawLength == bdsp.SIZE_PARTY),
    "BDSP daycare seed, egg or bounded parent length wrong.");
Must(bdsp.Data.SequenceEqual(bdspBefore), "BDSP projection cleared parent bytes.");

// Sword/Shield: Core's indexer exposes the entire remaining tail, but our
// digest must include only one stored PK8 per slot across two independent
// nurseries. Seed/occupancy are read-only, and all four parent bytes differ.
var swsh = new SAV8SWSH();
var swshDaycare = swsh.Daycare;
for (int index = 0; index < 4; index++)
{
    int facilityIndex = index / 2;
    int slotIndex = index % 2;
    swshDaycare[index].Span[0] = (byte)(0x31 + index);
    int occupiedOffset = facilityIndex == 0
        ? Daycare8.GetDaycare1StructOffset(slotIndex)
        : Daycare8.GetDaycare2StructOffset(slotIndex);
    swshDaycare.Data[occupiedOffset] = (byte)(index % 2 == 0 ? 1 : 0);
}
swshDaycare.SetDaycareSeed(0, 0x0123456789ABCDEF);
swshDaycare.SetDaycareSeed(1, 0xFEDCBA9876543210);
var swshBefore = swshDaycare.Data.ToArray();
var swshView = DaycareReadOnly.Project(swsh);
Must(swshView.Available && swshView.Facilities.Count == 2 &&
    swshView.Facilities[0].SeedHex == "0123456789ABCDEF" &&
    swshView.Facilities[1].SeedHex == "FEDCBA9876543210" &&
    swshView.Facilities.All(f => f.Slots.Count == 2) &&
    swshView.Facilities.All(f => f.Slots[0].Occupied && !f.Slots[1].Occupied) &&
    swshView.Facilities.SelectMany(f => f.Slots).All(s => s.RawLength == swsh.SIZE_STORED) &&
    swshView.Facilities.SelectMany(f => f.Slots).Select(s => s.RawSha256).Distinct().Count() == 4 &&
    swshDaycare.Data.SequenceEqual(swshBefore),
    "SWSH two-nursery bounded metadata, distinct parents or read immutability failed.");
Must(swshDaycare[0].Length > swsh.SIZE_STORED,
    "SWSH Core tail-slice regression was not exercised.");
var firstParentHash = swshView.Facilities[0].Slots[0].RawSha256;
// The byte immediately after PK8 #0 is the occupancy flag of slot #1.
// Changing that tail byte must not change the bounded parent #0 digest.
swshDaycare[0].Span[swsh.SIZE_STORED] ^= 0x7F;
Must(DaycareReadOnly.Project(swsh).Facilities[0].Slots[0].RawSha256 == firstParentHash,
    "SWSH parent hash included bytes outside the stored PK8 boundary.");
swshDaycare[0].Span[0] ^= 0xFF;
Must(swshView.Facilities[0].Slots[0].RawSha256 == firstParentHash &&
    DaycareReadOnly.Project(swsh).Facilities[0].Slots[0].RawSha256 != firstParentHash,
    "SWSH snapshot leaked mutable Core storage.");
Must(Compare(swshView, Expect(swshView)).Count == 0,
    "SWSH two-facility schema v11 parity failed.");

// Strict v11 comparison and malformed-oracle rejection.
var expected = Expect(view);
Must(Compare(view, expected).Count == 0, "Identical daycare projection must match.");
Must(Compare(view, expected with { Available = false }).Count > 0,
    "Conflicting availability falsely passed.");
var wrongFacility = expected.Facilities[0] with { FacilityIndex = 1 };
Must(Compare(view, expected with { Facilities = [wrongFacility] }).Count > 0,
    "Reordered facility index falsely passed.");
var wrongSlot = expected.Facilities[0].Slots[0] with { RawSha256 = new string('0', 64) };
var wrongSlots = expected.Facilities[0].Slots.ToArray();
wrongSlots[0] = wrongSlot;
Must(Compare(view, expected with
{
    Facilities = [expected.Facilities[0] with { Slots = wrongSlots }],
}).Any(x => x.Contains("RawSha256")), "Parent hash mismatch went undetected.");
Must(Compare(view, expected with
{
    Facilities = [expected.Facilities[0] with { SeedHex = "invalid" }],
}).Count > 0, "Malformed seed falsely passed.");
Must(Compare(view, expected with
{
    Facilities = [expected.Facilities[0] with { Slots = [] }],
}).Count > 0, "Missing slots falsely passed.");
Must(Compare(view, null).Count > 0 && Compare(null, expected).Count > 0,
    "Missing daycare projection falsely passed.");

var noDaycare = new DaycareReadOnly(false, Array.Empty<DaycareFacilityReadOnly>());
Must(Compare(noDaycare, Expect(noDaycare)).Count == 0,
    "Explicitly unsupported daycare must compare as unsupported.");
Must(Compare(noDaycare, expected).Count > 0,
    "Unsupported daycare falsely matched a supported expectation.");

var sample = new SaveReadOnlyResult("SAV7USUM", "USUM", 7, "TEST", 1, 2,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint)
{
    Daycare = view,
};
var presentation = ReadOnlyProjectionPresenter.FormatTrainer(sample, false);
Must(presentation.Contains("Daycare (Core raw metadata)") &&
    presentation.Contains("Facility 1") &&
    presentation.Contains("occupied") &&
    presentation.Contains("No editing, export"),
    "Android-independent daycare presentation missing or misleading.");
// Malicious/unreviewed Core adapters cannot expose unbounded writable tails.
static void MustReject(Action action)
{
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new Exception("Unreviewed nursery layout was accepted.");
}
MustReject(() => DaycareReadOnly.ProjectStorage(new FakeDaycare(2, 513), 0));
MustReject(() => DaycareReadOnly.ProjectStorage(new FakeDaycare(3, 10), 0));
MustReject(() => DaycareReadOnly.ProjectStorage(new FakeDaycare(1, 10), -1));
MustReject(() => DaycareReadOnly.ProjectStorage(new FakeDaycare(1, 10), 2));
Console.WriteLine("Pinned-Core Gen1–8 nursery metadata including Gen2 egg and SWSH dual nursery, bounded detached hashes, strict schemas v11/v12, and read-only presentation tests passed; no desktop or device certification.");

internal sealed class FakeDaycare(int count, int length) : IDaycareStorage
{
    public int DaycareSlotCount => count;
    public Memory<byte> GetDaycareSlot(int index) => new byte[length];
    public bool IsDaycareOccupied(int index) => false;
    public void SetDaycareOccupied(int index, bool occupied) =>
        throw new Exception("Read-only projection invoked a mutation setter.");
}

