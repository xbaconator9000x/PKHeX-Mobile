using System.Buffers.Binary;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;
using PKHeX.Core;
using PKHeXMobile.ReadOnly.Smoke;

static void MustReject(byte[] bytes)
{
    var snapshot = SaveInputSnapshot.FromBytes(bytes);
    if (ReadOnlySaveInspector.TryInspect(snapshot, out var result, out var reason))
        throw new Exception($"Unexpectedly accepted synthetic garbage: {result?.CoreSaveType}");
    if (result is not null || string.IsNullOrWhiteSpace(reason))
        throw new Exception("Rejection must not return a save or a blank reason.");
}

static void RequireRisk(byte[] bytes, SaveInputRiskCode code)
{
    var risk = SaveInputRiskAnalyzer.Assess(bytes);
    if (risk?.Code != code)
        throw new Exception($"Expected {code}, got {risk?.Code}.");
    var snapshot = SaveInputSnapshot.FromBytes(bytes);
    if (ReadOnlySaveInspector.TryInspect(snapshot, out var result, out var reason) ||
        result is not null || string.IsNullOrWhiteSpace(reason))
        throw new Exception($"Risky input {code} must be rejected before Core.");
}

static byte[] StampedGen3(uint signature, int count)
{
    var data = new byte[0x20000];
    for (int i = 0; i < count; i++)
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i * 0x1000 + 0xFF8, 4), signature);
    return data;
}

static byte[] CfruWindowFixture()
{
    // Fully synthetic, no proprietary game data. Each of 14 fragments uses the
    // documented CFRU window, but fragment 1 differs from retail FRLG checksum.
    ushort[] cfru = [0xF24, 0xFF0, 0xFF0, 0xFF0, 0xD98,
        0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0x450];
    var bytes = new byte[0x20000];
    for (int id = 0; id < 14; id++)
    {
        int off = id * 0x1000;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(off + 0xFF4, 2), (ushort)id);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(off + 0xFF8, 4), 0x08012025);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(off + 0xFFC, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(off, 4), 1);
        if (id == 1)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(off + 0xF84, 4), 2);
        uint sum = 0;
        for (int i = 0; i < cfru[id]; i += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(off + i, 4)));
        ushort checksum = unchecked((ushort)(sum + (sum >> 16)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(off + 0xFF6, 2), checksum);
    }
    return bytes;
}

static async Task MustThrowAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

MustReject(new byte[512]);
MustReject(new byte[0x20000]); // A Gen 3-sized all-zero buffer must not count as a valid save.
MustReject(new byte[0x8000]);  // A Gen 1-sized all-zero buffer must not count as a valid save.
RequireRisk([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00], SaveInputRiskCode.CompressedContainer);
RequireRisk([0x1F, 0x8B, 0x08, 0x00], SaveInputRiskCode.CompressedContainer);
// C-08: all RetroArch #RZIP prefixes, including truncated/invalid headers,
// are refused before any retail Core parsing. These are not decoder tests.
RequireRisk([0x23, 0x52, 0x5A, 0x49, 0x50], SaveInputRiskCode.RetroArchRzipContainer);
var rzipHeader = new byte[20];
new byte[] { 0x23, 0x52, 0x5A, 0x49, 0x50, 0x76, 0x01, 0x23 }.CopyTo(rzipHeader, 0);
BinaryPrimitives.WriteUInt32LittleEndian(rzipHeader.AsSpan(8, 4), 131072);
BinaryPrimitives.WriteUInt64LittleEndian(rzipHeader.AsSpan(12, 8), 512);
RequireRisk(rzipHeader, SaveInputRiskCode.RetroArchRzipContainer);
rzipHeader[6] = 0x02; // unsupported version still refused
RequireRisk(rzipHeader, SaveInputRiskCode.RetroArchRzipContainer);
if (SaveInputRiskAnalyzer.Assess([0x23, 0x52, 0x5A, 0x49]) is not null)
    throw new Exception("Partial non-RZIP prefix was incorrectly classified.");

