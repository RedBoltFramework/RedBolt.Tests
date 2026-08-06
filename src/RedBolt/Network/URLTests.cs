namespace RedBolt.Tests;

using RedBolt.Network;

public sealed class URLTests
{
    [Fact]
    public void Encode_and_decode_match_rbfx()
    {
        const string decoded = "Abcd123!@#$%^&*()_+-={}|\":<>?[]';,./~`";
        const string encoded = "Abcd123%21%40%23%24%25%5E%26%2A%28%29_%2B-%3D%7B%7D%7C%22%3A%3C%3E%3F%5B%5D%27%3B%2C.%2F~%60";

        Assert.Equal(encoded, URL.Encode(decoded));
        Assert.Equal(decoded, URL.Decode(encoded));
        Assert.Equal("B", URL.Decode("%4142"));
    }

    [Fact]
    public void URL_is_disassembled_and_assembled()
    {
        Assert.False(new URL());

        var url = new URL("foo://beak:sharp@example.com:8042/over/there?name=ferret#nose");

        Assert.True(url);
        Assert.Equal("foo", url.Scheme);
        Assert.Equal("beak", url.User);
        Assert.Equal("sharp", url.Password);
        Assert.Equal("example.com", url.Host);
        Assert.Equal((ushort)8042, url.Port);
        Assert.Equal("/over/there", url.Path);
        Assert.Equal("name=ferret", url.Query);
        Assert.Equal("nose", url.Hash);
        Assert.Equal("foo://beak:sharp@example.com:8042/over/there?name=ferret#nose", url.ToString());
    }

    [Fact]
    public void Default_ports_are_parsed_but_omitted_when_formatted()
    {
        var http = new URL("http://example.com/path");
        var https = new URL("https://example.com");

        Assert.Equal((ushort)80, http.Port);
        Assert.Equal("http://example.com/path", http.ToString());
        Assert.Equal((ushort)443, https.Port);
        Assert.Equal("https://example.com", https.ToString());
    }
}
