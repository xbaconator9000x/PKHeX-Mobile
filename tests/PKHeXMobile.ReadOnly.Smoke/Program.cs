using System.Buffers.Binary;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;
using PKHeX.Core;

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


// Pure presentation is bounded to one box and must preserve source slot indices.
var sparse = view with
{
    PartyCount = 2,
    Party = [new PokemonSlot(0, 25, "A\\nB", 5, false, true)],
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
if (!ReadOnlyProjectionPresenter.FormatParty(sparse).Contains("A\\nB".Replace("\\n", " ")) ||
    ReadOnlyProjectionPresenter.FormatParty(sparse).Contains("A\\nB"))
    throw new Exception("Untrusted nickname controls were not sanitized.");
if (!ReadOnlyProjectionPresenter.FormatTrainer(sparse, false).Contains("not certified"))
    throw new Exception("Provisional presentation warning missing.");
try
{
    ReadOnlyProjectionPresenter.FormatBox(sparse, 2);
    throw new Exception("Out-of-range box selection must be rejected.");
}
catch (ArgumentOutOfRangeException) { }

Console.WriteLine("Pinned Core reader: synthetic negatives, RZIP and hack-risk cases, near-miss checks and 1 same-Core synthetic Gen 1 positive.");
Console.WriteLine("Provider-neutral read service: two-open success, identity-aware same-byte replacement refusal, missing identity, null stream, stale bytes, revoked provider and cancellation passed.");
Console.WriteLine("No independently verified real-world fixture, Android device, format certification or export has been tested.");