RequireRisk(StampedGen3(0x01121999, 8), SaveInputRiskCode.PossibleUnbound);
RequireRisk(StampedGen3(0x66290096, 8), SaveInputRiskCode.PossibleGsChronicles);
var conflict = StampedGen3(0x01121999, 8);
for (int i = 8; i < 16; i++)
    BinaryPrimitives.WriteUInt32LittleEndian(conflict.AsSpan(i * 0x1000 + 0xFF8, 4), 0x66290096);
RequireRisk(conflict, SaveInputRiskCode.ConflictingGen3Signatures);
var lumi = new byte[0xEDC20];
BinaryPrimitives.WriteUInt32LittleEndian(lumi, 0xFFFF0001);
RequireRisk(lumi, SaveInputRiskCode.PossibleLuminescentPlatinum);
var lumiNewerSize = new byte[0xEF0A4];
BinaryPrimitives.WriteUInt32LittleEndian(lumiNewerSize, 0xFFFF0012);
RequireRisk(lumiNewerSize, SaveInputRiskCode.PossibleLuminescentPlatinum);
var cfru = CfruWindowFixture();
RequireRisk(cfru, SaveInputRiskCode.PossibleCfruSectorLayout);

// Threshold, checksum and shape negatives: these must NOT be classified as known
// hack markers. This does not assert they are valid retail saves or safe to write.
if (SaveInputRiskAnalyzer.Assess(StampedGen3(0x01121999, 7)) is not null ||
    SaveInputRiskAnalyzer.Assess(StampedGen3(0x66290096, 7)) is not null ||
    SaveInputRiskAnalyzer.Assess(StampedGen3(0x08012025, 28)) is not null)
    throw new Exception("Risk analyzer produced a known-hack false positive.");
cfru[0x1000 + 0xFF6] ^= 0x40; // checksum invalidates the CFRU structural match
if (SaveInputRiskAnalyzer.Assess(cfru) is not null)
    throw new Exception("Invalid CFRU checksum must not be accepted as a structural match.");
var notLumi = new byte[0xEDC20];
BinaryPrimitives.WriteUInt32LittleEndian(notLumi, 0xFFFE0001);
if (SaveInputRiskAnalyzer.Assess(notLumi) is not null)
    throw new Exception("Non-Luminescent revision marker was flagged.");

var snapshot = SaveInputSnapshot.FromBytes(new byte[512]);
if (ReadOnlySaveInspector.TryInspect(snapshot, out var accepted, out _))
    throw new Exception("Unexpected positive detection.");
if (accepted is not null)
    throw new Exception("A rejected save must have no projection.");

// Synthetic in-memory Gen 1 fixture generated by the same pinned Core, not an independent
// real-world or desktop parity fixture. No file is written, and the reader never serializes.
var synthetic = new SAV1(LanguageID.English, GameVersion.RB)
{
    OT = "TEST",
    TID16 = 1234,
};
// A freshly constructed empty SAV1 does not initialize empty party/current-box
// list sentinels. The pinned Core's Gen 1 detector requires both valid list headers.
synthetic.Data[0x2F2D] = 0xFF; // INT Gen 1 party: count 0, end marker.
synthetic.Data[0x30C1] = 0xFF; // INT Gen 1 current box: count 0, end marker.
var syntheticBytes = synthetic.Write().ToArray();
var syntheticSnapshot = SaveInputSnapshot.FromBytes(syntheticBytes);
if (!ReadOnlySaveInspector.TryInspect(syntheticSnapshot, out var view, out var failure) || view is null)
    throw new Exception($"Core-generated Gen 1 save was not inspected: {failure}");
if (view.CoreSaveType != nameof(SAV1) || view.Generation != 1 || view.TrainerId != 1234 ||
    view.TrainerName != "TEST" || view.Compatibility != SaveCompatibility.SupportedReadOnly ||
    view.VerifiedProfileId is not null || view.BoxCount != 12 || view.Boxes.Count != 12 ||
    view.OriginalFingerprint != syntheticSnapshot.Fingerprint)
    throw new Exception("Core-generated Gen 1 trainer/box projection differs from the fixture.");
if (!syntheticSnapshot.Matches(syntheticBytes))
    throw new Exception("Inspection modified its source snapshot.");

