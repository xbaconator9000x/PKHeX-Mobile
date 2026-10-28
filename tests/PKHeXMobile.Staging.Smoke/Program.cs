using PKHeXMobile.Domain;

static void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static async Task RejectAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

var originalBytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
var candidateBytes = (byte[])originalBytes.Clone();
candidateBytes[4] ^= 0x11;
candidateBytes[5] ^= 0x22;
candidateBytes[30] ^= 0x33;
var original = SaveInputSnapshot.FromBytes(originalBytes);
var candidate = SaveInputSnapshot.FromBytes(candidateBytes);
var sourceId = new DocumentSourceIdentity("synthetic", "source");
var backupId = new DocumentSourceIdentity("synthetic", "backup");
var stageId = new DocumentSourceIdentity("synthetic", "stage");
var ranges = new[] { new SaveByteRange(4, 2), new SaveByteRange(30, 1) };

var sourceOpens = new List<TrackedStream>();
var source = await OpenSource(originalBytes, _ => sourceId, _ => originalBytes, sourceOpens);
var backupOpens = new List<TrackedStream>();
var stageOpens = new List<TrackedStream>();
var proof = await StagedCandidatePreflight.VerifyAsync(
    source, candidateBytes, ranges,
    Artifact(_ => (originalBytes, backupId), backupOpens),
    Artifact(_ => (candidateBytes, stageId), stageOpens));
Check(proof.ChangeAudit.ChangedByteCount == 3 &&
      proof.ChangeAudit.ChangedRuns.Count == 2 &&
      proof.Backup.Fingerprint == original.Fingerprint &&
      proof.StagedCandidate.Fingerprint == candidate.Fingerprint &&
      proof.SourceIdentity == sourceId &&
      proof.Backup.Identity == backupId &&
      proof.StagedCandidate.Identity == stageId,
      "Successful preflight returned incorrect fingerprints, identities or change scope.");
Check(sourceOpens.Count == 3 && backupOpens.Count == 2 && stageOpens.Count == 2 &&
      sourceOpens.Concat(backupOpens).Concat(stageOpens).All(s => s.WasDisposed),
      "Preflight did not independently open and dispose source, backup and stage.");
candidateBytes[4] ^= 0x11;
Check(proof.ChangeAudit.ChangedByteCount == 3 &&
      proof.StagedCandidate.Fingerprint == candidate.Fingerprint,
      "Preflight evidence retained a mutable candidate.");
candidateBytes[4] ^= 0x11;

// Artifact verification must refuse aliases, missing/changing identities, changing
// bytes, short streams, cancelled reads and provider errors.
async Task RejectArtifact<T>(
    Func<int, (byte[] Bytes, DocumentSourceIdentity? Identity)> get,
    DocumentSourceIdentity[] forbidden) where T : Exception
{
    var opened = new List<TrackedStream>();
    await RejectAsync<T>(async () => await ArtifactReadbackVerifier.VerifyAsync(
        original, Artifact(get, opened), forbidden));
    Check(opened.All(s => s.WasDisposed), "Artifact failure leaked a stream.");
}
await RejectArtifact<InvalidOperationException>(_ => (originalBytes, sourceId), [sourceId]);
await RejectArtifact<InvalidOperationException>(_ => (originalBytes, null), [sourceId]);
await RejectArtifact<InvalidOperationException>(i =>
    (originalBytes, i == 1 ? backupId : stageId), [sourceId]);
await RejectArtifact<InvalidOperationException>(i =>
    (i == 1 ? originalBytes : candidateBytes, backupId), [sourceId]);
await RejectArtifact<InvalidOperationException>(_ =>
    (originalBytes.AsSpan(0, 32).ToArray(), backupId), [sourceId]);

int failedOpens = 0;
var failedStreams = new List<TrackedStream>();
await RejectAsync<IOException>(async () =>
    await ArtifactReadbackVerifier.VerifyAsync(original, token =>
    {
        if (++failedOpens == 2) throw new IOException("Synthetic provider failure");
        var stream = new TrackedStream(originalBytes);
        failedStreams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, backupId));
    }, [sourceId]));
Check(failedOpens == 2 && failedStreams.All(s => s.WasDisposed),
      "Second-open provider failure was not propagated or first stream leaked.");

int preCancelledOpens = 0;
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    await RejectAsync<OperationCanceledException>(async () =>
        await ArtifactReadbackVerifier.VerifyAsync(original, _ =>
        {
            preCancelledOpens++;
            return ValueTask.FromResult(new DocumentReadHandle(
                new TrackedStream(originalBytes), backupId));
        }, [sourceId], cts.Token));
}
Check(preCancelledOpens == 0, "Pre-cancelled verification opened an artifact.");

// The preflight is intentionally fail-closed for identity-less source documents.
var anonymous = await OpenSource(originalBytes, _ => null, _ => originalBytes, []);
await RejectAsync<InvalidOperationException>(async () =>
    await StagedCandidatePreflight.VerifyAsync(
        anonymous, candidateBytes, ranges,
        Artifact(_ => (originalBytes, backupId), []),
        Artifact(_ => (candidateBytes, stageId), [])));

