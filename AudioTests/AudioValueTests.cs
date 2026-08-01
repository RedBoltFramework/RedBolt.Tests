namespace RedBolt.Tests;

using RedBolt.Audio;

public sealed class AudioValueTests
{
    [Fact]
    public void Audio_definitions_preserve_native_names_and_channel_order()
    {
        Assert.Equal("Master", AudioDefs.SoundMaster);
        Assert.Equal("Effect", AudioDefs.SoundEffect);
        Assert.Equal("Ambient", AudioDefs.SoundAmbient);
        Assert.Equal("Voice", AudioDefs.SoundVoice);
        Assert.Equal("Music", AudioDefs.SoundMusic);

        Assert.Equal(0, (int)SpeakerMode.Auto);
        Assert.Equal(1, (int)SpeakerMode.Mono);
        Assert.Equal(2, (int)SpeakerMode.Stereo);
        Assert.Equal(3, (int)SpeakerMode.Quadrophonic);
        Assert.Equal(4, (int)SpeakerMode.Surround51);
        Assert.Equal(5, Enum.GetValues<SpeakerMode>().Length);
    }

    [Fact]
    public void Audio_event_payloads_are_zero_initialized_and_copyable()
    {
        var finished = new AudioEvents.SoundFinishedEvent();
        var updated = new AudioEvents.RecordingUpdatedEvent();
        var started = new AudioEvents.RecordingStartedEvent();
        var ended = new AudioEvents.RecordingEndedEvent();

        Assert.Null(finished.Node);
        Assert.Null(finished.SoundSource);
        Assert.Null(finished.Sound);
        Assert.Null(updated.Microphone);
        Assert.Equal(0, updated.DataLength);
        Assert.False(updated.ClearData);
        Assert.Null(started.Microphone);
        Assert.Null(ended.Microphone);
        Assert.Equal(0, ended.DataLength);
        Assert.False(ended.ClearData);

        var copy = updated;
        copy.DataLength = 128;
        copy.ClearData = true;
        Assert.Equal(0, updated.DataLength);
        Assert.False(updated.ClearData);
    }
}
