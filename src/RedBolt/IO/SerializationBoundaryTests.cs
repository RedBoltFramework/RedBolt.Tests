namespace RedBolt.Tests;

using RedBolt.IO;

public sealed class SerializationBoundaryTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(127u)]
    [InlineData(128u)]
    [InlineData(16384u)]
    [InlineData(uint.MaxValue)]
    public void Variable_length_integers_roundtrip_at_encoding_boundaries(uint value)
    {
        using var buffer = new VectorBuffer();

        Assert.True(buffer.WriteVLE(value));
        var encodedSize = buffer.Size;
        buffer.Seek(0);

        Assert.Equal(value, buffer.ReadVLE());
        Assert.Equal(encodedSize, buffer.Position);
        Assert.True(buffer.Eof);
    }

    [Fact]
    public void Strings_stop_at_nul_and_roundtrip_utf8_and_empty_lines()
    {
        using var buffer = new VectorBuffer();
        Assert.True(buffer.WriteString("before\0after"));
        Assert.True(buffer.WriteString("žąalias"));
        Assert.True(buffer.WriteLine(string.Empty));

        buffer.Seek(0);

        Assert.Equal("before", buffer.ReadString());
        Assert.Equal("žąalias", buffer.ReadString());
        Assert.Equal(string.Empty, buffer.ReadLine());
        Assert.True(buffer.Eof);
    }

    [Fact]
    public void Short_reads_do_not_cross_the_end_of_a_buffer()
    {
        using var buffer = new VectorBuffer([0x7f]);

        Assert.Equal((byte)0x7f, buffer.ReadUByte());
        Assert.Equal(1u, buffer.Position);
        Assert.Equal(0u, buffer.Read([0xaa, 0xbb]));
        Assert.Equal(1u, buffer.Position);
        Assert.True(buffer.Eof);
    }
}
