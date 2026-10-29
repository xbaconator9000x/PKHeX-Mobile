/// <summary>A provider-like stream that denies seeking/Length and returns short chunks.</summary>
internal sealed class NonSeekableReadStream : Stream
{
    private readonly MemoryStream _source;
    public NonSeekableReadStream(byte[] bytes) => _source = new MemoryStream(bytes, writable: false);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException("Length is unavailable.");
    public override long Position
    {
        get => throw new NotSupportedException("Position is unavailable.");
        set => throw new NotSupportedException("Position is unavailable.");
    }
    public override int Read(byte[] buffer, int offset, int count) =>
        _source.Read(buffer, offset, Math.Min(count, 7));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _source.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
    public override ValueTask DisposeAsync() => _source.DisposeAsync();
}
