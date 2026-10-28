// Executable lifecycle tests against the actual adapted PKForge SaveSessionService.
// Synthetic Core fixture only; no Android provider or write certification.
using System.Buffers.Binary;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.PortConsolidation;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
async Task Refuses<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); }
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
var access = new MemoryAccess(original);
var service = new SaveSessionService(access, new SaveEngine());
var first = await service.OpenAsync(new PickedDocument("source-A", "synthetic"));
Check(service.ProviderOrigin is { }, "Original provider binding was not retained.");
Check(service.ProviderOrigin!.DocumentId == "source-A", "Wrong bound document ID.");
Check(service.ProviderOrigin.Original.Matches(original), "Engine normalization leaked into provider baseline.");
Check(ReferenceEquals(service.ProviderOrigin.EngineSession, service.CurrentSession),
    "Binding does not own active engine session.");
Check(first.Document.DocumentId == "source-A", "Opened wrong source.");
var firstGeneration = service.ProviderOrigin.Generation;
var preview = await ProviderBoundRetailNicknameEditor.PreviewAsync(service, access, 0, 0, "PREVIEW");
Check(preview.PreviewNickname == "PREVIEW", "Real session service failed provider-bound preview.");
Check(access.Writes == 0, "Read-only preview reached writer.");
Check(original.AsSpan().SequenceEqual(untouched), "Original source mutated.");

service.MarkWritten("source-A", original);
Check(service.ProviderOrigin is null, "Write notification retained old baseline.");
await Refuses<InvalidOperationException>(
    () => ProviderBoundRetailNicknameEditor.PreviewAsync(service, access, 0, 0, "PREVIEW"),
    "preview after write notification");
await service.OpenAsync(new PickedDocument("source-A", "synthetic"));
Check(service.ProviderOrigin is { }, "Reopen did not restore provider binding.");
Check(service.ProviderOrigin!.Generation > firstGeneration, "Reopen generation did not advance.");
service.RevertToBaseline();
Check(service.ProviderOrigin is null, "Revert retained stale provider-origin binding.");
await service.OpenAsync(new PickedDocument("source-A", "synthetic"));
Check(service.ProviderOrigin is { }, "Third open did not bind original.");
service.Close();
Check(service.ProviderOrigin is null && service.CurrentSession is null && service.Current is null,
    "Close did not invalidate source/session.");
Check(access.Writes == 0, "Lifecycle operations reached writer.");

