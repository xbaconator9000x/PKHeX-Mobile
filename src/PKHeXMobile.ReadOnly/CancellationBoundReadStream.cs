namespace PKHeXMobile.ReadOnly;

/// <summary>
/// A read-only stream whose lifetime is tied to an inspection cancellation.
/// On cancellation, attempt to close the underlying provider stream on a worker
/// thread. This can unblock cooperative providers, but a provider that ignores
/// descriptor closure can still hang; this is NOT a bounded-latency guarantee.
/// Never use this wrapper for save writes.
/// </summary>
public sealed class CancellationBoundReadStream : Stream
{
    private readonly Stream _inner;
    private readonly CancellationToken _lifetime;
    private readonly CancellationTokenRegistration _registration;
    private int _innerClosed;

    public CancellationBoundReadStream(Stream inner, CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (!inner.CanRead)
            throw new ArgumentException("The provider stream must be readable.", nameof(inner));
        lifetime.ThrowIfCancellationRequested();
        _inner = inner;
        _lifetime = lifetime;
        _registration = lifetime.Register(static state =>
        {
            // Do not block Android's Cancel button on a misbehaving provider.
            ThreadPool.QueueUserWorkItem(static item =>
            {
                try { ((CancellationBoundReadStream)item!).CloseInner(); }
                catch (Exception) { /* provider cleanup must not crash the process */ }
            }, state);
        }, this);
    }

    public override bool CanRead => Volatile.Read(ref _innerClosed) == 0 && _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _lifetime.ThrowIfCancellationRequested();
        try
        {
            int read = _inner.Read(buffer, offset, count);
            _lifetime.ThrowIfCancellationRequested();
            return read;
        }
        catch (Exception e) when ((e is IOException or ObjectDisposedException) &&
                                  _lifetime.IsCancellationRequested)
        {
            throw new OperationCanceledException("Provider read was cancelled.", e, _lifetime);
        }
    }

    public override int Read(Span<byte> buffer)
    {
        _lifetime.ThrowIfCancellationRequested();
        try
        {
            int read = _inner.Read(buffer);
            _lifetime.ThrowIfCancellationRequested();
            return read;
        }
        catch (Exception e) when ((e is IOException or ObjectDisposedException) &&
                                  _lifetime.IsCancellationRequested)
        {
            throw new OperationCanceledException("Provider read was cancelled.", e, _lifetime);
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _lifetime.ThrowIfCancellationRequested();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime, cancellationToken);
        try
        {
            int read = await _inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            return read;
        }
        catch (Exception e) when ((e is IOException or ObjectDisposedException) &&
                                  linked.IsCancellationRequested)
        {
            throw new OperationCanceledException("Provider read was cancelled.", e, linked.Token);
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private void CloseInner()
    {
        if (Interlocked.Exchange(ref _innerClosed, 1) == 0)
            _inner.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _registration.Dispose();
            CloseInner();
        }
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
