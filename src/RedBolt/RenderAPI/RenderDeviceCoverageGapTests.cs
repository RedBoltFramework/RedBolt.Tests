namespace RedBolt.Tests;

using System.Reflection;

using RedBolt;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderDeviceCoverageGapTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RenderDeviceCoverageGapTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Render_device_capability_queries_and_multisample_selection_cover_fallbacks()
    {
        var device = _fixture.RenderDevice;
        var colorFormat = TextureFormat.TexFormatRgba8Unorm;
        var unknownFormat = TextureFormat.TexFormatUnknown;

        Assert.True(device.IsTextureFormatSupported(colorFormat));
        Assert.False(device.IsTextureFormatSupported(unknownFormat));
        Assert.True(device.IsRenderTargetFormatSupported(colorFormat));
        Assert.False(device.IsRenderTargetFormatSupported(unknownFormat));
        Assert.True(device.IsUnorderedAccessFormatSupported(colorFormat));
        Assert.False(device.IsUnorderedAccessFormatSupported(unknownFormat));
        Assert.True(device.IsMultiSampleSupported(colorFormat, 1));
        Assert.False(device.IsMultiSampleSupported(unknownFormat, 2));

        foreach (var requested in new[] { 0, 1, 3, 16, 64 })
        {
            var selected = device.GetSupportedMultiSample(colorFormat, requested);

            Assert.InRange(selected, 1, 16);
            Assert.Equal(0, selected & (selected - 1));
        }
    }

    [GraphicsFact]
    public void Render_device_fullscreen_helpers_cover_native_modes_and_refresh_gaps()
    {
        var nativeModes = RenderDevice.GetFullscreenModes(0);

        Assert.NotEmpty(nativeModes);
        Assert.Equal(nativeModes.Count, nativeModes.Distinct().Count());
        Assert.Equal(
            nativeModes.OrderBy(mode => mode.Size.X)
                .ThenBy(mode => mode.Size.Y)
                .ThenBy(mode => mode.RefreshRate),
            nativeModes);

        var modes = new[]
        {
            new FullscreenMode(new Vector2i(1280, 720), 60),
            new FullscreenMode(new Vector2i(1920, 1080), 30),
            new FullscreenMode(new Vector2i(1920, 1080), 144),
            new FullscreenMode(new Vector2i(2560, 1440), 60)
        };

        Assert.Equal(2, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(1600, 900), 60)));
        Assert.Equal(2, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(1920, 1080), 120)));
        Assert.Equal(modes[3], RenderDevice.GetClosestFullscreenMode(
            modes, new FullscreenMode(new Vector2i(3840, 2160), 240)));
    }

    [GraphicsFact]
    public void Render_device_present_consumes_dirty_texture_parameters_and_updates_stats()
    {
        var device = _fixture.RenderDevice;
        var originalFilter = device.GetDefaultTextureFilterMode();
        var originalAnisotropy = device.GetDefaultTextureAnisotropy();
        var firstFrame = device.GetFrameIndex();

        try
        {
            device.SetDefaultTextureFilterMode(
                originalFilter == TextureFilterMode.Bilinear
                    ? TextureFilterMode.Nearest
                    : TextureFilterMode.Bilinear);
            device.SetDefaultTextureAnisotropy(originalAnisotropy + 1);

            ref var stats = ref device.GetRenderContext().GetMutableStats();
            stats.NumPrimitives = 7;
            stats.NumDraws = 5;
            stats.NumDispatches = 3;

            device.Present();

            Assert.Equal((long)firstFrame + 1, (long)device.GetFrameIndex());
            Assert.Equal(7, device.GetStats().NumPrimitives);
            Assert.Equal(5, device.GetStats().NumDraws);
            Assert.Equal(3, device.GetStats().NumDispatches);
        }
        finally
        {
            device.SetDefaultTextureFilterMode(originalFilter);
            device.SetDefaultTextureAnisotropy(originalAnisotropy);
            device.Present();
        }
    }

    [GraphicsFact]
    public void Raw_texture_mips_memory_and_restore_lifecycle_cover_valid_gpu_paths()
    {
        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget,
            new Vector3i(8, 8, 1));
        using var texture = new RenderDeviceCoverageRawTexture(parameters);

        Assert.True(texture.GetHandles().IsValid());
        Assert.True(texture.CalculateMemoryUseGPU() > 0);
        Assert.True(texture.CreateForTest(parameters));

        texture.MarkDirty();
        Assert.True(texture.GetLevelsDirty());
        texture.GenerateLevels();
        Assert.False(texture.GetLevelsDirty());

        texture.Invalidate();
        Assert.False(texture.GetHandles().IsValid());
        Assert.Equal(0ul, texture.CalculateMemoryUseGPU());

        texture.Restore();
        Assert.True(texture.GetHandles().IsValid());
        Assert.True(texture.IsDataLost());
        texture.ClearDataLost();
        Assert.False(texture.IsDataLost());
    }

    [GraphicsFact]
    public void Raw_texture_array_and_volume_uavs_cover_view_dimensions_and_access_modes()
    {
        using var arrayTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2DArray,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess,
            new Vector3i(4, 4, 1),
            ArraySize: 2,
            NumLevels: 1));

        Assert.True(arrayTexture.GetHandles().IsValid());
        Assert.Equal(2, arrayTexture.GetHandles().RenderSurfaces.Length);

        var readOnly = arrayTexture.CreateUAV(new RawTextureUAVKey(
            CanWrite: false, CanRead: true, FirstSlice: 1, NumSlices: 1, NumLevels: 1));
        var writeOnly = arrayTexture.CreateUAV(new RawTextureUAVKey(
            CanWrite: true, CanRead: false, FirstSlice: 0, NumSlices: 1, NumLevels: 1));

        Assert.NotNull(readOnly);
        Assert.NotNull(writeOnly);
        Assert.Equal(
            global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex2dArray,
            readOnly!.GetDesc().TextureDim);

        using var volumeTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture3D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindUnorderedAccess,
            new Vector3i(4, 4, 2),
            NumLevels: 1));

        Assert.True(volumeTexture.GetHandles().IsValid());
        var volumeView = volumeTexture.CreateUAV(new RawTextureUAVKey(
            CanWrite: true, CanRead: true, FirstSlice: 0, NumSlices: 1, NumLevels: 1));
        Assert.NotNull(volumeView);
        Assert.Equal(
            global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex3d,
            volumeView!.GetDesc().TextureDim);
    }

    [GraphicsFact]
    public void Raw_texture_native_handle_import_covers_external_texture_metadata()
    {
        using var glTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess,
            new Vector3i(4, 4, 1)));

        using var imported = new RenderDeviceCoverageRawTexture();
        Assert.True(imported.CreateFromHandle(
            glTexture.GetHandles().Texture!, TextureFormat.TexFormatUnknown, msaaLevel: 1));
        Assert.Equal(TextureType.Texture2D, imported.GetParams().Type);
        Assert.True((imported.GetParams().Flags & TextureFlag.BindRenderTarget) != 0);
        Assert.True((imported.GetParams().Flags & TextureFlag.BindUnorderedAccess) != 0);
    }

    [GraphicsFact]
    public void Raw_texture_uav_validation_and_read_guards_cover_invalid_ranges()
    {
        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindUnorderedAccess,
            new Vector3i(8, 8, 1),
            NumLevels: 3));

        Assert.True(texture.GetHandles().IsValid());
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(CanWrite: false, CanRead: false)));
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(FirstSlice: 1)));
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(FirstLevel: 3)));
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(NumSlices: 2)));
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(NumLevels: 99)));

        var alias = new RawTextureUAVKey(FirstLevel: 1, NumLevels: 0);
        var aliasView = texture.CreateUAV(alias);
        Assert.NotNull(aliasView);
        Assert.Same(aliasView, texture.CreateUAV(alias with { NumLevels = 2 }));

        Assert.False(texture.Read(0, 3, new byte[256]));
        Assert.False(texture.Read(1, 0, new byte[256]));
        Assert.False(texture.Read(0, 0, new byte[1]));
    }

    [GraphicsFact]
    public void Raw_texture_update_validates_ranges_strides_and_repacking()
    {
        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatR8Unorm,
            TextureFlag.None,
            new Vector3i(4, 2, 1),
            NumLevels: 1));

        Assert.True(texture.GetHandles().IsValid());
        Assert.Throws<ArgumentOutOfRangeException>(() => texture.Update(
            1, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[8]));
        Assert.Throws<ArgumentOutOfRangeException>(() => texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 1, new byte[8]));
        Assert.Throws<ArgumentOutOfRangeException>(() => texture.Update(
            0, Vector3i.Zero, new Vector3i(5, 2, 1), 0, new byte[10]));
        Assert.Throws<ArgumentOutOfRangeException>(() => texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[6], rowStride: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[8], rowStride: 4, sliceStride: 7));
        Assert.Throws<ArgumentException>(() => texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[7], rowStride: 4, sliceStride: 8));

        texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0,
            Enumerable.Range(0, 10).Select(static value => (byte)value).ToArray(),
            rowStride: 5, sliceStride: 10);
    }

    [GraphicsFact]
    public void Raw_buffer_graphics_paths_cover_scratch_mapping_restore_and_immutable_updates()
    {
        using var staticBuffer = new RawBuffer(
            new RawBufferParams(BufferType.Vertex, 32, 4, BufferFlag.None));
        var mapped = staticBuffer.Map();
        mapped.Fill(0x4a);
        staticBuffer.Unmap();

        using var shadowed = new RawBuffer(
            new RawBufferParams(BufferType.Vertex, 16, 4, BufferFlag.Shadowed));
        shadowed.Update(new byte[16]);
        shadowed.Invalidate();
        shadowed.Restore();
        Assert.NotNull(shadowed.GetHandle());

        using var immutable = new RawBuffer(
            new RawBufferParams(BufferType.Vertex, 16, 4, BufferFlag.Immutable),
            new byte[16]);
        immutable.UpdateRange(new byte[16], 0, 16);

        SetPrivateField<object?>(shadowed, "_lastUpdateFrameIndex", null);
        SetPrivateField(shadowed, "_needsResolve", true);
        shadowed.Resolve();
    }

    private static void SetPrivateField<T>(object instance, string name, T value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderDeviceCoverageGapHeadlessTests
{
    public RenderDeviceCoverageGapHeadlessTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Raw_texture_headless_lifecycle_covers_uninitialized_gpu_paths()
    {
        using var texture = new RenderDeviceCoverageRawTexture();

        Assert.Null(texture.GetRenderDevice());
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey()));
        Assert.False(texture.Read(0, 0, Array.Empty<byte>()));
        Assert.Equal(0ul, texture.CalculateMemoryUseGPU());
        Assert.Throws<InvalidOperationException>(() => texture.Update(
            0, Vector3i.Zero, Vector3i.One, 0, Array.Empty<byte>()));

        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.None,
            new Vector3i(8, 4, 1));
        Assert.True(texture.CreateForTest(parameters));
        Assert.False(texture.GetHandles().IsValid());
        Assert.False(texture.Read(0, 0, Array.Empty<byte>()));

        texture.MarkDirty();
        Assert.True(texture.GetLevelsDirty());
        texture.GenerateLevels();
        Assert.True(texture.GetLevelsDirty());
        texture.Update(0, Vector3i.Zero, new Vector3i(8, 4, 1), 0, new byte[8 * 4 * 4]);
        texture.Destroy();
        Assert.Equal(0ul, texture.CalculateMemoryUseGPU());

        Assert.Throws<ArgumentNullException>(() => texture.CreateFromHandle(null!));
