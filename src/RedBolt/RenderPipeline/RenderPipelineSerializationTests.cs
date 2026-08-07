namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineSerializationTests
{
    [Fact]
    public void Non_default_settings_and_parameters_roundtrip_through_scene_xml()
    {
        using var source = new Scene();
        var pipeline = source.CreateComponent<RenderPipeline>();
        var settings = pipeline.Settings;

        var renderBufferManager = settings.RenderBufferManager;
        renderBufferManager.ColorSpace = RenderPipelineColorSpace.LinearHDR;
        renderBufferManager.ReadableDepth = true;
        settings.RenderBufferManager = renderBufferManager;

        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.MaterialQuality = MaterialQuality.Low;
        sceneProcessor.SpecularQuality = SpecularQuality.Disabled;
        sceneProcessor.ReflectionQuality = ReflectionQuality.Vertex;
        sceneProcessor.LinearReflectionMaps = true;
        sceneProcessor.MaxVertexLights = 2;
        sceneProcessor.MaxPixelLights = 3;
        sceneProcessor.AmbientMode = DrawableAmbientMode.Flat;
        sceneProcessor.DepthPrePass = true;
        sceneProcessor.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        sceneProcessor.EnableShadows = false;
        sceneProcessor.CubemapBoxProjection = true;
        sceneProcessor.PcfKernelSize = 5;
        sceneProcessor.VarianceShadowMapParams = new Vector2f(0.1f, 0.2f);
        sceneProcessor.NormalOffsetScale = 2.0f;
        settings.SceneProcessor = sceneProcessor;

        var instancingBuffer = settings.InstancingBuffer;
        instancingBuffer.EnableInstancing = false;
        settings.InstancingBuffer = instancingBuffer;

        var shadowMapAllocator = settings.ShadowMapAllocator;
        shadowMapAllocator.EnableVarianceShadowMaps = true;
        shadowMapAllocator.VarianceShadowMapMultiSample = 2;
        shadowMapAllocator.Use16BitShadowMaps = true;
        shadowMapAllocator.DepthBiasScale = 2.0f;
        shadowMapAllocator.DepthBiasOffset = 0.25f;
        settings.ShadowMapAllocator = shadowMapAllocator;
        settings.DrawDebugGeometry = false;

        pipeline.SetSettings(settings);
        pipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);
        using (var value = new Variant(3.0f))
        {
            pipeline.GetRenderPathParameters()["Custom Render"] = value;
            pipeline.SetFrameShaderParameter("Custom Frame", value);
        }

        using var buffer = new VectorBuffer();
        Assert.True(source.SaveXML(buffer));
        buffer.Seek(0);
        using var loadedScene = new Scene();
        Assert.True(loadedScene.LoadXML(buffer));
        var loaded = loadedScene.GetComponent<RenderPipeline>();
        Assert.NotNull(loaded);
        ref readonly var actual = ref loaded!.Settings;

        Assert.Equal(RenderPipelineColorSpace.LinearHDR, actual.RenderBufferManager.ColorSpace);
        Assert.True(actual.RenderBufferManager.ReadableDepth);
        Assert.Equal(MaterialQuality.Low, actual.SceneProcessor.MaterialQuality);
        Assert.Equal(SpecularQuality.Disabled, actual.SceneProcessor.SpecularQuality);
        Assert.Equal(ReflectionQuality.Vertex, actual.SceneProcessor.ReflectionQuality);
        Assert.True(actual.SceneProcessor.LinearReflectionMaps);
        Assert.Equal(2, actual.SceneProcessor.MaxVertexLights);
        Assert.Equal(3, actual.SceneProcessor.MaxPixelLights);
        Assert.Equal(DrawableAmbientMode.Flat, actual.SceneProcessor.AmbientMode);
        Assert.True(actual.SceneProcessor.DepthPrePass);
        Assert.Equal(DirectLightingMode.DeferredBlinnPhong, actual.SceneProcessor.LightingMode);
        Assert.False(actual.SceneProcessor.EnableShadows);
        Assert.True(actual.SceneProcessor.CubemapBoxProjection);
        Assert.Equal(5, actual.SceneProcessor.PcfKernelSize);
        Assert.Equal(new Vector2f(0.1f, 0.2f), actual.SceneProcessor.VarianceShadowMapParams);
        Assert.Equal(2.0f, actual.SceneProcessor.NormalOffsetScale);
        Assert.False(actual.InstancingBuffer.EnableInstancing);
        Assert.True(actual.ShadowMapAllocator.EnableVarianceShadowMaps);
        Assert.Equal(2, actual.ShadowMapAllocator.VarianceShadowMapMultiSample);
        Assert.True(actual.ShadowMapAllocator.Use16BitShadowMaps);
        Assert.Equal(2.0f, actual.ShadowMapAllocator.DepthBiasScale);
        Assert.Equal(0.25f, actual.ShadowMapAllocator.DepthBiasOffset);
        Assert.False(actual.DrawDebugGeometry);
        Assert.Contains(loaded.GetRenderPasses(), pass => pass == ("Postprocess: FXAA v3", true));
        Assert.Equal(3.0f, loaded.GetRenderPathParameters()["Custom Render"].GetFloat());
        Assert.Equal(3.0f, loaded.GetFrameShaderParameters()["Custom Frame"].GetFloat());
    }
}