// Deliberately complete a newer document open before an older blocked read.
// The old request must not overwrite the newer session or provider baseline.
var delayed = new DelayFirstReadAccess(original);
var racing = new SaveSessionService(delayed, new SaveEngine());
var oldTask = racing.OpenAsync(new PickedDocument("old", "old")).AsTask();
await delayed.Entered.Task;
var newer = await racing.OpenAsync(new PickedDocument("new", "new"));
Check(newer.Document.DocumentId == "new", "Newer open returned wrong document.");
delayed.Release.TrySetResult(true);
await Refuses<OperationCanceledException>(async () => { await oldTask; }, "out-of-order open");
Check(racing.Current?.Document.DocumentId == "new", "Late old open replaced newer session.");
Check(racing.ProviderOrigin?.DocumentId == "new", "Late old open replaced provider binding.");
racing.Close();
Check(delayed.Writes == 0, "Concurrent opens wrote to provider.");
Check(original.AsSpan().SequenceEqual(untouched), "Lifecycle tests mutated synthetic original.");
// The private hook pauses after the final epoch check, inside the activation gate.
// A second lifecycle operation cannot commit through this boundary.
var activationProperty = typeof(SaveSessionService).GetProperty(
    "_activationTestHook", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    ?? throw new Exception("Missing activation interleaving test hook.");
var activationGateField = typeof(SaveSessionService).GetField(
    "_activationGate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    ?? throw new Exception("Missing activation transaction gate.");

async Task ExerciseActivationBoundary(bool closeInsteadOfNewOpen)
{
    var guarded = new SaveSessionService(new MemoryAccess(original), new SaveEngine());
    using var entered = new ManualResetEventSlim(false);
    using var release = new ManualResetEventSlim(false);
    var calls = 0;
    activationProperty.SetValue(guarded, (Action)(() =>
    {
        if (Interlocked.Increment(ref calls) == 1)
        {
            entered.Set();
            release.Wait();
        }
    }));
    var firstOpen = Task.Factory.StartNew(
        () => guarded.OpenAsync(new PickedDocument("first", "synthetic")).AsTask().GetAwaiter().GetResult(),
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    try
    {
        Check(entered.Wait(TimeSpan.FromSeconds(15)), "Activation boundary hook was not reached.");
        var gate = activationGateField.GetValue(guarded) ?? throw new Exception("Null activation gate.");
        var unexpectedlyAcquired = Monitor.TryEnter(gate);
        if (unexpectedlyAcquired) Monitor.Exit(gate);
        Check(!unexpectedlyAcquired, "Commit gate is not held across activation.");
        if (closeInsteadOfNewOpen)
        {
            var close = Task.Factory.StartNew(() => guarded.Close(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            release.Set();
            await firstOpen;
            await close;
            Check(guarded.Current is null && guarded.CurrentSession is null && guarded.ProviderOrigin is null,
                "Close racing activation left a published session.");
        }
        else
        {
            var secondOpen = Task.Factory.StartNew(
                () => guarded.OpenAsync(new PickedDocument("second", "synthetic")).AsTask().GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            release.Set();
            await firstOpen;
            await secondOpen;
            Check(guarded.Current?.Document.DocumentId == "second"
                  && guarded.ProviderOrigin?.DocumentId == "second"
                  && ReferenceEquals(guarded.CurrentSession, guarded.ProviderOrigin?.EngineSession),
                "Second open failed to supersede first committed session.");
            guarded.Close();
        }
    }
    finally
    {
        release.Set();
        guarded.Close();
    }
}
await ExerciseActivationBoundary(false); // AT-01: check-to-activation Open/Open
await ExerciseActivationBoundary(true);  // AT-02: check-to-activation Open/Close

// Revert and write notifications must advance the open epoch even while a read is blocked.
foreach (var invalidateByRevert in new[] { true, false })
{
    var stalled = new DelaySelectedReadAccess(original, "slow");
    var guarded = new SaveSessionService(stalled, new SaveEngine());
    await guarded.OpenAsync(new PickedDocument("base", "synthetic"));
    var pending = guarded.OpenAsync(new PickedDocument("slow", "synthetic")).AsTask();
    await stalled.Entered.Task;
    if (invalidateByRevert)
        guarded.RevertToBaseline();
    else
        guarded.MarkWritten("base", original);
    stalled.Release.TrySetResult(true);
    await Refuses<OperationCanceledException>(async () => { await pending; },
        invalidateByRevert ? "slow open after revert" : "slow open after write notification");
    Check(guarded.Current?.Document.DocumentId == "base" && guarded.ProviderOrigin is null,
        "Invalidated slow open replaced baseline.");
    guarded.Close();
    Check(stalled.Writes == 0, "Lifecycle invalidation reached provider writer.");
}
Console.WriteLine($"Actual PKForge provider-origin session lifecycle: {checks} synthetic checks passed.");
Console.WriteLine("No physical Android provider, source identity certification or Save As performed.");

sealed class MemoryAccess(byte[] bytes) : ISaveFileAccess
{
    private readonly byte[] _bytes = bytes.ToArray();
    public int Writes { get; private set; }
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(_bytes.ToArray());
    }
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        Writes++;
        throw new InvalidOperationException("No writes allowed.");
    }
}

sealed class DelayFirstReadAccess(byte[] bytes) : ISaveFileAccess
{
    private readonly byte[] _bytes = bytes.ToArray();
    private int _reads;
    public int Writes { get; private set; }
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _reads) == 1)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
        }
        return _bytes.ToArray();
    }
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        Writes++;
        throw new InvalidOperationException("No writes allowed.");
    }
}

sealed class DelaySelectedReadAccess(byte[] bytes, string delayedId) : ISaveFileAccess
{
    private readonly byte[] _bytes = bytes.ToArray();
    public int Writes { get; private set; }
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (documentId == delayedId)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
        }
        return _bytes.ToArray();
    }
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        Writes++;
        throw new InvalidOperationException("No writes allowed.");
    }
}