// Candidate byte scope must be approved; stage/backup reads are not attempted
// after a malformed or unauthorized candidate.
int prematureArtifactOpens = 0;
var scopedSource = await OpenSource(originalBytes, _ => sourceId, _ => originalBytes, []);
await RejectAsync<InvalidOperationException>(async () =>
    await StagedCandidatePreflight.VerifyAsync(
        scopedSource, candidateBytes, [new SaveByteRange(4, 2)],
        _ => { prematureArtifactOpens++; throw new Exception("Should not open backup"); },
        _ => { prematureArtifactOpens++; throw new Exception("Should not open stage"); }));
Check(prematureArtifactOpens == 0, "Unauthorized candidate reached artifact providers.");

// A backup must not be the source, and a stage must not be the backup or source.
async Task RejectPreflight<T>(
    Func<int, (byte[] Bytes, DocumentSourceIdentity? Identity)> backup,
    Func<int, (byte[] Bytes, DocumentSourceIdentity? Identity)> stage) where T : Exception
{
    var s = await OpenSource(originalBytes, _ => sourceId, _ => originalBytes, []);
    await RejectAsync<T>(async () => await StagedCandidatePreflight.VerifyAsync(
        s, candidateBytes, ranges, Artifact(backup, []), Artifact(stage, [])));
}
await RejectPreflight<InvalidOperationException>(
    _ => (originalBytes, sourceId), _ => (candidateBytes, stageId));
await RejectPreflight<InvalidOperationException>(
    _ => (originalBytes, backupId), _ => (candidateBytes, backupId));
await RejectPreflight<InvalidOperationException>(
    _ => (originalBytes, backupId), _ => (candidateBytes, sourceId));
await RejectPreflight<InvalidOperationException>(
    _ => (originalBytes, backupId), _ => (candidateBytes, null));
await RejectPreflight<InvalidOperationException>(
    _ => (originalBytes, backupId), i => (i == 1 ? candidateBytes : originalBytes, stageId));

// The final source read catches races AFTER the backup and stage both verified.
var finalSourceOpens = new List<TrackedStream>();
var changedAtEnd = await OpenSource(originalBytes, _ => sourceId,
    i => i == 3 ? candidateBytes : originalBytes, finalSourceOpens);
await RejectAsync<InvalidOperationException>(async () =>
    await StagedCandidatePreflight.VerifyAsync(
        changedAtEnd, candidateBytes, ranges,
        Artifact(_ => (originalBytes, backupId), []),
        Artifact(_ => (candidateBytes, stageId), [])));
Check(finalSourceOpens.Count == 3 && finalSourceOpens.All(s => s.WasDisposed) &&
      !changedAtEnd.SourceIdentityConfirmed,
      "Changed source passed final revalidation or retained stale identity confirmation.");

var swappedAtEnd = await OpenSource(originalBytes,
    i => i == 3 ? stageId : sourceId, _ => originalBytes, []);
await RejectAsync<InvalidOperationException>(async () =>
    await StagedCandidatePreflight.VerifyAsync(
        swappedAtEnd, candidateBytes, ranges,
        Artifact(_ => (originalBytes, backupId), []),
        Artifact(_ => (candidateBytes, stageId), [])));
Check(!swappedAtEnd.SourceIdentityConfirmed, "Same-byte source replacement was accepted.");

int cancelledSourceOpens = 0;
using (var cts = new CancellationTokenSource())
{
    var cancelSource = await SaveDocumentSession.OpenWithIdentityAsync(_ =>
    {
        cancelledSourceOpens++;
        return ValueTask.FromResult(new DocumentReadHandle(
            new TrackedStream(originalBytes), sourceId));
    });
    cts.Cancel();
    await RejectAsync<OperationCanceledException>(async () =>
        await StagedCandidatePreflight.VerifyAsync(
            cancelSource, candidateBytes, ranges,
            Artifact(_ => (originalBytes, backupId), []),
            Artifact(_ => (candidateBytes, stageId), []), cts.Token));
}
Check(cancelledSourceOpens == 1, "Pre-cancelled preflight reopened source.");

Console.WriteLine("Staging preflight suite: detached candidate scope, distinct source/backup/stage identities, two-open byte readback, disposal, stale-source race, provider failures and cancellation passed. Synthetic non-publishing evidence only.");

static Func<CancellationToken, ValueTask<DocumentReadHandle>> Artifact(
    Func<int, (byte[] Bytes, DocumentSourceIdentity? Identity)> data,
    List<TrackedStream> streams)
{
    int count = 0;
    return _ =>
    {
        var (bytes, identity) = data(++count);
        var stream = new TrackedStream(bytes);
        streams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, identity));
    };
}

static Task<SaveDocumentSession> OpenSource(
    byte[] initialBytes,
    Func<int, DocumentSourceIdentity?> identity,
    Func<int, byte[]> data,
    List<TrackedStream> streams)
{
    int count = 0;
    return SaveDocumentSession.OpenWithIdentityAsync(_ =>
    {
        var index = ++count;
        var stream = new TrackedStream(data(index));
        streams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, identity(index)));
    });
}

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