// Project actual pinned-Core PKM fields into immutable scalars without mutating
// the source. This is not an independent desktop parity or legal-Pokémon fixture.
var syntheticPokemon = new PK1 { Species = 25, Move1 = 33, Move2 = 45, EV_HP = 123 };
var beforeProjection = syntheticPokemon.Data.ToArray();
var projectedPokemon = ReadOnlySaveInspector.ProjectPokemon(2, syntheticPokemon);
if (projectedPokemon.SlotIndex != 2 || projectedPokemon.Species != 25 ||
    projectedPokemon.Details is not { } coreDetails ||
    coreDetails.Moves.First != 33 || coreDetails.Moves.Second != 45 ||
    coreDetails.EffortValues.HP != 123 ||
    !syntheticPokemon.Data.SequenceEqual(beforeProjection))
    throw new Exception("Pinned-Core scalar projection lost data or modified the PKM.");
try
{
    ReadOnlySaveInspector.ProjectPokemon(-1, syntheticPokemon);
    throw new Exception("Negative source slot index must be rejected.");
}
catch (ArgumentOutOfRangeException) { }

// H-01/HG-01: synthetic ZA gift cloning must be rejected if pinned Core
// changes the card type/context. This is a source-matched package diagnostic,
// not gift import, save editing or a real event distribution fixture.
var zaGift = new WA9 { CardID = 17, CardType = WA9.GiftType.Item };
var zaOriginalBytes = zaGift.Data.ToArray();
var nativeZaClone = zaGift.Clone();
var nativePreservesIdentity = nativeZaClone.GetType() == zaGift.GetType() &&
    nativeZaClone.Context == zaGift.Context &&
    nativeZaClone.Data.SequenceEqual(zaGift.Data);
var guardedZaClone = MysteryGiftCloneIdentity.TryClonePreservingIdentity(zaGift, out var safeZaClone);
if (guardedZaClone != nativePreservesIdentity ||
    (guardedZaClone && (safeZaClone is null || safeZaClone.Context != EntityContext.Gen9a)) ||
    (!guardedZaClone && safeZaClone is not null) ||
    !zaGift.Data.SequenceEqual(zaOriginalBytes))
    throw new Exception("ZA gift clone identity guard failed or modified its source.");
var svGift = new WC9 { CardID = 18 };
if (!MysteryGiftCloneIdentity.TryClonePreservingIdentity(svGift, out var safeSvClone) ||
    safeSvClone is not WC9 || safeSvClone.Context != EntityContext.Gen9)
    throw new Exception("Identity-preserving Gen9 gift clone was refused.");
Console.WriteLine(nativePreservesIdentity
    ? "Pinned-Core WA9 clone preserves identity in this binary."
    : "Pinned-Core WA9 clone loses type/context; guarded clone path refuses it.");

// Provider-neutral end-to-end path: exactly two independent read opens, same bytes.
int opens = 0;
var inspected = await ReadOnlyDocumentService.InspectAsync(_ =>
{
    opens++;
    return ValueTask.FromResult<Stream>(new MemoryStream(syntheticBytes, writable: false));
});
if (!inspected.IsRecognized || inspected.Result?.TrainerId != 1234 ||
    inspected.RejectionReason is not null || inspected.Fingerprint != syntheticSnapshot.Fingerprint ||
    opens != 2)
    throw new Exception("Document service failed its two-open read-only positive case.");

if (inspected.SourceIdentity is not null || inspected.SourceIdentityConfirmed)
    throw new Exception("Stream-only provider must not claim verified document identity.");

// S-01/S-04: a content provider can supply streams without filesystem paths.
// Same bytes under a different provider/document ID must be refused.
var stableIdentity = new DocumentSourceIdentity("org.example.documents", "opaque:42");
int identityOpens = 0;
var identityInspected = await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
{
    identityOpens++;
    return ValueTask.FromResult(new DocumentReadHandle(
        new MemoryStream(syntheticBytes, writable: false),
        new DocumentSourceIdentity("org.example.documents", "opaque:42")));
});
if (!identityInspected.IsRecognized || identityInspected.Result?.TrainerId != 1234 ||
    !identityInspected.SourceIdentityConfirmed ||
    identityInspected.SourceIdentity != stableIdentity || identityOpens != 2)
    throw new Exception("Identity-aware read failed its two-open positive case.");

