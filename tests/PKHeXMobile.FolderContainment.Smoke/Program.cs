// Portable, synthetic folder-provider spy. No Android provider, ROM, save or bank asset.
using PKForge.App;
using PKForge.Domain;
using PKHeXMobile.Domain;

var assertions = 0;
void Check(bool result, string reason)
{
    if (!result) throw new Exception(reason);
    assertions++;
}
async Task Refuses<T>(Func<Task> action, string reason) where T : Exception
{
    try { await action(); }
    catch (T) { assertions++; return; }
    throw new Exception(reason);
}

var bytes = new byte[] { 2, 3, 5, 7, 11, 13 };
var provider = new SpyFolder(bytes);
var access = new ReadOnlyFolderFileAccess(provider);
var files = await access.ListFilesAsync("tree");
Check(files.Count == 1 && files[0].DisplayName == "synthetic.bnk", "Folder listing changed");
Check(provider.ListCount == 1, "Folder list was not delegated");
var result = (await access.ReadFileAsync("document")).ToArray();
Check(result.AsSpan().SequenceEqual(bytes), "Folder import changed bytes");
result[0] ^= 0xFF;
Check(bytes[0] == 2, "Folder import aliases provider storage");
Check(provider.ReadCount == 1, "Folder read not delegated");

foreach (var name in new[] { "existing.bnk", "manifest.json", "../parent", "" })
{
    await Refuses<UnsafeSaveWriteException>(() =>
        access.WriteFileAsync("tree", name, bytes).AsTask(), "Folder export unexpectedly allowed");
}
await Refuses<UnsafeSaveWriteException>(() =>
    access.WriteFileAsync("tree", "empty.bnk", ReadOnlyMemory<byte>.Empty).AsTask(),
    "Empty export unexpectedly allowed");
Check(provider.WriteCount == 0, "Folder provider received a write");
Check(bytes.AsSpan().SequenceEqual(new byte[] { 2, 3, 5, 7, 11, 13 }),
    "Folder source was mutated");

var oversized = new SpyFolder(new byte[SaveInputSnapshot.MaxInputBytes + 1]);
await Refuses<InvalidDataException>(() =>
    new ReadOnlyFolderFileAccess(oversized).ReadFileAsync("document").AsTask(),
    "Oversized folder import unexpectedly allowed");
Check(oversized.ReadCount == 1 && oversized.WriteCount == 0, "Oversized import reached writer");
var empty = new SpyFolder([]);
await Refuses<InvalidDataException>(() =>
    new ReadOnlyFolderFileAccess(empty).ReadFileAsync("document").AsTask(),
    "Empty folder import unexpectedly allowed");
var invalid = new SpyFolder(bytes);
await Refuses<ArgumentException>(() =>
    new ReadOnlyFolderFileAccess(invalid).ReadFileAsync(" ").AsTask(),
    "Blank document ID unexpectedly accepted");
Check(invalid.ReadCount == 0, "Invalid document ID reached provider");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    var preCancelled = new SpyFolder(bytes);
    await Refuses<OperationCanceledException>(() =>
        new ReadOnlyFolderFileAccess(preCancelled).ReadFileAsync("document", cancelled.Token).AsTask(),
        "Cancelled read reached provider");
    Check(preCancelled.ReadCount == 0, "Cancelled read opened provider");
    await Refuses<OperationCanceledException>(() =>
        new ReadOnlyFolderFileAccess(preCancelled).ListFilesAsync("tree", cancelled.Token).AsTask(),
        "Cancelled listing reached provider");
    Check(preCancelled.ListCount == 0, "Cancelled listing opened provider");
}
Console.WriteLine($"PKForge folder read-only containment: {assertions} synthetic assertions passed.");
Console.WriteLine("No Android picker, real provider, write publication or device certification.");

sealed class SpyFolder(byte[] bytes) : IFolderFileAccess
{
    public int ListCount { get; private set; }
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }

    public ValueTask<IReadOnlyList<PickedDocument>> ListFilesAsync(
        string treeId, CancellationToken cancellationToken = default)
    {
        ListCount++;
        IReadOnlyList<PickedDocument> files = [new PickedDocument("document", "synthetic.bnk")];
        return ValueTask.FromResult(files);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(bytes);
    }

    public ValueTask WriteFileAsync(
        string treeId, string fileName, ReadOnlyMemory<byte> candidate,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;
        throw new Exception("Underlying folder provider must not receive writes");
    }
}
