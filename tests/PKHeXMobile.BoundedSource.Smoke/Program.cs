// Synthetic provider-spy tests for the exact PKForge read-only adapter.
// No Android provider, private save, writer or retail format is certified.
using PKForge.App;
using PKForge.Domain;
using PKHeXMobile.Domain;

var checks = 0;
void Check(bool condition, string reason)
{
    if (!condition) throw new Exception(reason);
    checks++;
}
async Task Refuses<T>(Func<Task> action, string reason) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new Exception(reason);
}

var original = new byte[] { 1, 3, 5, 7, 9, 11 };
var stable = new FakeProvider(original);
var access = new ReadOnlySaveFileAccess(stable);
var copy = (await access.ReadAsync("source")).ToArray();
Check(copy.AsSpan().SequenceEqual(original) && stable.ReadCount == 2,
    "Stable source must be independently read twice");
copy[0] ^= 0xFF;
Check(original[0] == 1, "Returned bytes must not alias the source");

var changed = original.ToArray();
changed[3] ^= 0x40;
var race = new FakeProvider(original) { SecondBytes = changed };
await Refuses<InvalidDataException>(() =>
    new ReadOnlySaveFileAccess(race).ReadAsync("source").AsTask(),
    "Changed second read was promoted");
Check(race.ReadCount == 2 && race.WriteCount == 0, "Changed source reached a writer");

var revoked = new FakeProvider(original) { FailSecond = true };
await Refuses<IOException>(() =>
    new ReadOnlySaveFileAccess(revoked).ReadAsync("source").AsTask(),
    "Revoked second read was promoted");
Check(revoked.ReadCount == 2 && revoked.WriteCount == 0, "Revocation reached a writer");

var oversized = new FakeProvider(new byte[SaveInputSnapshot.MaxInputBytes + 1]);
await Refuses<InvalidDataException>(() =>
    new ReadOnlySaveFileAccess(oversized).ReadAsync("source").AsTask(),
    "Oversized input was promoted");
Check(oversized.ReadCount == 1, "Oversized source should not be opened twice");

var empty = new FakeProvider([]);
await Refuses<InvalidDataException>(() =>
    new ReadOnlySaveFileAccess(empty).ReadAsync("source").AsTask(),
    "Empty input was promoted");
Check(empty.ReadCount == 1, "Empty source should not be opened twice");

var invalidId = new FakeProvider(original);
await Refuses<ArgumentException>(() =>
    new ReadOnlySaveFileAccess(invalidId).ReadAsync("  ").AsTask(),
    "Blank provider ID was accepted");
Check(invalidId.ReadCount == 0, "Invalid source ID opened provider");

using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    var preCancelled = new FakeProvider(original);
    await Refuses<OperationCanceledException>(() =>
        new ReadOnlySaveFileAccess(preCancelled).ReadAsync("source", cancelled.Token).AsTask(),
        "Pre-cancelled source opened");
    Check(preCancelled.ReadCount == 0, "Pre-cancelled provider was opened");
}
using (var cancelled = new CancellationTokenSource())
{
    var during = new FakeProvider(original) { AfterFirstRead = cancelled.Cancel };
    await Refuses<OperationCanceledException>(() =>
        new ReadOnlySaveFileAccess(during).ReadAsync("source", cancelled.Token).AsTask(),
        "Cancellation after first read was ignored");
    Check(during.ReadCount == 1, "Cancelled source was opened a second time");
}
await Refuses<UnsafeSaveWriteException>(() =>
    access.WriteAtomicallyAsync("source", original).AsTask(),
    "Source writer was unexpectedly enabled");
Check(stable.WriteCount == 0, "Provider received a write");

Console.WriteLine($"Bounded two-open PKForge source smoke: {checks} synthetic assertions passed.");
Console.WriteLine("Not a device, retail-layout, same-version desktop or Save As certification.");

sealed class FakeProvider(byte[] source) : ISaveFileAccess
{
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public byte[]? SecondBytes { get; init; }
    public bool FailSecond { get; init; }
    public Action? AfterFirstRead { get; init; }

    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        if (ReadCount == 2 && FailSecond) throw new IOException("Synthetic provider revoke");
        var result = ReadCount == 2 && SecondBytes is not null ? SecondBytes : source;
        if (ReadCount == 1) AfterFirstRead?.Invoke();
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(result.ToArray());
    }

    public ValueTask WriteAtomicallyAsync(
        string documentId, ReadOnlyMemory<byte> candidate,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;
        throw new Exception("No provider writes are allowed");
    }
}