int replacementOpens = 0;
MemoryStream? replacementStream = null;
await MustThrowAsync<InvalidOperationException>(async () =>
{
    await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        replacementOpens++;
        replacementStream = new MemoryStream(syntheticBytes, writable: false);
        return ValueTask.FromResult(new DocumentReadHandle(replacementStream,
            new DocumentSourceIdentity("org.example.documents",
                replacementOpens == 1 ? "opaque:42" : "opaque:99")));
    });
});
if (replacementOpens != 2 || replacementStream?.CanRead != false)
    throw new Exception("Same-byte document replacement must fail and dispose its stream.");

// Missing identity cannot be silently promoted to a verified one.
int ambiguousOpens = 0;
await MustThrowAsync<InvalidOperationException>(async () =>
{
    await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        ambiguousOpens++;
        return ValueTask.FromResult(new DocumentReadHandle(
            new MemoryStream(syntheticBytes, writable: false),
            ambiguousOpens == 1 ? null : stableIdentity));
    });
});
if (ambiguousOpens != 2)
    throw new Exception("Ambiguous provider identity did not reach the second open.");

var unknownIdentity = await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    ValueTask.FromResult(new DocumentReadHandle(
        new MemoryStream(syntheticBytes, writable: false), null)));
if (!unknownIdentity.IsRecognized || unknownIdentity.SourceIdentityConfirmed)
    throw new Exception("An unverified provider identity must remain unverified.");

await MustThrowAsync<InvalidDataException>(async () =>
{
    await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
        ValueTask.FromResult(new DocumentReadHandle(null!, stableIdentity)));
});


var refused = await ReadOnlyDocumentService.InspectAsync(_ =>
    ValueTask.FromResult<Stream>(new MemoryStream(new byte[512], writable: false)));
if (refused.IsRecognized || refused.Result is not null || string.IsNullOrWhiteSpace(refused.RejectionReason))
    throw new Exception("Document service must preserve parser refusal.");

var changed = syntheticBytes.ToArray();
int staleOpens = 0;
await MustThrowAsync<InvalidOperationException>(async () =>
{
    await ReadOnlyDocumentService.InspectAsync(_ =>
    {
        staleOpens++;
        if (staleOpens == 2)
            changed[10] ^= 0x40;
        return ValueTask.FromResult<Stream>(new MemoryStream(changed, writable: false));
    });
});
if (staleOpens != 2)
    throw new Exception("Stale document must be detected on the second provider open.");

await MustThrowAsync<IOException>(async () =>
{
    await ReadOnlyDocumentService.InspectAsync(_ => throw new IOException("Permission revoked"));
});
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    await MustThrowAsync<OperationCanceledException>(async () =>
    {
        await ReadOnlyDocumentService.InspectAsync(_ =>
            ValueTask.FromResult<Stream>(new MemoryStream(syntheticBytes)), cancellation.Token);
    });
}

try
{
    SaveWriteGuard.RequireAuthorized(
        new SaveInspection(view.CoreSaveType, view.Compatibility, view.VerifiedProfileId),
        syntheticBytes.Length, syntheticBytes.Length);
    throw new Exception("Read-only inspection must never authorize writes.");
}
catch (InvalidOperationException) { }

// A manifest verifier must compare every field and every occupied slot; this
// same-Core synthetic expectation is NOT an independently authored baseline.
var expectedParty = view.Party.Select(p =>
    new ExpectedPokemonSlot(p.SlotIndex, p.Species, p.Nickname, p.Level, p.IsEgg, p.IsShiny)).ToArray();
var expectedBoxes = view.Boxes.Select(b => new ExpectedBoxSnapshot(b.BoxIndex,
    b.OccupiedSlots.Select(p =>
        new ExpectedPokemonSlot(p.SlotIndex, p.Species, p.Nickname, p.Level, p.IsEgg, p.IsShiny)).ToArray())).ToArray();
var expectation = new ExpectedSaveProjection(
    view.CoreSaveType, view.GameVersion, view.Generation, view.TrainerName,
    view.TrainerId, view.SecretId, view.PartyCount, view.BoxCount,
    view.BoxSlotCount, expectedParty, expectedBoxes);
