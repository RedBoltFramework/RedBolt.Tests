// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.LibDataChannel;

public sealed class LibDataChannelBindingsTests
{
    [Fact]
    public void Peer_connection_constructs_and_deletes()
    {
        LibDataChannel.RtcPreload();
        var peerConnection = -1;
        try
        {
            using var configuration = new RtcConfiguration();
            peerConnection = LibDataChannel.RtcCreatePeerConnection(configuration);

            Assert.True(peerConnection >= 0);
            Assert.Equal(0, LibDataChannel.RtcClosePeerConnection(peerConnection));
            Assert.Equal(0, LibDataChannel.RtcDeletePeerConnection(peerConnection));
            peerConnection = -1;
        }
        finally
        {
            if (peerConnection >= 0)
            {
                LibDataChannel.RtcClosePeerConnection(peerConnection);
                LibDataChannel.RtcDeletePeerConnection(peerConnection);
            }
            LibDataChannel.RtcCleanup();
        }
    }
}
