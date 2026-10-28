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
                s.RawLength, s.RawSha256, s.ExperienceGained)).ToArray())).ToArray());

static IReadOnlyList<string> Compare(DaycareReadOnly? actual, ExpectedDaycareReadOnly? expected)
{
    var errors = new List<string>();
    DaycareProjectionVerifier.Compare(actual, expected, errors);
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
    bdspView.Facilities[0].Slots.All(s => s.RawLength == PokeCrypto.SIZE_8PARTY),
    "BDSP daycare seed, egg or bounded parent length wrong.");
Must(bdsp.Data.SequenceEqual(bdspBefore), "BDSP projection cleared parent bytes.");

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
Console.WriteLine("Pinned-Core Gen5/6/7/BDSP nursery metadata, detached hashes, UInt128 seed, strict schema v11, and read-only presentation tests passed; no desktop or device certification.");