if (SaveProjectionVerifier.Compare(view, expectation).Count != 0)
    throw new Exception("Identical expected projection should match.");
if (!SaveProjectionVerifier.Compare(view, expectation with { TrainerId = 4321 }).Contains("TrainerId mismatch"))
    throw new Exception("Verifier missed a trainer mismatch.");
if (!SaveProjectionVerifier.Compare(view, expectation with { Boxes = [] }).Contains("Boxes.Count mismatch"))
    throw new Exception("Verifier missed a box-count mismatch.");
var fakeSlot = new PokemonSlot(0, 25, "TEST", 5, false, false);
var occupied = view with { Party = [fakeSlot] };
if (!SaveProjectionVerifier.Compare(occupied, expectation).Contains("Party.Count mismatch"))
    throw new Exception("Verifier missed an occupied-slot mismatch.");
if (!SaveProjectionVerifier.Compare(occupied, expectation with { Party = [new ExpectedPokemonSlot(0, 26, "TEST", 5, false, false)] })
    .Contains("Party[0].Species mismatch"))
    throw new Exception("Verifier missed a species mismatch.");

// Missing expected JSON fields must be rejected even when a default value would
// otherwise happen to match an empty save. This prevents false parity passes.
try
{
    System.Text.Json.JsonSerializer.Deserialize<ExpectedSaveProjection>("{}");
    throw new Exception("Incomplete expected projection JSON was accepted.");
}
catch (System.Text.Json.JsonException) { }
try
{
    System.Text.Json.JsonSerializer.Deserialize<ExpectedPokemonSlot>("{\"slotIndex\":0}");
    throw new Exception("Incomplete expected Pokémon JSON was accepted.");
}
catch (System.Text.Json.JsonException) { }


// Schema v2 independently compares every projected Pokémon detail field.
// The baseline below is deliberately same-Core synthetic, NOT desktop evidence.
var d = projectedPokemon.Details!;
static ExpectedPokemonStatSpread ExpectSpread(PokemonStatSpread x) =>
    new(x.HP, x.Attack, x.Defense, x.SpecialAttack, x.SpecialDefense, x.Speed);
var expectedDetails = new ExpectedPokemonReadOnlyDetails(
    d.Nature, d.AbilityId, d.HeldItemId, d.Gender, d.Form,
    new ExpectedPokemonMoveIds(d.Moves.First, d.Moves.Second, d.Moves.Third, d.Moves.Fourth),
    ExpectSpread(d.IndividualValues), ExpectSpread(d.EffortValues));
var detailExpectedSlot = new ExpectedPokemonSlot(
    projectedPokemon.SlotIndex, projectedPokemon.Species, projectedPokemon.Nickname,
    projectedPokemon.Level, projectedPokemon.IsEgg, projectedPokemon.IsShiny)
{ Details = expectedDetails };
var detailActual = view with { PartyCount = 3, Party = [projectedPokemon] };
var detailBaseline = expectation with { PartyCount = 3, Party = [detailExpectedSlot] };
if (SaveProjectionVerifier.Compare(detailActual, detailBaseline, requireDetails: true).Count != 0)
    throw new Exception("Schema v2 identical detailed projection did not match.");
var legacySlot = detailExpectedSlot with { Details = null };
var legacyBaseline = detailBaseline with { Party = [legacySlot] };
if (SaveProjectionVerifier.Compare(detailActual, legacyBaseline).Count != 0 ||
    !SaveProjectionVerifier.Compare(detailActual, legacyBaseline, requireDetails: true)
        .Contains("Party[0].Details missing"))
    throw new Exception("Schema v1/v2 detail compatibility or strictness failed.");
