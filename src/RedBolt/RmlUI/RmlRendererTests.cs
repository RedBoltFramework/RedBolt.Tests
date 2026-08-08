namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.RmlUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RmlRendererTests : IDisposable
{
    private readonly GraphicsEngineFixture _fixture;
    private readonly IDisposable _providerModule;

    public RmlRendererTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
        _providerModule = global::Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi");
    }

    public void Dispose() => _providerModule.Dispose();

    [GraphicsFact]
    public unsafe void RmlRenderer_matches_texture_callback_behavior()
    {
        using var renderer = new RmlRenderer();
        Assert.NotEqual(nint.Zero, renderer.GetAdaptedInterface());

        var size = new RmlUiLegacyVector2i { X = 1, Y = 1 };
        Span<byte> pixels = [1, 2, 3, 4];
        nuint generatedTexture = 0;
        fixed (byte* source = pixels)
        {
            Assert.True(renderer.GenerateTexture(
                (nint)(&generatedTexture), (nint)source, (nint)(&size)));
        }
        Assert.NotEqual(0u, generatedTexture);
        renderer.ReleaseTexture(generatedTexture);

        size = new RmlUiLegacyVector2i { X = 1024, Y = 8 };
        var gpuAtlas = new byte[size.X * size.Y * 4];
        "RBHBGPU1"u8.CopyTo(gpuAtlas);
        nuint generatedGpuTexture = 0;
        fixed (byte* source = gpuAtlas)
        {
            Assert.True(renderer.GenerateTexture(
                (nint)(&generatedGpuTexture), (nint)source, (nint)(&size)));
        }
        Assert.NotEqual(0u, generatedGpuTexture);
        renderer.ReleaseTexture(generatedGpuTexture);

        size = new RmlUiLegacyVector2i { X = 2048, Y = 8 };
        var transportedGpuAtlas = new byte[size.X * size.Y * 4];
        ReadOnlySpan<byte> transportSignature =
            [(byte)'R', (byte)'B', (byte)'H', 0xff, (byte)'B', (byte)'G', (byte)'P', 0xff,
                (byte)'U', (byte)'1', 0, 0xff];
        transportSignature.CopyTo(transportedGpuAtlas);
        for (var alpha = 3; alpha < transportedGpuAtlas.Length; alpha += 4)
            transportedGpuAtlas[alpha] = 0xff;
        ReadOnlySpan<byte> atlasSignature = "RBHBGPU1"u8;
        for (var index = 0; index < atlasSignature.Length; ++index)
            transportedGpuAtlas[transportSignature.Length + (index / 3) * 4 + index % 3] = atlasSignature[index];
        generatedGpuTexture = 0;
        fixed (byte* source = transportedGpuAtlas)
        {
            Assert.True(renderer.GenerateTexture(
                (nint)(&generatedGpuTexture), (nint)source, (nint)(&size)));
        }
        Assert.NotEqual(0u, generatedGpuTexture);
        renderer.ReleaseTexture(generatedGpuTexture);

        using var path = new Std.String();
        path.Assign("Textures/Missing.png");
        var missingSize = new RmlUiLegacyVector2i { X = -1, Y = -1 };
        nuint missingTexture = 0;
        Assert.True(renderer.LoadTexture((nint)(&missingTexture), (nint)(&missingSize), path));
        Assert.NotEqual(0u, missingTexture);
        Assert.Equal(-1, missingSize.X);
        Assert.Equal(-1, missingSize.Y);
        renderer.ReleaseTexture(missingTexture);
        renderer.ReleaseTexture(0);
    }

    [GraphicsFact]
    public unsafe void RmlRenderer_submits_legacy_geometry()
    {
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        try
        {
            using var renderer = new RmlRenderer();
            Span<RmlUiLegacyVertex> vertices =
            [
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 0, Y = 0 },
                    Colour = uint.MaxValue
                },
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 32, Y = 0 },
                    Colour = uint.MaxValue
                },
                new()
                {
                    Position = new RmlUiLegacyVector2f { X = 0, Y = 32 },
                    Colour = uint.MaxValue
                }
            ];
            Span<int> indices = [0, 1, 2];
            var translation = new RmlUiLegacyVector2f();

            Assert.True(_fixture.Graphics.BeginFrame());
            try
            {
                var renderContext = _fixture.RenderDevice.GetRenderContext();
                renderContext.SetSwapChainRenderTargets();
                renderContext.SetFullViewport();
                renderer.BeginRendering();
                fixed (RmlUiLegacyVertex* vertexData = vertices)
                fixed (int* indexData = indices)
                {
                    renderer.RenderGeometry(
                        (nint)vertexData, vertices.Length, (nint)indexData, indices.Length, 0, (nint)(&translation));
                }
                renderer.EndRendering();
            }
            finally
            {
                _fixture.Graphics.EndFrame();
            }
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
