namespace RedBolt.Tests;

using RedBolt.IO;

public sealed class MemoryBufferBoundaryTests
{
    [Fact]
    public void Read_and_write_are_bounded_and_seek_clamps_to_size()
    {
        var source = new byte[] { 1, 2, 3 };
        using var buffer = new MemoryBuffer(source);
        Span<byte> destination = stackalloc byte[5];

        Assert.Equal(3u, buffer.Read(destination));
        Assert.Equal(3u, buffer.Position);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination[..3].ToArray());
        Assert.Equal(3u, buffer.Seek(uint.MaxValue));

        Assert.Equal(0u, buffer.Seek(0));
        Assert.Equal(3u, buffer.Write([4, 5, 6, 7]));
        Assert.Equal(new byte[] { 4, 5, 6 }, source);
        Assert.Equal(3u, buffer.Position);
    }

    [Fact]
    public void Sliced_read_only_memory_preserves_slice_bounds_and_clone_state()
    {
        var source = new byte[] { 9, 8, 7, 6 };
        using var buffer = new MemoryBuffer(source.AsMemory(1, 2));
        buffer.SetName("slice");
        buffer.Seek(1);

        using var clone = (MemoryBuffer)buffer.Clone();

        Assert.True(buffer.IsReadOnly);
        Assert.Equal(2u, buffer.Size);
        Assert.Equal(1u, clone.Position);
        Assert.Equal("slice", clone.Name);
        clone.Seek(0);
        Assert.Equal(8u, clone.ReadUByte());
        Assert.Equal(7u, clone.ReadUByte());
        Assert.Equal(2u, clone.Position);
    }

    [Fact]
    public unsafe void Unmanaged_and_null_pointers_have_defined_lifetime_and_size()
    {
        using var empty = new MemoryBuffer(nint.Zero, uint.MaxValue);
        Assert.Equal(0u, empty.Size);
        Assert.Equal(0u, empty.Position);
        Assert.True(empty.Eof);

        byte* bytes = stackalloc byte[2];
        bytes[0] = 11;
        bytes[1] = 22;
        var buffer = new MemoryBuffer((nint)bytes, 2);

        Assert.Equal(11, buffer.ReadUByte());
        Assert.Equal(1u, buffer.Position);
        buffer.Dispose();
        buffer.Dispose();
    }
}
