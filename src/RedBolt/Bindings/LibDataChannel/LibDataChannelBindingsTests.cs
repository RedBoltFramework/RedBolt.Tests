namespace RedBolt.Tests;

using RedBolt.Bindings.LibDataChannel;

public sealed class LibDataChannelBindingsTests
{
    [Fact]
    public void Peer_connection_constructs_and_deletes()
    {
        LibDataChannelNativeBindings.RtcPreload();
        var peerConnection = -1;
        try
        {
            using var configuration = new RtcConfiguration();
            peerConnection = LibDataChannelNativeBindings.RtcCreatePeerConnection(configuration);

            Assert.True(peerConnection >= 0);
            Assert.Equal(0, LibDataChannelNativeBindings.RtcClosePeerConnection(peerConnection));
            Assert.Equal(0, LibDataChannelNativeBindings.RtcDeletePeerConnection(peerConnection));
            peerConnection = -1;
        }
        finally
        {
            if (peerConnection >= 0)
            {
                LibDataChannelNativeBindings.RtcClosePeerConnection(peerConnection);
                LibDataChannelNativeBindings.RtcDeletePeerConnection(peerConnection);
            }
            LibDataChannelNativeBindings.RtcCleanup();
        }
    }
}
