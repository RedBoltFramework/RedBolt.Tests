namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RawTextureRenderDeviceRemainingCoverageTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RawTextureRenderDeviceRemainingCoverageTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Raw_texture_wraps_native_dimension_variants_and_bindings()
    {
        var device = _fixture.RenderDevice.GetRenderDevice()!;
        var variants = new[]
        {
            (ResourceDimension.ResourceDimTexCube, TextureType.TextureCube, 6u, 1u),
            (ResourceDimension.ResourceDimTex2dArray, TextureType.Texture2DArray, 2u, 1u),
            (ResourceDimension.ResourceDimTex3d, TextureType.Texture3D, 1u, 2u)
        };

        foreach (var (nativeType, expectedType, arraySize, depth) in variants)
        {
            using var description = new TextureDesc
            {
                Type = nativeType,
                Usage = Usage.UsageDefault,
                Format = TextureFormat.TexFormatRgba8Unorm,
                Width = 4,
                Height = 4,
                MipLevels = 1,
                SampleCount = 1,
                ArraySize = arraySize,
                BindFlags = BindFlags.BindShaderResource
            };
            if (nativeType == ResourceDimension.ResourceDimTex3d)
                description.Depth = depth;

            IRenderDevice_CreateTexture(device, description, null!, out var nativeTexture);
            Assert.NotNull(nativeTexture);
            try
            {
                using var wrapped = new RemainingCoverageRawTexture();

                Assert.True(wrapped.CreateFromHandle(
                    nativeTexture!, TextureFormat.TexFormatUnknown, msaaLevel: 1));
                Assert.Equal(expectedType, wrapped.GetParams().Type);
                var expectedArraySize = nativeType == ResourceDimension.ResourceDimTex3d
                    ? (int)depth
                    : arraySize == 1u ? 1 : (int)arraySize;
                Assert.Equal(expectedArraySize, wrapped.GetParams().ArraySize);
                Assert.NotNull(wrapped.GetHandles().ShaderResourceView);
            }
            finally
            {
                _ = IObject_Release(nativeTexture!);
            }
        }

        using var boundDescription = new TextureDesc
        {
            Type = ResourceDimension.ResourceDimTex2d,
            Usage = Usage.UsageDefault,
            Format = TextureFormat.TexFormatRgba8Unorm,
            Width = 4,
            Height = 4,
            MipLevels = 1,
            SampleCount = 1,
            ArraySize = 1,
            BindFlags = BindFlags.BindShaderResource |
                BindFlags.BindRenderTarget |
                BindFlags.BindUnorderedAccess
        };
        IRenderDevice_CreateTexture(device, boundDescription, null!, out var boundTexture);
        Assert.NotNull(boundTexture);
        try
        {
            using var wrapped = new RemainingCoverageRawTexture();

            Assert.True(wrapped.CreateFromHandle(
                boundTexture!, TextureFormat.TexFormatRgba8Unorm, msaaLevel: 1));
            Assert.NotNull(wrapped.GetHandles().RenderTargetView);
            Assert.NotNull(wrapped.GetHandles().UnorderedAccessView);
        }
        finally
        {
            _ = IObject_Release(boundTexture!);
        }
    }

    [GraphicsFact]
    public void Raw_texture_resolves_multisampled_surfaces_and_reuses_resolve_attributes()
    {
        var multiSample = _fixture.RenderDevice.GetSupportedMultiSample(
            TextureFormat.TexFormatRgba8Unorm, 4);
        if (multiSample <= 1)
            return;

        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget,
            new Vector3i(8, 8, 1),
            NumLevels: 1,
            MultiSample: multiSample));

        texture.MarkDirty();
        Assert.True(texture.GetResolveDirty());

        texture.Resolve();
        Assert.False(texture.GetResolveDirty());

        texture.MarkDirty();
        texture.Resolve();
        Assert.False(texture.GetResolveDirty());
    }

    [GraphicsFact]
    public void Raw_texture_reads_three_dimensional_data_and_uses_staging_depth()
    {
        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture3D,
            TextureFormat.TexFormatR8Unorm,
            TextureFlag.None,
            new Vector3i(4, 3, 2),
            NumLevels: 1));
        var source = Enumerable.Range(0, 4 * 3 * 2)
            .Select(static value => (byte)value)
            .ToArray();

        texture.Update(0, Vector3i.Zero, new Vector3i(4, 3, 2), 0, source);

        var destination = new byte[source.Length];
        Assert.True(texture.Read(0, 0, destination));
        Assert.Equal(source, destination);
    }

    [GraphicsFact]
    public void Raw_texture_update_after_invalidation_ignores_missing_gpu_handles()
    {
        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatR8Unorm,
            TextureFlag.None,
            new Vector3i(4, 2, 1),
            NumLevels: 1));

        texture.Invalidate();
        texture.Update(0, Vector3i.Zero, new Vector3i(4, 2, 1), 0, new byte[8]);

        Assert.False(texture.GetHandles().IsValid());
    }

    [GraphicsFact]
    public void Raw_texture_reuses_equal_creation_and_rejects_invalid_uav_access()
    {
        var equalParameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.None,
            new Vector3i(4, 4, 1),
            NumLevels: 1);
        using var texture = new RemainingCoverageRawTexture();

        Assert.True(texture.CreateForTest(equalParameters));
        var originalHandle = texture.GetHandles().Texture;
        Assert.True(texture.CreateForTest(equalParameters));
        Assert.Same(originalHandle, texture.GetHandles().Texture);
        Assert.Null(texture.CreateUAV(new RawTextureUAVKey(CanRead: false, CanWrite: false)));
    }

    [GraphicsFact]
    public void Raw_texture_marks_no_auto_resolve_multisampling_without_resolve_dirty()
    {
        var multiSample = _fixture.RenderDevice.GetSupportedMultiSample(
            TextureFormat.TexFormatRgba8Unorm, 4);
        if (multiSample <= 1)
            return;

        using var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget | TextureFlag.NoMultiSampledAutoResolve,
            new Vector3i(8, 8, 1),
            NumLevels: 0,
            MultiSample: multiSample));

        texture.MarkDirty();

        Assert.False(texture.GetLevelsDirty());
        Assert.False(texture.GetResolveDirty());
    }

    [GraphicsFact]
    public void Raw_texture_reports_unsupported_bindings_and_unknown_dimensions()
    {
        using var unsupported = new RemainingCoverageRawTexture();
        Assert.False(unsupported.CreateForTest(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindDepthStencil,
            new Vector3i(4, 4, 1),
            NumLevels: 1)));

        using var unknown = new RemainingCoverageRawTexture();
        Assert.Throws<ArgumentOutOfRangeException>(() => unknown.CreateForTest(new RawTextureParams(
            (TextureType)999,
            TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.None,
            new Vector3i(4, 4, 1),
            NumLevels: 1)));
    }

    [GraphicsFact]
    public void Render_device_screenshot_and_present_cover_software_backbuffer_paths()
    {
        var device = _fixture.RenderDevice;
        var originalSettings = device.GetWindowSettings();
        try
        {
            device.UpdateWindowSettings(originalSettings);

            Assert.True(device.TakeScreenShot(out var size, out var data));
            Assert.Equal(device.GetSwapChainSize(), size);
            Assert.Equal(size.X * size.Y * 4, data.Length);

            var settings = originalSettings;
            settings.VSync = true;
            device.UpdateWindowSettings(settings);
            device.Present();

            settings.VSync = false;
            device.UpdateWindowSettings(settings);
            device.Present();
        }
        finally
        {
            _fixture.ResetGraphics();
        }
    }

    [GraphicsFact]
    public void Render_device_multisampled_screenshot_resolves_software_backbuffer()
    {
        var multiSample = _fixture.RenderDevice.GetSupportedMultiSample(
            TextureFormat.TexFormatRgba8Unorm, 4);
        if (multiSample <= 1)
            return;

        _fixture.InitializeGraphics(new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RawTextureRenderDeviceRemainingCoverageTests",
            RenderBackend = TestGraphicsBackend.Current,
            MultiSample = multiSample,
            WindowWidth = 64,
            WindowHeight = 64
        });
        try
        {
            Assert.True(_fixture.RenderDevice.TakeScreenShot(out var size, out var data));
            Assert.Equal(size.X * size.Y * 4, data.Length);
        }
        finally
        {
            _fixture.ResetGraphics();
        }
    }

    [GraphicsFact]
    public void Render_device_normalizes_window_settings_and_handles_idempotent_invalidation()
    {
        var device = _fixture.RenderDevice;
        var normalized = device.GetWindowSettings();
        normalized.Monitor = -1;
        normalized.MultiSample = 32;
        normalized.Size = Vector2i.Zero;
        normalized.RefreshRate = 0;
        normalized.Mode = WindowMode.Windowed;
        normalized.Resizable = true;

        try
        {
            device.UpdateWindowSettings(normalized);

            var actual = device.GetWindowSettings();
            Assert.Equal(0, actual.Monitor);
            Assert.NotEqual(Vector2i.Zero, actual.Size);

            device.InvalidateDeviceState();
            device.InvalidateDeviceState();
            device.RestoreDeviceState();
        }
        finally
        {
            _fixture.ResetGraphics();
        }
    }

    [GraphicsFact]
    public void Render_device_fullscreen_selection_covers_size_and_refresh_ties()
    {
        var modes = new[]
        {
            new FullscreenMode(new Vector2i(1280, 720), 30),
            new FullscreenMode(new Vector2i(1280, 720), 60),
            new FullscreenMode(new Vector2i(1920, 1080), 60),
            new FullscreenMode(new Vector2i(1920, 1080), 144)
        };

        Assert.Equal(1, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(1280, 720), 75)));
        Assert.Equal(3, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(1600, 900), 120)));
        Assert.Equal(2, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(1600, 900), 240)));
        Assert.Equal(3, RenderDevice.GetClosestFullscreenModeIndex(
            modes, new FullscreenMode(new Vector2i(2560, 1440), 60)));
    }

    private sealed class RemainingCoverageRawTexture : RawTexture
    {
        public bool CreateForTest(RawTextureParams parameters) => Create(parameters);
    }
}
