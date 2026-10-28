// Exercises both read-only services with the pinned PKForge.Domain contracts.
// No Android provider, private save or actual file mutation is used.
using PKForge.App;
using PKForge.Domain;

var original = new byte[] { 3, 5, 8, 13, 21, 34 };
var bytes = original.ToArray();
var snapshot = new SaveSnapshot("synthetic", 0, bytes, Array.Empty<SlotSummary>(), "synthetic");
var writer = new ReadOnlySaveWriter();
var spy = new SpyFileAccess(bytes);
var access = new ReadOnlySaveFileAccess(spy);
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Refuses(Action action, string label)
{
    try { action(); }
    catch (UnsafeSaveWriteException ex)
    {
        Check(ex.Message.Contains("disabled"), label + ": missing refusal reason");
        return;
    }
    throw new Exception(label + ": write was unexpectedly allowed");
}

var read = access.ReadAsync("source").AsTask().GetAwaiter().GetResult();
Check(read.Span.SequenceEqual(original), "read delegation changed bytes");
Check(spy.ReadCount == 1, "read was not delegated");
Refuses(() => writer.WriteAsync("source", snapshot, new byte[] { 1, 2 })
    .AsTask().GetAwaiter().GetResult(), "ordinary write");
Refuses(() => writer.WriteScopedAsync("source", snapshot, new byte[] { 1, 2 },
    WriteScope.Only(new SlotRef(0, 0))).AsTask().GetAwaiter().GetResult(), "scoped write");
Refuses(() => writer.WriteScopedAsync("source", snapshot, bytes,
    WriteScope.Everything).AsTask().GetAwaiter().GetResult(), "unrestricted restore");
Refuses(() => writer.WriteAsync("source", snapshot, bytes)
    .AsTask().GetAwaiter().GetResult(), "unchanged candidate");
writer.ConfirmLayoutRisk("source");
Refuses(() => writer.WriteAsync("source", snapshot, new byte[] { 9 })
    .AsTask().GetAwaiter().GetResult(), "confirmed risk");
Check(writer.WhyWritesAreRefused("source", snapshot) == ReadOnlySaveWriter.Reason,
    "UI did not receive refusal explanation");
Refuses(() => access.WriteAtomicallyAsync("source", new byte[] { 9 })
    .AsTask().GetAwaiter().GetResult(), "source bypass");
Refuses(() => access.WriteAtomicallyAsync("alias", bytes)
    .AsTask().GetAwaiter().GetResult(), "alias bypass");
Refuses(() => access.WriteAtomicallyAsync("source", ReadOnlyMemory<byte>.Empty)
    .AsTask().GetAwaiter().GetResult(), "empty write bypass");
Check(spy.WriteCount == 0, "provider received a write");
Check(bytes.AsSpan().SequenceEqual(original), "source bytes changed");
Console.WriteLine($"PKForge read-only writer containment passed: {checks} assertions.");
Console.WriteLine("No source document, Android provider or Save As was used.");

sealed class SpyFileAccess(byte[] source) : ISaveFileAccess
{
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(source.ToArray());
    }
    public ValueTask WriteAtomicallyAsync(
        string documentId, ReadOnlyMemory<byte> candidate,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;
        throw new Exception("Underlying provider must never be called for writes");
    }
}
