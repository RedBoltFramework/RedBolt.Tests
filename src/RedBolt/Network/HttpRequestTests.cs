// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Net;
using System.Net.Sockets;
using System.Text;

using RedBolt.Network;

public sealed class HttpRequestTests
{
    [Fact]
    public async Task Request_exposes_status_headers_and_non_blocking_response_stream()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = ServeOnce(listener);
        using var request = new HttpRequest($"http://127.0.0.1:{port}/test", "POST",
            ["X-Test: value"], "body");

        Assert.True(SpinWait.SpinUntil(
            () => request.State is HttpRequestState.Closed or HttpRequestState.Error,
            TimeSpan.FromSeconds(5)));
        Assert.Equal(HttpRequestState.Closed, request.State);
        Assert.Equal(string.Empty, request.Error);
        Assert.Equal(201, request.StatusCode);
        Assert.Equal("POST", request.Verb);
        Assert.Equal(5u, request.AvailableSize);

        var first = new byte[2];
        Assert.Equal(2u, request.Read(first));
        Assert.Equal("he", Encoding.UTF8.GetString(first));
        Assert.False(request.IsEof());

        var rest = new byte[8];
        Assert.Equal(3u, request.Read(rest));
        Assert.Equal("llo", Encoding.UTF8.GetString(rest, 0, 3));
        Assert.True(request.IsEof());
        Assert.Equal(0u, request.Seek(0));

        var receivedRequest = await serverTask;
        Assert.Contains("POST /test HTTP/1.1", receivedRequest);
        Assert.Contains("X-Test: value", receivedRequest, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("body", receivedRequest);
    }

    private static async Task<string> ServeOnce(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var requestBytes = new List<byte>();
        var buffer = new byte[1024];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer);
            requestBytes.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
            var request = Encoding.ASCII.GetString(requestBytes.ToArray());
            if (request.Contains("\r\n\r\nbody", StringComparison.Ordinal))
                break;
        }

        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 201 Created\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        await stream.WriteAsync(response);
        return Encoding.ASCII.GetString(requestBytes.ToArray());
    }
}