#if DEBUG
        Assert.Throws<InvalidOperationException>(() => texture.CreateFromGLTexture(
            0, TextureType.Texture2D, TextureFlag.None, TextureFormat.TexFormatRgba8Unorm, 1, 1));
#else
        Assert.False(texture.CreateFromGLTexture(
            0, TextureType.Texture2D, TextureFlag.None, TextureFormat.TexFormatRgba8Unorm, 1, 1));
#endif
    }

    [Fact]
    public void Raw_texture_parameter_validation_covers_each_dimension_and_binding_guard()
    {
        var invalidParameters = new[]
        {
            new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(0, 4, 1)),
            new RawTextureParams(TextureType.TextureCube, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(0, 4, 1)),
            new RawTextureParams(TextureType.Texture3D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(4, 4, 0)),
            new RawTextureParams(TextureType.Texture2DArray, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(4, 4, 1), ArraySize: 0),
            new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget | TextureFlag.BindDepthStencil, new Vector3i(4, 4, 1)),
            new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(4, 4, 1), MultiSample: 4)
        };

        foreach (var parameters in invalidParameters)
        {
            using var texture = new RenderDeviceCoverageRawTexture();
            Assert.False(texture.CreateForTest(parameters));
        }
    }

    [Fact]
    public void Raw_texture_headless_update_validates_compressed_alignment_and_strides()
    {
        using var compressed = new RenderDeviceCoverageRawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatBc1Unorm,
            TextureFlag.None,
            new Vector3i(8, 8, 1),
            NumLevels: 1));

        Assert.Throws<ArgumentException>(() => compressed.Update(
            0, new Vector3i(1, 0, 0), new Vector3i(4, 4, 1), 0, new byte[8]));

        using var texture = new RenderDeviceCoverageRawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatR8Unorm,
            TextureFlag.None,
            new Vector3i(4, 2, 1),
            NumLevels: 1));

        // A headless texture validates its region before returning, but it has
        // no GPU handles, so stride validation is intentionally skipped.
        texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[6], rowStride: 3);
        texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[8], rowStride: 4, sliceStride: 7);
        texture.Update(
            0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[7], rowStride: 4, sliceStride: 8);
    }
}

internal sealed class RenderDeviceCoverageRawTexture : RawTexture
{
    public RenderDeviceCoverageRawTexture()
    {
    }

    public RenderDeviceCoverageRawTexture(RawTextureParams parameters)
        : base(parameters)
    {
    }

    public bool CreateForTest(RawTextureParams parameters) => Create(parameters);
}
