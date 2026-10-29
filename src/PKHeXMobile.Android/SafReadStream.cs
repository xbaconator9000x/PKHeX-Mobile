using Android.Content;
using Android.Content.Res;
using Android.OS;
using PKHeXMobile.ReadOnly;

namespace PKHeXMobile.Android;

/// <summary>
/// Read-only SAF descriptor opening with a provider-visible cancellation signal.
/// Ownership of the descriptor and stream transfers together to the caller.
/// The lifetime wrapper also attempts descriptor closure during blocked reads.
/// Providers can ignore cancellation and closure; device fault tests are required.
/// </summary>
internal static class SafReadStream
{
    public static Stream Open(ContentResolver resolver, global::Android.Net.Uri uri,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(uri);
        token.ThrowIfCancellationRequested();
        using var signal = new CancellationSignal();
        using var registration = token.Register(static state =>
            ((CancellationSignal)state!).Cancel(), signal);

        // "r" only. Do not request write grants, infer a filesystem path or
        // fall back to direct file access when a provider refuses this call.
        var descriptor = resolver.OpenAssetFileDescriptor(uri, "r", signal)
            ?? throw new IOException("The provider returned no read descriptor.");
        try
        {
            token.ThrowIfCancellationRequested();
            var stream = descriptor.CreateInputStream()
                ?? throw new IOException("The provider returned no read stream.");
            return new CancellationBoundReadStream(new OwnedReadStream(stream, descriptor), token);
        }
        catch
        {
            descriptor.Close();
            descriptor.Dispose();
            throw;
        }
    }

    private sealed class OwnedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly AssetFileDescriptor _descriptor;
        private int _disposed;

        public OwnedReadStream(Stream inner, AssetFileDescriptor descriptor)
        {
            _inner = inner;
            _descriptor = descriptor;
        }

        public override bool CanRead => _disposed == 0 && _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { _inner.Dispose(); }
                finally
                {
                    try { _descriptor.Close(); }
                    finally { _descriptor.Dispose(); }
                }
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
