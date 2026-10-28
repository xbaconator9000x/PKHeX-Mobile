using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool value, string message)
{
    if (!value) throw new Exception(message);
}
static async Task MustThrow<T>(Func<Task> attempt) where T : Exception
{
    try { await attempt(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

var a = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
var b = (byte[])a.Clone();
b[3] ^= 0x80;
var identityA = new DocumentSourceIdentity("org.example.synthetic", "document-A");
var identityB = new DocumentSourceIdentity("org.example.synthetic", "document-B");

var stableStreams = new List<ProbeStream>();
int stableOpens = 0;
var stable = await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
{
    stableOpens++;
    var stream = new ProbeStream(a);
    stableStreams.Add(stream);
    return ValueTask.FromResult(new DocumentReadHandle(stream, identityA));
});
Must(stableOpens == 2 && stableStreams.All(s => s.Disposed) &&
    stable.SourceIdentityConfirmed && stable.SourceIdentity == identityA &&
    !stable.IsRecognized && stable.RejectionReason is not null,
    "Stable provider was not read twice, disposed, or accurately marked unrecognized.");

// A matching URI-derived identity is only a hint: even a provider that replaced
// the file with the same bytes cannot be authenticated by these two reads.
int sameByteOpens = 0;
var sameByte = await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
{
    sameByteOpens++;
    return ValueTask.FromResult(new DocumentReadHandle(new ProbeStream(a), identityA));
});
Must(sameByteOpens == 2 && sameByte.SourceIdentityConfirmed &&
    !sameByte.IsRecognized, "Same-byte replacement semantics drifted.");

var noIdentity = await ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    ValueTask.FromResult(new DocumentReadHandle(new ProbeStream(a), null)));
Must(!noIdentity.SourceIdentityConfirmed && noIdentity.SourceIdentity is null,
    "Stream-only or identity-less reads were promoted to verified identity.");

int changedOpens = 0;
var changedStreams = new List<ProbeStream>();
await MustThrow<InvalidOperationException>(() =>
    ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        var stream = new ProbeStream(++changedOpens == 1 ? a : b);
        changedStreams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, identityA));
    }));
Must(changedOpens == 2 && changedStreams.All(s => s.Disposed),
    "Same-ID changed bytes were published or streams leaked.");

int replacedOpens = 0;
var replacedStreams = new List<ProbeStream>();
await MustThrow<InvalidOperationException>(() =>
    ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        var stream = new ProbeStream(a);
        replacedStreams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(
            stream, ++replacedOpens == 1 ? identityA : identityB));
    }));
Must(replacedOpens == 2 && replacedStreams.All(s => s.Disposed),
    "Same-byte identity replacement was accepted or leaked a descriptor.");

int failedReadOpens = 0;
var failing = new List<ProbeStream>();
await MustThrow<IOException>(() =>
    ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        var stream = new ProbeStream(a) { ThrowOnRead = ++failedReadOpens == 2 };
        failing.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, identityA));
    }));
Must(failedReadOpens == 2 && failing.All(s => s.Disposed),
    "A second-open read fault leaked streams or returned a projection.");

var firstReadFault = new ProbeStream(a) { ThrowOnRead = true };
await MustThrow<IOException>(() =>
    SaveDocumentSession.OpenWithIdentityAsync(_ =>
        ValueTask.FromResult(new DocumentReadHandle(firstReadFault, identityA))));
Must(firstReadFault.Disposed, "First-open read failure leaked the provider stream.");

int openFailureCount = 0;
await MustThrow<IOException>(() =>
    ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        if (++openFailureCount == 2) throw new IOException("synthetic provider revoke");
        return ValueTask.FromResult(new DocumentReadHandle(new ProbeStream(a), identityA));
    }));
Must(openFailureCount == 2, "Provider open revocation was not propagated.");

