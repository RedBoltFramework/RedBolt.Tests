// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Audio;
using RedBolt.IO;

public sealed class AudioValueTests
{
    private sealed class TestSoundStream : SoundStream
    {
        public override uint GetData(sbyte[] dest, uint numBytes) => 0;
    }

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

    [Fact]
    public void Sound_stream_defaults_to_44100_hz_and_rejects_seek()
    {
        var stream = new TestSoundStream();

        Assert.Equal(44100u, stream.GetIntFrequency());
        Assert.Equal(1u, stream.GetSampleSize());
        Assert.False(stream.GetStopAtEnd());
        Assert.False(stream.IsSixteenBit());
        Assert.False(stream.IsStereo());
        Assert.False(stream.Seek(12));
    }

    [Theory]
    [InlineData(false, false, 1u)]
    [InlineData(true, false, 2u)]
    [InlineData(false, true, 2u)]
    [InlineData(true, true, 4u)]
    public void Sound_stream_format_updates_sample_size_and_flags(bool sixteenBit, bool stereo, uint sampleSize)
    {
        var stream = new TestSoundStream();

        stream.SetFormat(22050, sixteenBit, stereo);
        stream.SetStopAtEnd(true);

        Assert.Equal(22050u, stream.GetIntFrequency());
        Assert.Equal(22050.0f, stream.GetFrequency());
        Assert.Equal(sampleSize, stream.GetSampleSize());
        Assert.Equal(sixteenBit, stream.IsSixteenBit());
        Assert.Equal(stereo, stream.IsStereo());
        Assert.True(stream.GetStopAtEnd());
    }

    [Fact]
    public void Buffered_sound_stream_reads_partial_buffers_and_clear_resets_length()
    {
        var stream = new BufferedSoundStream();
        stream.SetFormat(1000, sixteenBit: true, stereo: false);
        stream.AddData([1, 2, 3], 2);
        stream.AddData([4, 5], 2);

        Assert.Equal(4u, stream.GetBufferNumBytes());
        Assert.Equal(0.002f, stream.GetBufferLength());

        var first = new sbyte[3];
        Assert.Equal(3u, stream.GetData(first, 3));
        Assert.Equal([1, 2, 4], first);
        Assert.Equal(1u, stream.GetBufferNumBytes());

        stream.Clear();
        Assert.Equal(0u, stream.GetBufferNumBytes());
        Assert.Equal(0.0f, stream.GetBufferLength());
        Assert.Equal(0u, stream.GetData(new sbyte[1], 1));
    }

    [Fact]
    public void Sound_rejects_malformed_wav_and_aligns_loop_offsets()
    {
        var malformed = new Sound();
        Assert.False(malformed.LoadWav(new MemoryBuffer("not-a-wav"u8.ToArray())));

        var sound = new Sound();
        sound.SetData([1, 2, 3, 4], 4);
        sound.SetFormat(22050, sixteenBit: true, stereo: false);
        sound.SetLoop(1, 99);

        Assert.True(sound.IsLooped());
        Assert.Equal(0, sound.GetRepeatOffset());
        Assert.Equal(4, sound.GetEndOffset());
        Assert.Equal(4u, sound.GetDataSize());
        Assert.Equal(2u, sound.GetSampleSize());
        Assert.Equal(4.0f / 2.0f / 22050.0f, sound.GetLength());
    }
}
