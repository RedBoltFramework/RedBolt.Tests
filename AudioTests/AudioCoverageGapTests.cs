namespace RedBolt.Tests;

using RedBolt.Audio;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AudioCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public AudioCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Sound_begin_load_dispatches_raw_and_empty_data_fallbacks()
    {
        var raw = new Sound { Name = "AudioCoverage/raw.bin" };
        using (var source = new MemoryBuffer([1, 2, 0xFF]))
        {
            Assert.True(raw.BeginLoad(source));
        }

        Assert.Equal(3u, raw.GetDataSize());
        Assert.Equal([1, 2, unchecked((sbyte)0xFF)], raw.GetData());
        Assert.False(raw.IsCompressed());

        var empty = new Sound { Name = "AudioCoverage/empty.raw" };
        using (var source = new MemoryBuffer(Array.Empty<byte>()))
        {
            Assert.True(empty.BeginLoad(source));
        }

        Assert.NotNull(empty.GetData());
        Assert.Empty(empty.GetData()!);
        Assert.Equal(0u, empty.GetDataSize());
        Assert.Equal(0.0f, empty.GetLength());
        Assert.False(empty.IsLooped());

        Assert.Throws<ArgumentNullException>(() => empty.BeginLoad(null!));
    }

    [Fact]
    public void Sound_wav_loader_handles_formats_chunks_and_invalid_headers()
    {
        var eightBit = new Sound();
        using (var source = new MemoryBuffer(BuildWav(
                   audioFormat: 1,
                   channels: 1,
                   frequency: 8000,
                   bits: 8,
                   data: [0, 128, 255],
                   extraFormatBytes: [0xAA, 0xBB],
                   addUnknownChunk: true)))
        {
            Assert.True(eightBit.LoadWav(source));
        }

        Assert.Equal(8000u, eightBit.GetIntFrequency());
        Assert.Equal(1u, eightBit.GetSampleSize());
        Assert.False(eightBit.IsSixteenBit());
        Assert.False(eightBit.IsStereo());
        Assert.Equal(
            [unchecked((sbyte)-128), 0, 127],
            eightBit.GetData()![..3]);

        var sixteenBitStereo = new Sound();
        using (var source = new MemoryBuffer(BuildWav(
                   audioFormat: 1,
                   channels: 2,
                   frequency: 44100,
                   bits: 16,
                   data: [0, 1, 2, 3, 4, 5, 6, 7],
                   addUnknownChunk: true)))
        {
            Assert.True(sixteenBitStereo.LoadWav(source));
        }

        Assert.Equal(44100u, sixteenBitStereo.GetIntFrequency());
        Assert.Equal(4u, sixteenBitStereo.GetSampleSize());
        Assert.True(sixteenBitStereo.IsSixteenBit());
        Assert.True(sixteenBitStereo.IsStereo());
        Assert.Equal(
            [unchecked((sbyte)0), 1, 2, 3, 4, 5, 6, 7],
            sixteenBitStereo.GetData()![..8]);

        var emptyData = new Sound();
        using (var source = new MemoryBuffer(BuildWav(
                   audioFormat: 1,
                   channels: 1,
                   frequency: 22050,
                   bits: 8,
                   data: [])))
        {
            Assert.True(emptyData.LoadWav(source));
        }

        Assert.Equal(0u, emptyData.GetDataSize());
        Assert.Empty(emptyData.GetData()!);
        Assert.Equal(22050u, emptyData.GetIntFrequency());

        using var badRiff = new MemoryBuffer(BuildWav(
            audioFormat: 1,
            channels: 1,
            frequency: 8000,
            bits: 8,
            data: [1],
            riff: "NOPE"));
        Assert.False(new Sound().LoadWav(badRiff));

        using var badWave = new MemoryBuffer(BuildWav(
            audioFormat: 1,
            channels: 1,
            frequency: 8000,
            bits: 8,
            data: [1],
            wave: "NOPE"));
        Assert.False(new Sound().LoadWav(badWave));

        using var unsupportedFormat = new MemoryBuffer(BuildWav(
            audioFormat: 3,
            channels: 1,
            frequency: 8000,
            bits: 8,
            data: [1]));
        Assert.False(new Sound().LoadWav(unsupportedFormat));

        using var noDataChunk = new MemoryBuffer(BuildWav(
            audioFormat: 1,
            channels: 1,
            frequency: 8000,
            bits: 8,
            data: [],
            includeDataChunk: false,
            addUnknownChunk: true));
        Assert.False(new Sound().LoadWav(noDataChunk));
    }

    [Fact]
    public void Sound_loop_alignment_and_interpolation_guards_cover_empty_and_bounded_data()
    {
        var sound = new Sound();
        sound.SetData(Enumerable.Range(0, 12).Select(static value => (byte)value).ToArray(), 12);
        sound.SetFormat(1000, sixteenBit: true, stereo: true);

        sound.SetLoop(3, 11);
        Assert.True(sound.IsLooped());
        Assert.Equal(0, sound.GetRepeatOffset());
        Assert.Equal(8, sound.GetEndOffset());
        Assert.Equal(4u, sound.GetSampleSize());
        Assert.Equal(0.003f, sound.GetLength());

        var data = sound.GetData()!;
        Assert.Equal(data[..4], data[8..12]);

        sound.SetLoop(100, 100);
        Assert.Equal(12, sound.GetRepeatOffset());
        Assert.Equal(12, sound.GetEndOffset());

        sound.SetLooped(false);
        Assert.False(sound.IsLooped());
        Assert.Equal(12, sound.GetEndOffset());
        Assert.Equal([0, 0, 0, 0], sound.GetData()![12..16]);

        sound.SetLooped(true);
        Assert.True(sound.IsLooped());
        Assert.Equal([0, 1, 2, 3], sound.GetData()![12..16]);

        sound.SetFormat(0, sixteenBit: false, stereo: false);
        Assert.Equal(0.0f, sound.GetLength());

        var empty = new Sound();
        empty.SetSize(0);
        empty.SetLooped(true);
        Assert.True(empty.IsLooped());
        empty.SetLooped(false);
        Assert.False(empty.IsLooped());
        empty.SetData([1, 2], 0);
        Assert.Null(empty.GetData());
    }

    [Fact]
    public void Buffered_sound_stream_handles_empty_inputs_short_buffers_and_partial_reads()
    {
        var stream = new BufferedSoundStream();
        stream.SetFormat(1000, sixteenBit: true, stereo: false);
        stream.AddData((byte[])null!, 0);
        stream.AddData([9, 9], 0);
        stream.AddData(new short[] { 0x1234, -2 }, 4);
        stream.AddData([7, 8], 2);

        Assert.Equal(6u, stream.GetBufferNumBytes());
        Assert.Equal(0.003f, stream.GetBufferLength());
        Assert.Equal(0u, stream.GetData([], 0));

        var first = new sbyte[3];
        Assert.Equal(3u, stream.GetData(first, 3));
        Assert.Equal([unchecked((sbyte)0x34), 0x12, unchecked((sbyte)0xFE)], first);
        Assert.Equal(3u, stream.GetBufferNumBytes());

        var second = new sbyte[3];
        Assert.Equal(3u, stream.GetData(second, 3));
        Assert.Equal([unchecked((sbyte)0xFF), 7, 8], second);
        Assert.Equal(0u, stream.GetBufferNumBytes());

        stream.Clear();
        Assert.Equal(0.0f, stream.GetBufferLength());
        Assert.Equal(0u, stream.GetData(new sbyte[1], 1));
    }

    [Fact]
    public void Audio_uninitialized_playback_and_gain_pause_paths_have_safe_fallbacks()
    {
        var audio = _fixture.Engine.GetSubsystem<Audio>()!;

        Assert.False(audio.IsInitialized());
        Assert.False(audio.IsPlaying());
        Assert.False(audio.Play());

        var output = new short[] { 1, 2, 3, 4 };
        audio.MixOutput(output, 2);
        Assert.Equal([0, 0, 0, 0], output);

        Assert.Equal(1.0f, audio.GetMasterGain("AudioCoverage/unknown"));
        Assert.False(audio.HasMasterGain("AudioCoverage/unknown"));
        Assert.Equal(1.0f, audio.GetSoundSourceMasterGain(default));
        Assert.Equal(1.0f, audio.GetSoundSourceMasterGain(new StringHash(AudioDefs.SoundMaster)));
        Assert.Equal(1.0f, audio.GetSoundSourceMasterGain(new StringHash("AudioCoverage/unknown")));

        audio.SetMasterGain("AudioCoverage/Music", -1.0f);
        Assert.Equal(0.0f, audio.GetMasterGain("AudioCoverage/Music"));
        Assert.True(audio.HasMasterGain("AudioCoverage/Music"));
        audio.SetMasterGain("AudioCoverage/Music", 0.25f);
        Assert.Equal(0.25f, audio.GetMasterGain("AudioCoverage/Music"));
        Assert.Equal(0.25f, audio.GetSoundSourceMasterGain(new StringHash("AudioCoverage/Music")));

        audio.PauseSoundType("AudioCoverage/Music");
        Assert.True(audio.IsSoundTypePaused("AudioCoverage/Music"));
        audio.ResumeSoundType("AudioCoverage/never-paused");
        Assert.True(audio.IsSoundTypePaused("AudioCoverage/Music"));
        audio.ResumeSoundType("AudioCoverage/Music");
        Assert.False(audio.IsSoundTypePaused("AudioCoverage/Music"));
        audio.PauseSoundType("AudioCoverage/Music");
        audio.ResumeAll();
        Assert.False(audio.IsSoundTypePaused("AudioCoverage/Music"));

        audio.Stop();
        audio.Update(0.1f);
        audio.SetListener(null);
        Assert.Null(audio.GetListener());
    }

    [Fact]
    public void Sound_source_attributes_clamp_values_and_use_resource_fallbacks()
    {
        var audio = _fixture.Engine.GetSubsystem<Audio>()!;
        var scene = new Scene();
        var node = scene.CreateChild("AudioCoverageSource");
        var source = node.CreateComponent<SoundSource>();
        var sound = CreateSound([1, 2, 3, 4], frequency: 1000);
        sound.Name = "AudioCoverage/attribute.wav";
        Assert.True(_fixture.Resources.AddManualResource(sound));

        Assert.Equal(ResourceRef.Empty, source.GetSoundAttr());
        source.SetSoundAttr(new ResourceRef(nameof(Sound), sound.Name));
        Assert.Same(sound, source.GetSound());
        Assert.Equal(new ResourceRef(nameof(Sound), sound.Name), source.GetSoundAttr());

        source.SetFrequency(-1.0f);
        Assert.Equal(0.0f, source.GetFrequency());
        source.SetFrequency(999999.0f);
        Assert.Equal(535232.0f, source.GetFrequency());
        source.SetGain(-1.0f);
        Assert.Equal(0.0f, source.GetGain());
        source.SetAttenuation(-1.0f);
        Assert.Equal(0.0f, source.GetAttenuation());
        source.SetAttenuation(2.0f);
        Assert.Equal(1.0f, source.GetAttenuation());
        source.SetPanning(-2.0f);
        Assert.Equal(-1.0f, source.GetPanning());
        source.SetPanning(2.0f);
        Assert.Equal(1.0f, source.GetPanning());
        source.SetReach(-2.0f);
        Assert.Equal(-1.0f, source.GetReach());
        source.SetReach(2.0f);
        Assert.Equal(1.0f, source.GetReach());
        source.SetLowFrequency(true);
        Assert.True(source.IsLowFrequency());
        source.SetAutoRemoveMode(AutoRemoveMode.Node);
        Assert.Equal(AutoRemoveMode.Node, source.GetAutoRemoveMode());
        source.SetIgnoreSceneTimeScale(true);
        Assert.True(source.GetIgnoreSceneTimeScale());

        source.SetSoundType(AudioDefs.SoundMaster);
        Assert.Equal(AudioDefs.SoundEffect, source.GetSoundType());
        source.SetSoundType("AudioCoverage/Music");
        Assert.Equal("AudioCoverage/Music", source.GetSoundType());
        audio.SetMasterGain("AudioCoverage/Music", 0.5f);
        source.UpdateMasterGain();

        source.SetPlayingAttr(true);
        Assert.True(source.IsPlaying());
        source.SetPositionAttr(1);
        Assert.Equal(1, source.GetPositionAttr());
        source.SetPlayingAttr(false);
        Assert.False(source.IsPlaying());

        source.Play(sound);
        source.SetSoundAttr(new ResourceRef(nameof(Sound), "AudioCoverage/missing.wav"));
        Assert.Null(source.GetSound());
        Assert.False(source.IsPlaying());
        source.SetSoundAttr(ResourceRef.Empty);
        Assert.Null(source.GetSound());
    }

    [Fact]
    public void Sound_source_seek_play_position_update_and_autoremove_paths_are_deterministic()
    {
        var scene = new Scene();
        var node = scene.CreateChild("AudioCoverageSeek");
        var source = node.CreateComponent<SoundSource>();
        var sound = CreateSound([1, 2, 3, 4, 5, 6, 7, 8], sixteenBit: true, frequency: 1000);

        source.Play(sound);
        source.Seek(-1.0f);
        Assert.Equal(0, source.GetPlayPosition());
        source.Seek(0.003f);
        Assert.Equal(6, source.GetPlayPosition());
        source.SetPlayPosition(1);
        Assert.Equal(2, source.GetPlayPosition());
        source.SetPlayPosition(999);
        Assert.Equal(8, source.GetPlayPosition());
        Assert.Equal(0.004f, source.GetTimePosition());
        source.SetPositionAttr(-1);
        Assert.Equal(0, source.GetPositionAttr());
        source.Stop();
        source.Seek(0.002f);
        Assert.Equal(4, source.GetPlayPosition());
        source.Play((SoundStream?)null);
        source.Play((Sound?)null);
        Assert.False(source.IsPlaying());

        var looped = CreateSound([1, 2], frequency: 1000);
        looped.SetLooped(true);
        source.Play(looped);
        source.Update(looped.GetLength() * 1.5f);
        Assert.True(source.IsPlaying());
        Assert.InRange(source.GetTimePosition(), 0.0f, looped.GetLength());

        var finished = node.CreateComponent<SoundSource>();
        finished.SetAutoRemoveMode(AutoRemoveMode.Disabled);
        finished.SetIgnoreSceneTimeScale(true);
        finished.Play(CreateSound([1], frequency: 1000));
        finished.Mix(new int[2], 2, 1000, SpeakerMode.Mono, interpolation: false);
        Assert.False(finished.IsPlaying());
        finished.Update(0.0f);
        Assert.False(finished.IsPlaying());
        Assert.Same(node, finished.TryGetNode());

        var componentNode = scene.CreateChild("AudioCoverageComponentRemove");
        var componentSource = componentNode.CreateComponent<SoundSource>();
        componentSource.SetAutoRemoveMode(AutoRemoveMode.Component);
        componentSource.SetIgnoreSceneTimeScale(true);
        componentSource.Play(CreateSound([1], frequency: 1000));
        componentSource.Mix(new int[2], 2, 1000, SpeakerMode.Mono, interpolation: false);
        Assert.False(componentSource.IsPlaying());
        componentSource.Update(0.0f);
        Assert.Null(componentSource.TryGetNode());

        var nodeRemove = scene.CreateChild("AudioCoverageNodeRemove");
        var nodeSource = nodeRemove.CreateComponent<SoundSource>();
        nodeSource.SetAutoRemoveMode(AutoRemoveMode.Node);
        nodeSource.SetIgnoreSceneTimeScale(true);
        nodeSource.Play(CreateSound([1], frequency: 1000));
        nodeSource.Mix(new int[2], 2, 1000, SpeakerMode.Mono, interpolation: false);
        Assert.False(nodeSource.IsPlaying());
        nodeSource.Update(0.0f);
        Assert.Same(nodeRemove, nodeSource.TryGetNode());
        Assert.Null(nodeRemove.GetScene());
    }

    [Fact]
    public void Sound_source_mix_covers_channel_modes_low_frequency_and_interpolation()
    {
        var scene = new Scene();

        var noPlayback = scene.CreateChild("AudioCoverageNoPlayback").CreateComponent<SoundSource>();
        var untouched = new int[] { 7 };
        noPlayback.Mix(untouched, 1, 44100, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([7], untouched);

        var monoSound = CreateSound([10, 20, 30, 40]);
        var monoSource = scene.CreateChild("AudioCoverageMono").CreateComponent<SoundSource>();
        monoSource.Play(monoSound);
        var monoOutput = new int[2];
        monoSource.Mix(monoOutput, 2, 44100, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([10, 20], monoOutput);

        monoSource.Stop();
        monoSource.Play(monoSound);
        var stereoOutput = new int[4];
        monoSource.SetPanning(0.5f);
        monoSource.Mix(stereoOutput, 2, 44100, SpeakerMode.Stereo, interpolation: false);
        Assert.Equal([5, 15, 10, 30], stereoOutput);

        monoSource.Stop();
        monoSource.Play(monoSound);
        monoSource.SetPanning(0.0f);
        monoSource.SetReach(1.0f);
        var quadOutput = new int[4];
        monoSource.Mix(quadOutput, 1, 44100, SpeakerMode.Quadrophonic, interpolation: false);
        Assert.Equal([20, 20, 0, 0], quadOutput);

        monoSource.Stop();
        monoSource.Play(monoSound);
        var surroundOutput = new int[6];
        monoSource.Mix(surroundOutput, 1, 44100, SpeakerMode.Surround51, interpolation: false);
        Assert.Equal([20, 20, 20, 0, 0, 0], surroundOutput);

        monoSource.Stop();
        monoSource.Play(monoSound);
        monoSource.SetLowFrequency(true);
        var skippedLowFrequency = new int[2];
        monoSource.Mix(skippedLowFrequency, 1, 44100, SpeakerMode.Stereo, interpolation: false);
        Assert.Equal([0, 0], skippedLowFrequency);

        monoSource.Stop();
        monoSource.Play(monoSound);
        var lfeOutput = new int[6];
        monoSource.Mix(lfeOutput, 1, 44100, SpeakerMode.Surround51, interpolation: false);
        Assert.Equal(10, lfeOutput[3]);

        var stereoSound = CreateSound([10, 20, 30, 40], stereo: true);
        var stereoSource = scene.CreateChild("AudioCoverageStereo").CreateComponent<SoundSource>();
        stereoSource.Play(stereoSound);
        var downmix = new int[1];
        stereoSource.Mix(downmix, 1, 44100, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([15], downmix);

        stereoSource.Stop();
        stereoSource.Play(stereoSound);
        var directStereo = new int[2];
        stereoSource.Mix(directStereo, 1, 44100, SpeakerMode.Stereo, interpolation: false);
        Assert.Equal([10, 20], directStereo);

        stereoSource.Stop();
        stereoSource.Play(stereoSound);
        var stereoQuad = new int[4];
        stereoSource.Mix(stereoQuad, 1, 44100, SpeakerMode.Quadrophonic, interpolation: false);
        Assert.Equal([10, 20, 10, 20], stereoQuad);

        stereoSource.Stop();
        stereoSource.Play(stereoSound);
        var stereoSurround = new int[6];
        stereoSource.Mix(stereoSurround, 1, 44100, SpeakerMode.Surround51, interpolation: false);
        Assert.Equal([10, 20, 0, 0, 10, 20], stereoSurround);

        var interpolationSound = CreateSound([10, 20]);
        var interpolationSource = scene.CreateChild("AudioCoverageInterpolation").CreateComponent<SoundSource>();
        interpolationSource.Play(interpolationSound);
        interpolationSource.SetFrequency(22050.0f);
        var interpolated = new int[2];
        interpolationSource.Mix(interpolated, 2, 44100, SpeakerMode.Mono, interpolation: true);
        Assert.Equal([10, 15], interpolated);

        interpolationSource.Stop();
        interpolationSource.Play(interpolationSound);
        var invalidMode = new int[] { 3 };
        interpolationSource.Mix(invalidMode, 1, 44100, SpeakerMode.Auto, interpolation: false);
        Assert.Equal([3], invalidMode);
    }

    [Fact]
    public void Sound_source_stream_mix_handles_partial_data_and_stop_at_end()
    {
        var scene = new Scene();
        var source = scene.CreateChild("AudioCoverageStream").CreateComponent<SoundSource>();
        var stream = new ArraySoundStream([5, 6, 7, 8], stopAtEnd: true);

        source.Play(stream);
        Assert.True(source.IsPlaying());
        Assert.Null(source.GetSound());
        Assert.Equal(8000.0f, source.GetFrequency());

        var first = new int[2];
        source.Mix(first, 2, 8000, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([5, 6], first);
        Assert.True(source.IsPlaying());

        var second = new int[1];
        source.Mix(second, 1, 8000, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([7], second);
        Assert.False(source.IsPlaying());

        var emptySource = scene.CreateChild("AudioCoverageEmptyStream").CreateComponent<SoundSource>();
        emptySource.Play(new ArraySoundStream([], stopAtEnd: true));
        var emptyOutput = new int[1];
        emptySource.Mix(emptyOutput, 1, 8000, SpeakerMode.Mono, interpolation: false);
        Assert.Equal([0], emptyOutput);
        Assert.False(emptySource.IsPlaying());
    }

    [Fact]
    public void Sound_source_3d_attenuation_covers_listener_and_geometry_fallbacks()
    {
        var audio = _fixture.Engine.GetSubsystem<Audio>()!;
        var standalone = new SoundSource3D();
        standalone.CalculateAttenuation();
        Assert.Equal(0.0f, standalone.GetAttenuation());
        standalone.Dispose();

        var scene = new Scene();
        var sourceNode = scene.CreateChild("AudioCoverage3DSource");
        var source = sourceNode.CreateComponent<SoundSource3D>();
        var listenerNode = scene.CreateChild("AudioCoverage3DListener");
        var listener = listenerNode.CreateComponent<SoundListener>();

        audio.SetListener(null);
        source.CalculateAttenuation();
        Assert.Equal(0.0f, source.GetAttenuation());

        audio.SetListener(listener);
        listener.SetEnabled(false);
        source.CalculateAttenuation();
        Assert.Equal(0.0f, source.GetAttenuation());

        listener.SetEnabled(true);
        source.SetDistanceAttenuation(-1.0f, -2.0f, -1.0f);
        Assert.Equal(0.0f, source.GetNearDistance());
        Assert.Equal(0.0f, source.GetFarDistance());
        Assert.Equal(0.1f, source.RollAngleoffFactor());
        source.SetAngleAttenuation(-1.0f, 999.0f);
        Assert.Equal(0.0f, source.GetInnerAngle());
        Assert.Equal(360.0f, source.GetOuterAngle());
        source.SetNearDistance(2.0f);
        source.SetFarDistance(10.0f);
        source.SetRolloffFactor(1.0f);
        source.SetAngleAttenuation(30.0f, 60.0f);
        sourceNode.SetPosition(new Vector3f(0.0f, 0.0f, 1.0f));
        listenerNode.SetPosition(Vector3f.Zero);
        source.CalculateAttenuation();
        Assert.InRange(source.GetAttenuation(), 0.0f, 1.0f);

        source.SetAngleAttenuation(360.0f, 360.0f);
        source.SetDistanceAttenuation(2.0f, 1.0f, 1.0f);
        sourceNode.SetPosition(new Vector3f(0.0f, 0.0f, 1.0f));
        source.CalculateAttenuation();
        Assert.Equal(1.0f, source.GetAttenuation());
        sourceNode.SetPosition(new Vector3f(0.0f, 0.0f, 3.0f));
        source.CalculateAttenuation();
        Assert.Equal(0.0f, source.GetAttenuation());

        var otherScene = new Scene();
        var otherListener = otherScene.CreateChild().CreateComponent<SoundListener>();
        audio.SetListener(otherListener);
        source.CalculateAttenuation();
        Assert.Equal(0.0f, source.GetAttenuation());
        audio.SetListener(listener);

        var debug = new DebugRenderer();
        source.SetAngleAttenuation(20.0f, 40.0f);
        source.DrawDebugGeometry(debug, depthTest: true);
        Assert.True(debug.HasContent());
    }

    private static Sound CreateSound(
        byte[] data,
        bool sixteenBit = false,
        bool stereo = false,
        uint frequency = 44100)
    {
        var sound = new Sound();
        sound.SetData(data, (uint)data.Length);
        sound.SetFormat(frequency, sixteenBit, stereo);
        return sound;
    }

    private static byte[] BuildWav(
        ushort audioFormat,
        ushort channels,
        uint frequency,
        ushort bits,
        byte[] data,
        byte[]? extraFormatBytes = null,
        bool addUnknownChunk = false,
        bool includeDataChunk = true,
        string riff = "RIFF",
        string wave = "WAVE")
    {
        var format = new List<byte>();
        AddUShort(format, audioFormat);
        AddUShort(format, channels);
        AddUInt(format, frequency);
        AddUInt(format, checked(frequency * (uint)channels * (uint)(bits / 8)));
        AddUShort(format, checked((ushort)((uint)channels * (uint)(bits / 8))));
        AddUShort(format, bits);
        if (extraFormatBytes is not null)
            format.AddRange(extraFormatBytes);

        var chunks = new List<byte>();
        AddChunk(chunks, "fmt ", format.ToArray());
        if (addUnknownChunk)
            AddChunk(chunks, "JUNK", [0x11, 0x22, 0x33]);
        if (includeDataChunk)
            AddChunk(chunks, "data", data);

        var result = new List<byte>();
        AddFourCC(result, riff);
        AddUInt(result, (uint)(4 + chunks.Count));
        AddFourCC(result, wave);
        result.AddRange(chunks);
        return result.ToArray();
    }

    private static void AddChunk(List<byte> destination, string id, byte[] data)
    {
        AddFourCC(destination, id);
        AddUInt(destination, (uint)data.Length);
        destination.AddRange(data);
    }

    private static void AddFourCC(List<byte> destination, string value)
    {
        Assert.Equal(4, value.Length);
        destination.AddRange(value.Select(static character => (byte)character));
    }

    private static void AddUShort(List<byte> destination, ushort value)
    {
        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
    }

    private static void AddUInt(List<byte> destination, uint value)
    {
        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
        destination.Add((byte)(value >> 16));
        destination.Add((byte)(value >> 24));
    }

    private sealed class ArraySoundStream : SoundStream
    {
        private readonly sbyte[] _data;
        private int _position;

        public ArraySoundStream(IEnumerable<sbyte> data, bool stopAtEnd)
        {
            _data = data.ToArray();
            SetFormat(8000, sixteenBit: false, stereo: false);
            SetStopAtEnd(stopAtEnd);
        }

        public override bool Seek(uint sampleNumber)
        {
            _position = (int)global::System.Math.Min(sampleNumber * GetSampleSize(), (uint)_data.Length);
            return true;
        }

        public override uint GetData(sbyte[] dest, uint numBytes)
        {
            var count = global::System.Math.Min((int)numBytes, _data.Length - _position);
            if (count <= 0)
                return 0;

            Array.Copy(_data, _position, dest, 0, count);
            _position += count;
            return (uint)count;
        }
    }
}