var mutations = new (string Path, ExpectedPokemonReadOnlyDetails Changed)[]
{
    ("Nature", expectedDetails with { Nature = "MISMATCH" }),
    ("AbilityId", expectedDetails with { AbilityId = expectedDetails.AbilityId + 1 }),
    ("HeldItemId", expectedDetails with { HeldItemId = expectedDetails.HeldItemId + 1 }),
    ("Gender", expectedDetails with { Gender = (byte)(expectedDetails.Gender + 1) }),
    ("Form", expectedDetails with { Form = (byte)(expectedDetails.Form + 1) }),
    ("Moves.First", expectedDetails with { Moves = expectedDetails.Moves with { First = (ushort)(expectedDetails.Moves.First + 1) } }),
    ("Moves.Second", expectedDetails with { Moves = expectedDetails.Moves with { Second = (ushort)(expectedDetails.Moves.Second + 1) } }),
    ("Moves.Third", expectedDetails with { Moves = expectedDetails.Moves with { Third = (ushort)(expectedDetails.Moves.Third + 1) } }),
    ("Moves.Fourth", expectedDetails with { Moves = expectedDetails.Moves with { Fourth = (ushort)(expectedDetails.Moves.Fourth + 1) } }),
    ("IndividualValues.HP", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { HP = expectedDetails.IndividualValues.HP + 1 } }),
    ("IndividualValues.Attack", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { Attack = expectedDetails.IndividualValues.Attack + 1 } }),
    ("IndividualValues.Defense", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { Defense = expectedDetails.IndividualValues.Defense + 1 } }),
    ("IndividualValues.SpecialAttack", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { SpecialAttack = expectedDetails.IndividualValues.SpecialAttack + 1 } }),
    ("IndividualValues.SpecialDefense", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { SpecialDefense = expectedDetails.IndividualValues.SpecialDefense + 1 } }),
    ("IndividualValues.Speed", expectedDetails with { IndividualValues = expectedDetails.IndividualValues with { Speed = expectedDetails.IndividualValues.Speed + 1 } }),
    ("EffortValues.HP", expectedDetails with { EffortValues = expectedDetails.EffortValues with { HP = expectedDetails.EffortValues.HP + 1 } }),
    ("EffortValues.Attack", expectedDetails with { EffortValues = expectedDetails.EffortValues with { Attack = expectedDetails.EffortValues.Attack + 1 } }),
    ("EffortValues.Defense", expectedDetails with { EffortValues = expectedDetails.EffortValues with { Defense = expectedDetails.EffortValues.Defense + 1 } }),
    ("EffortValues.SpecialAttack", expectedDetails with { EffortValues = expectedDetails.EffortValues with { SpecialAttack = expectedDetails.EffortValues.SpecialAttack + 1 } }),
    ("EffortValues.SpecialDefense", expectedDetails with { EffortValues = expectedDetails.EffortValues with { SpecialDefense = expectedDetails.EffortValues.SpecialDefense + 1 } }),
    ("EffortValues.Speed", expectedDetails with { EffortValues = expectedDetails.EffortValues with { Speed = expectedDetails.EffortValues.Speed + 1 } }),
};
foreach (var (field, changedDetails) in mutations)
{
    var changedBaseline = detailBaseline with
    { Party = [detailExpectedSlot with { Details = changedDetails }] };
    if (!SaveProjectionVerifier.Compare(detailActual, changedBaseline, requireDetails: true)
        .Contains("Party[0].Details." + field + " mismatch"))
        throw new Exception("Schema v2 failed to detect mismatch: " + field);
}
try
{
    System.Text.Json.JsonSerializer.Deserialize<ExpectedPokemonReadOnlyDetails>("{\"nature\":\"TEST\"}");
    throw new Exception("Incomplete nested detail JSON was accepted.");
}
catch (System.Text.Json.JsonException) { }

// Pure presentation is bounded to one box and must preserve source slot indices.
var sparse = view with
{
    PartyCount = 2,
    Party = [new PokemonSlot(0, 25, "A\nB", 5, false, true)],
    BoxCount = 2,
    BoxSlotCount = 30,
    Boxes =
    [
        new BoxSnapshot(0, []),
        new BoxSnapshot(1, [new PokemonSlot(29, 133, "EEVEE", 12, false, false)]),
    ],
};
var labels = ReadOnlyProjectionPresenter.BoxLabels(sparse);
if (labels.Count != 2 || labels[0] != "Box 1 (0 occupied)" ||
    labels[1] != "Box 2 (1 occupied)")
    throw new Exception("Box selection labels do not reflect sparse occupancy.");