var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var cancelledStreams = new List<ProbeStream>();
int cancelledOpens = 0;
using (var cancellation = new CancellationTokenSource())
{
    var operation = ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
    {
        var stream = new ProbeStream(a);
        if (++cancelledOpens == 2)
        {
            stream.Gate = gate;
            stream.ReadStarted = started;
        }
        cancelledStreams.Add(stream);
        return ValueTask.FromResult(new DocumentReadHandle(stream, identityA));
    }, cancellation.Token);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
    cancellation.Cancel();
    await MustThrow<OperationCanceledException>(() => operation);
}
Must(cancelledOpens == 2 && cancelledStreams.All(s => s.Disposed),
    "Cooperative second-read cancellation leaked streams or published stale data.");

// A subsequent failed verification must revoke the earlier identity-confirmed
// state rather than leaving a stale success flag on a reusable session.
var sessionBytes = a;
var session = await SaveDocumentSession.OpenWithIdentityAsync(_ =>
    ValueTask.FromResult(new DocumentReadHandle(new ProbeStream(sessionBytes), identityA)));
await session.VerifySourceUnchangedAsync();
Must(session.SourceIdentityConfirmed, "Initial re-open failed to confirm stable identity.");
sessionBytes = b;
await MustThrow<InvalidOperationException>(() => session.VerifySourceUnchangedAsync());
Must(!session.SourceIdentityConfirmed, "Failed re-verification retained stale confirmation.");

int preCancelledOpens = 0;
using (var preCancelled = new CancellationTokenSource())
{
    preCancelled.Cancel();
    await MustThrow<OperationCanceledException>(() =>
        ReadOnlyDocumentService.InspectWithIdentityAsync(_ =>
        {
            preCancelledOpens++;
            return ValueTask.FromResult(new DocumentReadHandle(new ProbeStream(a), identityA));
        }, preCancelled.Token));
}
Must(preCancelledOpens == 0, "Pre-cancelled operation opened a provider.");

var empty = new ProbeStream([]);
await MustThrow<InvalidDataException>(() =>
    SaveDocumentSession.OpenWithIdentityAsync(_ =>
        ValueTask.FromResult(new DocumentReadHandle(empty, identityA))));
Must(empty.Disposed, "Empty provider stream was not disposed.");
var huge = new ProbeStream(new byte[SaveInputSnapshot.MaxInputBytes + 1]) { MaxChunk = 4096 };
await MustThrow<InvalidDataException>(() =>
    SaveDocumentSession.OpenWithIdentityAsync(_ =>
        ValueTask.FromResult(new DocumentReadHandle(huge, identityA))));
Must(huge.Disposed, "Oversized provider stream was not disposed.");

var legacy = await ReadOnlyDocumentService.InspectAsync(_ =>
    ValueTask.FromResult<Stream>(new ProbeStream(a)));
Must(!legacy.SourceIdentityConfirmed && legacy.SourceIdentity is null,
    "Legacy stream-only route falsely confirmed provider identity.");

Console.WriteLine("Provider fault suite: stable/changed identity, byte races, two-open disposal, read/open faults, cooperative cancellation, stale confirmation, nonseekable short reads, empty/oversized inputs and legacy identity boundary passed. Synthetic only; no Android device certification.");

sealed class ProbeStream(byte[] data) : Stream
{
    private readonly MemoryStream _inner = new(data, writable: false);
    public bool Disposed { get; private set; }
    public bool ThrowOnRead { get; set; }
    public int MaxChunk { get; set; } = 3;
    public TaskCompletionSource? Gate { get; set; }
    public TaskCompletionSource? ReadStarted { get; set; }
    public override bool CanRead => !Disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        if (ThrowOnRead) throw new IOException("synthetic read fault");
        return _inner.Read(buffer[..Math.Min(buffer.Length, MaxChunk)]);
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnRead) throw new IOException("synthetic read fault");
        if (Gate is { } gate)
        {
            ReadStarted?.TrySetResult();
            await gate.Task.WaitAsync(cancellationToken);
        }
        return await _inner.ReadAsync(buffer[..Math.Min(buffer.Length, MaxChunk)], cancellationToken);
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        Disposed = true;
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
