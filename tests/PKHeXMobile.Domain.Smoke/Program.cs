using PKHeXMobile.Domain;

static void MustBlock(Action attempt)
{
    try { attempt(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected this write to be blocked.");
}

static async Task MustRejectAsync<TException>(Func<Task> attempt) where TException : Exception
{
    try { await attempt(); }
    catch (TException) { return; }
    throw new Exception($"Expected {typeof(TException).Name}.");
}

MustBlock(() => SaveWriteGuard.RequireAuthorized(new("unknown", SaveCompatibility.Unknown, null), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedReadOnly, "game:v1"), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, null), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 0, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 128, 64));
SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 128, 128);

var originalBytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
var original = SaveInputSnapshot.FromBytes(originalBytes);
if (original.Length != 256 || original.Fingerprint.Sha256Hex.Length != 64)
    throw new Exception("Snapshot length/hash incorrect.");
var copy = original.CopyBytes();
copy[0] ^= 0xFF;
originalBytes[0] ^= 0xFF;
if (original.CopyBytes()[0] != 0 || original.Matches(originalBytes))
    throw new Exception("Snapshot leaked mutable storage.");
if (!original.Matches(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray()))
    throw new Exception("Unmodified bytes must match the original fingerprint.");

await MustRejectAsync<InvalidDataException>(() => SaveInputSnapshot.ReadAsync(new MemoryStream()));
await MustRejectAsync<InvalidDataException>(() =>
    SaveInputSnapshot.ReadAsync(new MemoryStream(new byte[SaveInputSnapshot.MaxInputBytes + 1])));
var readBack = await SaveInputSnapshot.ReadAsync(new MemoryStream(original.CopyBytes()));
original.RequireUnchanged(readBack);

var providerBytes = original.CopyBytes();
var session = await SaveDocumentSession.OpenAsync(_ =>
    ValueTask.FromResult<Stream>(new MemoryStream(providerBytes, writable: false)));
await session.VerifySourceUnchangedAsync();

var providerIdentity = new DocumentSourceIdentity("org.example.provider", "opaque-id");
var identifiedSession = await SaveDocumentSession.OpenWithIdentityAsync(_ =>
    ValueTask.FromResult(new DocumentReadHandle(
        new NonSeekableReadStream(original.CopyBytes()), providerIdentity)));
if (identifiedSession.SourceIdentity != providerIdentity || identifiedSession.SourceIdentityConfirmed)
    throw new Exception("Provider identity must not be confirmed before re-opening.");
await identifiedSession.VerifySourceUnchangedAsync();
if (!identifiedSession.SourceIdentityConfirmed)
    throw new Exception("Stable provider identity should be confirmed after a second read.");
try
{
    _ = new DocumentSourceIdentity("", "opaque-id");
    throw new Exception("Empty provider key must be rejected.");
}
catch (ArgumentException) { }
try
{
    _ = new DocumentSourceIdentity("org.example.provider", " ");
    throw new Exception("Empty document key must be rejected.");
}
catch (ArgumentException) { }

providerBytes[4] ^= 0x40;
await MustRejectAsync<InvalidOperationException>(() => session.VerifySourceUnchangedAsync());
await MustRejectAsync<IOException>(() =>
    SaveDocumentSession.OpenAsync(_ => throw new IOException("Provider unavailable")));
await MustRejectAsync<InvalidDataException>(() =>
    SaveDocumentSession.OpenAsync(_ => ValueTask.FromResult<Stream>(new MemoryStream())));
await using (var nonSeekable = new NonSeekableReadStream(original.CopyBytes()))
{
    if (nonSeekable.CanSeek)
        throw new Exception("Test stream must deny seeking.");
    var snapshotFromProvider = await SaveInputSnapshot.ReadAsync(nonSeekable);
    original.RequireUnchanged(snapshotFromProvider);
}
var providerSession = await SaveDocumentSession.OpenAsync(_ =>
    ValueTask.FromResult<Stream>(new NonSeekableReadStream(original.CopyBytes())));
await providerSession.VerifySourceUnchangedAsync();
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    await MustRejectAsync<OperationCanceledException>(() =>
        SaveInputSnapshot.ReadAsync(new NonSeekableReadStream(original.CopyBytes()), cancelled.Token));
}
Console.WriteLine("Domain contract, bounded input, non-seekable and identity-aware provider, cancellation, copy isolation, stale-provider and failure tests passed.");
