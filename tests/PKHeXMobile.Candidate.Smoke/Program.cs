using PKHeXMobile.Domain;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}
static void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
static async Task RejectAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

var bytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
var original = SaveInputSnapshot.FromBytes(bytes);
var edited = (byte[])bytes.Clone();
foreach (var i in new[] { 4, 5, 7, 20, 21 }) edited[i] ^= 0xFF;
var scope = new[] { new SaveByteRange(20, 2), new SaveByteRange(4, 4) };
var audit = SaveCandidateScopeAudit.Analyze(original, edited, scope);
Check(audit.ChangedByteCount == 5 && audit.ChangedRuns.Count == 3, "Disjoint byte runs were not measured.");
Check(audit.ChangedRuns[0] == new SaveChangedRun(4, 2) &&
      audit.ChangedRuns[1] == new SaveChangedRun(7, 1) &&
      audit.ChangedRuns[2] == new SaveChangedRun(20, 2),
      "Byte-run offsets or lengths are incorrect.");
Check(audit.ApprovedRanges[0] == new SaveByteRange(4, 4) &&
      audit.ApprovedRanges[1] == new SaveByteRange(20, 2),
      "Approved ranges were not sorted.");
Check(audit.Original == original.Fingerprint &&
      audit.Candidate.Sha256Hex != original.Fingerprint.Sha256Hex,
      "Candidate digest did not differ from original.");
edited[4] ^= 0xFF;
Check(audit.ChangedByteCount == 5 && audit.ChangedRuns[0].Length == 2,
      "Audit retained a mutable candidate array.");

Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, edited, [new SaveByteRange(20, 2)]));
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, bytes.AsSpan(0, 127), scope));
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, bytes, scope));
var unchanged = SaveCandidateScopeAudit.Analyze(original, bytes, scope, requireChange: false);
Check(unchanged.ChangedByteCount == 0 && unchanged.ChangedRuns.Count == 0 &&
      unchanged.Candidate == unchanged.Original, "Explicit no-op was not preserved.");

foreach (var invalid in new[]
{
    new SaveByteRange(-1, 1), new SaveByteRange(0, 0),
    new SaveByteRange(0, -1), new SaveByteRange(128, 1),
    new SaveByteRange(int.MaxValue, int.MaxValue),
})
{
    Reject<InvalidOperationException>(() =>
        SaveCandidateScopeAudit.Analyze(original, bytes, [invalid], requireChange: false));
}
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, bytes, Array.Empty<SaveByteRange>(), requireChange: false));
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, bytes,
        [new SaveByteRange(4, 4), new SaveByteRange(7, 4)], requireChange: false));
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(original, bytes,
        Enumerable.Repeat(new SaveByteRange(0, 1), SaveCandidateScopeAudit.MaxApprovedRanges + 1),
        requireChange: false));

var alternating = new byte[SaveCandidateScopeAudit.MaxReportedRuns * 2 + 1];
for (int i = 0; i < alternating.Length; i += 2) alternating[i] = 1;
var zeros = SaveInputSnapshot.FromBytes(new byte[alternating.Length]);
Reject<InvalidOperationException>(() =>
    SaveCandidateScopeAudit.Analyze(zeros, alternating, [new SaveByteRange(0, alternating.Length)]));

// This byte-level primitive never overrides the independent save/profile write gate.
Reject<InvalidOperationException>(() =>
    SaveWriteGuard.RequireAuthorized(
        new SaveInspection("unverified", SaveCompatibility.SupportedReadOnly, null), 128, 128));

var opened = new List<TrackedStream>();
int opens = 0;
var proof = await BackupReadbackVerifier.VerifyAsync(original, _ =>
{
    opens++;
    var stream = new TrackedStream(bytes);
    opened.Add(stream);
    return ValueTask.FromResult<Stream>(stream);
});
Check(opens == 2 && proof.VerifiedOpens == 2 &&
      proof.OriginalFingerprint == original.Fingerprint &&
      opened.All(s => s.WasDisposed), "Two-open backup readback did not verify/dispose both streams.");

int replacedOpens = 0;
var changedBackup = new List<TrackedStream>();
await RejectAsync<InvalidOperationException>(() =>
    BackupReadbackVerifier.VerifyAsync(original, _ =>
    {
        var data = ++replacedOpens == 1 ? bytes : bytes.Select(b => (byte)(b ^ 1)).ToArray();
        var stream = new TrackedStream(data);
        changedBackup.Add(stream);
        return ValueTask.FromResult<Stream>(stream);
    }));
Check(replacedOpens == 2 && changedBackup.All(s => s.WasDisposed),
      "Changed backup bytes were accepted or a stream leaked.");

var shortRead = new TrackedStream(bytes.AsSpan(0, 64).ToArray());
await RejectAsync<InvalidOperationException>(() =>
    BackupReadbackVerifier.VerifyAsync(original, _ =>
        ValueTask.FromResult<Stream>(shortRead)));
Check(shortRead.WasDisposed, "Short backup read leaked a stream.");

int revokedOpens = 0;
await RejectAsync<IOException>(() =>
    BackupReadbackVerifier.VerifyAsync(original, _ =>
    {
        if (++revokedOpens == 2) throw new IOException("Synthetic permission revoked");
        return ValueTask.FromResult<Stream>(new TrackedStream(bytes));
    }));
Check(revokedOpens == 2, "Second backup open was not attempted.");

int cancelledOpens = 0;
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    await RejectAsync<OperationCanceledException>(() =>
        BackupReadbackVerifier.VerifyAsync(original, _ =>
        {
            cancelledOpens++;
            return ValueTask.FromResult<Stream>(new TrackedStream(bytes));
        }, cts.Token));
}
Check(cancelledOpens == 0, "Pre-cancelled backup check opened a provider.");

Console.WriteLine("Candidate safety suite: exact scoped changes, immutable fingerprints, no-op/size/overlap/bounds refusal, bounded diagnostics, two-open backup readback, provider races, disposal and cancellation passed. Synthetic only; no write/export authorization.");

sealed class TrackedStream(byte[] data) : MemoryStream(data, writable: false)
{
    public bool WasDisposed { get; private set; }
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
    public override ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return base.DisposeAsync();
    }
}