if (!ReadOnlyProjectionPresenter.FormatBox(sparse, 0).Contains("No occupied slots.") ||
    !ReadOnlyProjectionPresenter.FormatBox(sparse, 1).Contains("Slot 30: #133 EEVEE") ||
    ReadOnlyProjectionPresenter.FormatBox(sparse, 1).Contains("Box 1"))
    throw new Exception("Single-box projection lost its original slot mapping.");
if (!ReadOnlyProjectionPresenter.FormatParty(sparse).Contains("A\nB".Replace("\n", " ")) ||
    ReadOnlyProjectionPresenter.FormatParty(sparse).Contains("A\nB"))
    throw new Exception("Untrusted nickname controls were not sanitized.");
if (!ReadOnlyProjectionPresenter.FormatTrainer(sparse, false).Contains("no game format is certified"))
    throw new Exception("Provisional presentation warning missing.");
try
{
    ReadOnlyProjectionPresenter.FormatBox(sparse, 2);
    throw new Exception("Out-of-range box selection must be rejected.");
}
catch (ArgumentOutOfRangeException) { }

// E-15/E-16: stable empty/occupied slot mapping and Unicode-safe labels.
var sparseCells = ReadOnlyProjectionPresenter.BoxCells(sparse, 1);
if (sparseCells.Count != 30 || sparseCells[0].SlotIndex != 0 ||
    sparseCells[0].Pokemon is not null || sparseCells[29].Pokemon?.Species != 133 ||
    ReadOnlyProjectionPresenter.FormatCellDetails(sparseCells[0]) != "Slot 1: empty" ||
    !ReadOnlyProjectionPresenter.FormatCellDetails(sparseCells[29]).Contains("Slot 30: #133"))
    throw new Exception("Stable slot-grid projection lost empty or occupied positions.");
if (ReadOnlyProjectionPresenter.BoxCells(sparse, 0).Any(cell => cell.Pokemon is not null))
    throw new Exception("Empty box gained phantom occupied cells.");
try
{
    ReadOnlyProjectionPresenter.BoxCells(sparse with
    {
        Boxes = [new BoxSnapshot(0, [new PokemonSlot(30, 25, "X", 5, false, false)])],
    }, 0);
    throw new Exception("Out-of-range source slot was accepted.");
}
catch (InvalidDataException) { }
try
{
    ReadOnlyProjectionPresenter.BoxCells(sparse with
    {
        Boxes = [new BoxSnapshot(0,
        [
            new PokemonSlot(0, 25, "A", 5, false, false),
            new PokemonSlot(0, 26, "B", 5, false, false),
        ])],
    }, 0);
    throw new Exception("Repeated source slot was accepted.");
}
catch (InvalidDataException) { }
if (ReadOnlyProjectionPresenter.SanitizeLabel("A\u202EB\u2066C") != "A B C" ||
    ReadOnlyProjectionPresenter.SanitizeLabel("A\nB") != "A B" ||
    ReadOnlyProjectionPresenter.SanitizeLabel("A\uD800B") != "A B" ||
    ReadOnlyProjectionPresenter.SanitizeLabel("A\u0301") != "A\u0301" ||
    ReadOnlyProjectionPresenter.SanitizeLabel("\U0001F600") != "\U0001F600" ||
    ReadOnlyProjectionPresenter.SanitizeLabel(new string('x', 81)) !=
        new string('x', 80) + "…")
    throw new Exception("Unicode-safe text sanitization failed.");
if (ReadOnlyProjectionPresenter.SanitizeLabel(new string('a', 79) + "A\u0301B") !=
    new string('a', 79) + "A\u0301…")
    throw new Exception("Grapheme truncation split a combining sequence.");
if (ReadOnlyProjectionPresenter.FormatCellDetails(
    new BoxCell(3, new PokemonSlot(3, 25, "A\u202EB", 10, false, true)))
    .Contains('\u202E'))
    throw new Exception("Accessibility slot details contain bidi override.");


