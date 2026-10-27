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

Console.WriteLine("Pinned Core reader: 5 original negatives, 6 hack-risk cases, 5 near-miss checks and 1 same-Core synthetic Gen 1 positive.");
Console.WriteLine("Provider-neutral read service: two-open success, rejection, stale bytes, revoked provider and cancellation passed.");
Console.WriteLine("No independently verified real-world fixture, Android device, format certification or export has been tested.");
