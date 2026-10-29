namespace PKHeXMobile.ReadOnly.Smoke;

/// <summary>Pure synthetic provider that ignores cancellation until disposed.</summary>
internal sealed class BlockingReadStream : Stream
{
    private readonly TaskCompletionSource<int> _pending =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Started => _started.Task;
    public bool WasDisposed { get; private set; }
    public override bool CanRead => !WasDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _started.TrySetResult();
        return new ValueTask<int>(_pending.Task);
    }
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !WasDisposed)
        {
            WasDisposed = true;
            _pending.TrySetException(new IOException("Synthetic provider descriptor closed."));
        }
        base.Dispose(disposing);
    }
}