// Party selection preserves original positions, rejects duplicates and exposes
// immutable, game-dependent Core fields without suggesting legality or edits.
var partyWithGap = sparse with
{
    PartyCount = 3,
    Party = [projectedPokemon],
};
var partyCells = ReadOnlyProjectionPresenter.PartyCells(partyWithGap);
if (partyCells.Count != 3 || partyCells[0].Pokemon is not null ||
    partyCells[2].Pokemon?.Species != 25 ||
    ReadOnlyProjectionPresenter.FormatPartySlotLabel(partyCells[0]) != "Party 1: empty" ||
    !ReadOnlyProjectionPresenter.FormatPartySlotDetails(partyCells[2]).Contains("Move IDs: 33, 45") ||
    !ReadOnlyProjectionPresenter.FormatPartySlotDetails(partyCells[2]).Contains("HP 123") ||
    !ReadOnlyProjectionPresenter.FormatPartySlotDetails(partyCells[2]).Contains("legality is not evaluated"))
    throw new Exception("Read-only party details or source positions are incorrect.");
try
{
    ReadOnlyProjectionPresenter.PartyCells(partyWithGap with
    {
        Party = [projectedPokemon, projectedPokemon],
    });
    throw new Exception("Repeated party slot was accepted.");
}
catch (InvalidDataException) { }
try
{
    ReadOnlyProjectionPresenter.PartyCells(partyWithGap with { PartyCount = 7 });
    throw new Exception("Oversized party capacity was accepted.");
}
catch (InvalidDataException) { }
if (ReadOnlyProjectionPresenter.FormatCellDetails(
    new BoxCell(2, projectedPokemon)).Contains("safe to export", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Scalar detail presentation implied write authorization.");

// A provider may ignore ReadAsync cancellation. Closing the owned descriptor
// off the UI thread should unblock cooperative providers and never publish a
// successful inspection after cancellation. This is a synthetic stream test,
// not a claim about actual SAF providers or a bounded cancellation deadline.
using (var cancellation = new CancellationTokenSource())
{
    var blocked = new BlockingReadStream();
    await using var bound = new CancellationBoundReadStream(blocked, cancellation.Token);
    var pendingRead = bound.ReadAsync(new byte[8].AsMemory()).AsTask();
    await blocked.Started.WaitAsync(TimeSpan.FromSeconds(5));
    cancellation.Cancel();
    if (await Task.WhenAny(pendingRead, Task.Delay(TimeSpan.FromSeconds(5))) != pendingRead)
        throw new Exception("Cancellation did not close the synthetic blocked provider.");
    await MustThrowAsync<OperationCanceledException>(() => pendingRead);
    if (!blocked.WasDisposed)
        throw new Exception("Provider descriptor was not closed on cancellation.");
}
using (var cancellation = new CancellationTokenSource())
{
    var inner = new MemoryStream(new byte[] { 1, 2, 3 }, writable: false);
    await using (var bound = new CancellationBoundReadStream(inner, cancellation.Token))
    {
        var bytes = new byte[3];
        if (await bound.ReadAsync(bytes.AsMemory()) != 3 || !bytes.SequenceEqual(new byte[] { 1, 2, 3 }))
            throw new Exception("Normal read-through changed provider bytes.");
        if (bound.CanWrite || bound.CanSeek)
            throw new Exception("Read-only wrapper exposed write or seek capability.");
    }
    if (inner.CanRead)
        throw new Exception("Normal disposal did not close the underlying provider.");
}
if (ReadOnlyGridLayout.ColumnsForWidth(400, 1f) != 6 ||
    ReadOnlyGridLayout.ColumnsForWidth(288, 1f) != 4 ||
    ReadOnlyGridLayout.ColumnsForWidth(288, 2f) != 2 ||
    ReadOnlyGridLayout.ColumnsForWidth(120, 2.5f) != 1 ||
    ReadOnlyGridLayout.ColumnsForWidth(0, 1f) != 1 ||
    ReadOnlyGridLayout.ColumnsForWidth(288, float.NaN) != 4)
    throw new Exception("Responsive grid column policy failed.");

Console.WriteLine("Pinned Core reader: synthetic negatives, RZIP and hack-risk cases, near-miss checks, 1 same-Core synthetic Gen 1 positive and PK1 scalar detail projection.");
Console.WriteLine("Provider-neutral read service: two-open success, identity-aware same-byte replacement refusal, missing identity, null stream, stale bytes, revoked provider and cancellation passed.");
Console.WriteLine("No independently verified real-world fixture, Android device, format certification or export has been tested.");
