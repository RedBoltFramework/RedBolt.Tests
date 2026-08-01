namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.IO;

public sealed class IOCoverageGapTests
{
    [Fact]
    public void Vector_buffer_covers_growth_shrink_clear_pinning_and_stream_copy_paths()
    {
        using var buffer = new VectorBuffer();
        Assert.Equal("VectorBuffer", buffer.Name);
        Assert.Equal("VectorBuffer", buffer.GetName());
        Assert.Equal(nint.Zero, buffer.GetData());

        buffer.SetData([1, 2]);
        Assert.Equal(2u, buffer.Size);
        Assert.Equal(3u, buffer.Write([3, 4, 5]));
        Assert.Equal(new byte[] { 3, 4, 5 }, buffer.GetBuffer());
        Assert.Equal(3u, buffer.Position);
        Assert.Equal(3u, buffer.Seek(uint.MaxValue));

        var pinned = buffer.GetData();
        Assert.NotEqual(0, pinned);
        Assert.Equal(3, Marshal.ReadByte(pinned));
        Assert.Equal(pinned, buffer.GetModifiableData());

        buffer.Resize(2);
        Assert.Equal(2u, buffer.Size);
        Assert.Equal(2u, buffer.Position);
        buffer.Clear();
        Assert.Equal(0u, buffer.Size);
        Assert.Equal(0u, buffer.Position);

        using var source = new MemoryBuffer([7, 8, 9]);
        using var copied = new VectorBuffer(source, 2);
        Assert.Equal(new byte[] { 7, 8 }, copied.GetBuffer());

        copied.SetData(nint.Zero, 10);
        Assert.Empty(copied.GetBuffer());
        copied.Dispose();
        copied.Dispose();
    }

    [Fact]
    public void Memory_buffer_string_read_only_and_bounded_write_paths_are_stable()
    {
        using var text = new MemoryBuffer("ž");
        Assert.True(text.IsReadOnly);
        Assert.Equal("ž", text.ReadString());

        using var readOnly = new MemoryBuffer(new ReadOnlyMemory<byte>([1, 2]));
        Assert.Equal(2u, readOnly.Write([3, 4, 5]));
        Assert.Equal(2u, readOnly.Position);
        Assert.Equal(new byte[] { 3, 4 }, new byte[] { Marshal.ReadByte(readOnly.GetData()), Marshal.ReadByte(readOnly.GetData(), 1) });

        using var vector = new VectorBuffer([4, 5, 6]);
        using var writable = new MemoryBuffer(vector);
        Assert.False(writable.IsReadOnly);
        writable.Seek(1);
        Span<byte> readback = stackalloc byte[4];
        Assert.Equal(2u, writable.Read(readback));
        writable.SeekRelative(-1);
        Assert.Equal(6, writable.ReadUByte());
    }

    [Theory]
    [InlineData("/a/./b/../c", "file", "/a/c")]
    [InlineData("C:\\a\\b", "file", "C:/a/b")]
    [InlineData("file:///a/b", "file", "/a/b")]
    [InlineData("file://relative", "file", "/relative")]
    [InlineData("http://host/path", "http", "host/path")]
    [InlineData("relative/path", "", "relative/path")]
    public void File_identifier_uri_and_path_normalization_cover_scheme_variants(string uri, string scheme, string fileName)
    {
        var identifier = FileIdentifier.FromUri(uri);
        Assert.Equal(scheme, identifier.Scheme);
        Assert.Equal(fileName, identifier.FileName);
        Assert.True((bool)identifier);
    }

    [Fact]
    public void File_identifier_append_uri_compare_and_special_file_paths_cover_edges()
    {
        Assert.True(FileIdentifier.FromUri("file:a").IsEmpty());
        Assert.True(FileIdentifier.FromUri("file:////a").IsEmpty());
        Assert.Equal("", FileIdentifier.Empty.ToUri());
        Assert.Equal("file:///tmp/data", new FileIdentifier("file", "/tmp/data").ToUri());
        Assert.Equal("file:///tmp/data", new FileIdentifier("file", "tmp/data").ToUri());
        Assert.Equal("http://host/path", new FileIdentifier("http", "host/path").ToUri());

        var first = new FileIdentifier("scheme", "root");
        first.AppendPath("");
        first.AppendPath("child");
        first.AppendPath("/leaf");
        Assert.Equal("root/child/leaf", first.FileName);

        var second = new FileIdentifier("scheme", "root/");
        second.AppendPath("/leaf");
        Assert.Equal("root/leaf", second.FileName);
        Assert.True(first > FileIdentifier.Empty);
        Assert.Equal(first, (FileIdentifier)first.Clone());
        Assert.Equal(first.ToUri(), first.ToString());

        Assert.Equal("/a/c", FileIdentifier.SanitizeFileName("/a/./b/../c/"));
        Assert.Equal("c", FileIdentifier.SanitizeFileName("../../a/../../c"));
        Assert.Equal("/", FileIdentifier.SanitizeFileName("////"));
    }
}
