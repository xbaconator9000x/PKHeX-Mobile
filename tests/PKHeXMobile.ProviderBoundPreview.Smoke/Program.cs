// Synthetic-only provider-bound preview checks. No Android provider or save writes.
using System.Buffers.Binary;
using PKForge.Domain;
using PKForge.Engine;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.PortConsolidation;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}
async Task Refuses<T>(Func<Task> run, string label) where T : Exception
{
    try { await run(); }
    catch (T) { checks++; return; }
    throw new Exception(label + ": expected " + typeof(T).Name);
}

var save = BlankSaveFile.Get(GameVersion.X, "TEST", LanguageID.English);
var mon = save.BlankPKM;
mon.Species = 25;
mon.CurrentLevel = 12;
mon.SetNickname("ORIGINAL");
mon.RefreshChecksum();
save.SetBoxSlotAtIndex(mon, 0, 0, EntityImportSettings.None);
BinaryPrimitives.WriteUInt32LittleEndian(save.Data[^0x1F0..], 0x42454546);
var original = save.Write().ToArray();
var untouched = original.ToArray();
var snapshot = SaveInputSnapshot.FromBytes(original);
using var engine = new SaveEngineSession(original.ToArray());
var sessions = new FakeSessions(engine, snapshot);
var access = new SpyAccess(original);
var preview = await ProviderBoundRetailNicknameEditor.PreviewAsync(
    sessions, access, 0, 0, "PROVIDER");
Check(preview.OriginalNickname == "ORIGINAL" && preview.PreviewNickname == "PROVIDER",
    "Preview did not use the original provider baseline.");
Check(access.Reads == 2 && access.Writes == 0, "Expected two bounded wrapper calls and no writes.");
Check(original.AsSpan().SequenceEqual(untouched), "Preview mutated the provider array.");
Check(preview.SourceFingerprint == snapshot.Fingerprint, "Preview source fingerprint differs.");
Check(preview.CandidateFingerprint != snapshot.Fingerprint, "Preview candidate unchanged.");
var candidate = preview.CopyCandidateBytes();
using (var reopened = new SaveEngineSession(candidate))
    Check(reopened.ReadEntity(0, 0).Nickname == "PROVIDER", "Preview did not reopen correctly.");
candidate[0] ^= 0x55;
Check(preview.CopyCandidateBytes()[0] != candidate[0], "Candidate escaped by mutable alias.");

var changed = original.ToArray();
changed[^1] ^= 1;
access.Reset(changed);
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "provider changed before preview");
Check(access.Writes == 0, "Provider mismatch triggered a write.");

access.Reset(original);
access.OnRead = count => { if (count == 2) access.SetBytes(changed); };
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "provider changed between preview checks");
Check(access.Reads == 2, "Post-preview provider recheck was skipped.");

access.Reset(original);
access.OnRead = count => { if (count == 1) sessions.ProviderOrigin = null; };
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "new selection invalidates binding");
sessions.Reset(snapshot);
access.Reset(original);
access.OnRead = count =>
{
    if (count == 1)
        sessions.Current = sessions.Current! with
        {
            Document = new PickedDocument("different-document", "same-bytes")
        };
};
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "same bytes with changed document ID");
sessions.Reset(snapshot);

access.Reset(new byte[SaveInputSnapshot.MaxInputBytes + 1]);
await Refuses<InvalidDataException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "oversized provider result");
access.Reset(original);
sessions.ProviderOrigin = null;
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER"),
    "missing provider-origin binding");
sessions.Reset(snapshot);
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
await Refuses<OperationCanceledException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(sessions, access, 0, 0, "PROVIDER", cancelled.Token),
    "pre-cancelled preview");
// A real async provider can be suspended when navigation cancels the UI request.
// This must propagate through PreviewAsync instead of waiting for the provider.
sessions.Reset(snapshot);
var stalled = new BlockingAccess();
using (var inFlight = new CancellationTokenSource())
{
    var pending = ProviderBoundRetailNicknameEditor.PreviewAsync(
        sessions, stalled, 0, 0, "PROVIDER", inFlight.Token);
    await stalled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    inFlight.Cancel();
    await Refuses<OperationCanceledException>(() => pending, "in-flight provider cancellation");
}
Check(stalled.Reads == 1 && stalled.Writes == 0, "Cancelled provider read reached a writer.");

// Cancellation arriving during the second provider recheck must not return a
// candidate, even if the provider has already handed back matching bytes.
access.Reset(original);
using (var late = new CancellationTokenSource())
{
    access.OnRead = count => { if (count == 2) late.Cancel(); };
    await Refuses<OperationCanceledException>(
        () => ProviderBoundRetailNicknameEditor.PreviewAsync(
            sessions, access, 0, 0, "PROVIDER", late.Token),
        "cancellation during second provider recheck");
}
Check(access.Reads == 2, "Late cancellation skipped the second provider recheck.");
Check(access.Writes == 0, "A refusal or cancellation reached a provider writer.");
Check(original.AsSpan().SequenceEqual(untouched), "Refusals mutated original provider bytes.");
Console.WriteLine($"Provider-bound retail preview: {checks} synthetic checks passed; no writes.");
Console.WriteLine("No Android provider identity, real-save fixture, Save As or device certification.");

sealed class FakeSessions : ISaveSessionService, IProviderOriginSession
{
    private readonly ISaveEngineSession _engine;
    public FakeSessions(ISaveEngineSession engine, SaveInputSnapshot source)
    {
        _engine = engine;
        Reset(source);
    }
    public SaveSession? Current { get; set; }
    public ISaveEngineSession? CurrentSession { get; set; }
    public ProviderOriginBinding? ProviderOrigin { get; set; }
    public void Reset(SaveInputSnapshot source)
    {
        CurrentSession = _engine;
        Current = new SaveSession(new PickedDocument("document-1", "synthetic"),
            _engine.Snapshot);
        ProviderOrigin = new ProviderOriginBinding("document-1", _engine, source, 1);
    }
    public ValueTask<SaveSession> OpenAsync(PickedDocument document, CancellationToken cancellationToken = default)
        => ValueTask.FromException<SaveSession>(new NotSupportedException());
    public void MarkWritten(string documentId, ReadOnlyMemory<byte> written) => ProviderOrigin = null;
    public void RevertToBaseline() => ProviderOrigin = null;
    public void Close() { ProviderOrigin = null; CurrentSession = null; Current = null; }
}

sealed class SpyAccess(byte[] initial) : ISaveFileAccess
{
    private byte[] _bytes = initial.ToArray();
    public int Reads { get; private set; }
    public int Writes { get; private set; }
    public Action<int>? OnRead { get; set; }
    public void Reset(byte[] bytes) { _bytes = bytes.ToArray(); Reads = 0; OnRead = null; }
    public void SetBytes(byte[] bytes) => _bytes = bytes.ToArray();
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (documentId != "document-1") throw new InvalidOperationException("Wrong document.");
        Reads++;
        OnRead?.Invoke(Reads);
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(_bytes.ToArray());
    }
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        Writes++;
        throw new InvalidOperationException("Provider writes must never be reached.");
    }
}

sealed class BlockingAccess : ISaveFileAccess
{
    public int Reads { get; private set; }
    public int Writes { get; private set; }
    public TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        Reads++;
        Started.TrySetResult(true);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return ReadOnlyMemory<byte>.Empty;
    }
    public ValueTask WriteAtomicallyAsync(
        string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        Writes++;
        throw new InvalidOperationException("Provider writes must never be reached.");
    }
}
