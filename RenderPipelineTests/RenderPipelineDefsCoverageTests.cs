namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineDefsCoverageTests
{
    [Fact]
    public void Managed_render_pipeline_helpers_cover_formats_parameters_and_regions()
    {
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm,
            InvokeColorSpaceFormat(RenderPipelineColorSpace.GammaLDR));
        Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb,
            InvokeColorSpaceFormat(RenderPipelineColorSpace.LinearLDR));
        Assert.Equal(TextureFormat.TexFormatRgba16Float,
            InvokeColorSpaceFormat(RenderPipelineColorSpace.LinearHDR));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm,
            InvokeColorSpaceFormat(RenderPipelineColorSpace.Optimized));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm,
            InvokeColorSpaceFormat((RenderPipelineColorSpace)int.MaxValue));

        var outputFormat = TextureFormat.TexFormatRg32Float;
        Assert.Equal(TextureFormat.TexFormatRgba16Float,
            InvokeColorFormat(RenderPipelineColorSpace.LinearHDR, outputFormat));
        Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb,
            InvokeColorFormat(RenderPipelineColorSpace.LinearLDR, outputFormat));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm,
            InvokeColorFormat(RenderPipelineColorSpace.GammaLDR, outputFormat));
        Assert.Equal(outputFormat,
            InvokeColorFormat(RenderPipelineColorSpace.Optimized, outputFormat));
        Assert.Equal(outputFormat,
            InvokeColorFormat((RenderPipelineColorSpace)int.MaxValue, outputFormat));

        Assert.Equal(new Vector4f(0.3f, 0.375f, 0.1f, 0.125f),
            InvokeClipToUv(100, 200, new IntRect(20, 50, 40, 100)));

        var defaults = new RenderBufferParams();
        Assert.Equal(1, defaults.MultiSampleLevel);
        var parameters = new RenderBufferParams(
            TextureFormat.TexFormatRgba8Unorm,
            4,
            RenderBufferFlag.Persistent | RenderBufferFlag.BilinearFiltering);
        var copy = parameters;
        Assert.True(parameters == copy);
        Assert.False(parameters != copy);
        Assert.True(parameters.Equals((object)copy));
        Assert.False(parameters.Equals("not render-buffer parameters"));
        _ = parameters.GetHashCode();

        copy.Flags = RenderBufferFlag.Persistent;
        Assert.False(parameters == copy);
        Assert.True(parameters != copy);

        var quad = new DrawQuadParams();
        Assert.Empty(quad.Resources);
        Assert.Empty(quad.Parameters);
        IReadOnlyList<ShaderResourceDesc> resources = [new ShaderResourceDesc()];
        IReadOnlyList<ShaderParameterDesc> shaderParameters = [new ShaderParameterDesc()];
        quad.Resources = resources;
        quad.Parameters = shaderParameters;
        Assert.Same(resources, quad.Resources);
        Assert.Same(shaderParameters, quad.Parameters);

        var emptyRegion = default(ShadowMapRegion);
        Assert.False(emptyRegion);
        using var texture = new Texture2D { Name = "Managed shadow map" };
        var region = new ShadowMapRegion
        {
            PageIndex = 3,
            Texture = texture,
            Rect = new IntRect(8, 12, 72, 44)
        };
        Assert.True(region);
        var split = region.GetSplit(5, new Vector2i(4, 2));
        Assert.Equal(3, split.PageIndex);
        Assert.Same(texture, split.Texture);
        Assert.Equal(new IntRect(24, 28, 40, 44), split.Rect);

        RenderTargetView depthView = default;
        var noDepth = new RenderOutputTargetInfo(
            default,
            new IntRect(0, 0, 64, 32),
            TextureFormat.TexFormatRgba8Unorm,
            multiSample: 0,
            bilinearFiltered: true,
            linearColorSpace: true);
        Assert.Equal(1, noDepth.MultiSample);
        Assert.False(noDepth.HasDepthStencil);
        Assert.True(noDepth.BilinearFiltered);
        Assert.True(noDepth.LinearColorSpace);

        var depthByView = new RenderOutputTargetInfo(
            default,
            IntRect.Zero,
            TextureFormat.TexFormatRgba8Unorm,
            depthStencilTarget: depthView,
            multiSample: -2,
            hdr: true,
            hasReadableDepth: true,
            hasStencil: true);
        Assert.Equal(1, depthByView.MultiSample);
        Assert.True(depthByView.HasDepthStencil);
        Assert.True(depthByView.HDR);
        Assert.True(depthByView.HasReadableDepth);
        Assert.True(depthByView.HasStencil);

        var depthByFormat = new RenderOutputTargetInfo(
            default,
            IntRect.Zero,
            TextureFormat.TexFormatRgba8Unorm,
            depthStencilFormat: TextureFormat.TexFormatD24UnormS8Uint);
        Assert.True(depthByFormat.HasDepthStencil);
    }

    [Fact]
    public void Managed_settings_cover_validation_hashes_and_value_operators()
    {
        var renderBuffers = new RenderBufferManagerSettings
        {
            InheritMultiSampleLevel = false,
            MultiSampleLevel = -4,
            ColorSpace = RenderPipelineColorSpace.LinearHDR,
            FilteredColor = true,
            StencilBuffer = true,
            ReadableDepth = true,
            ColorUsableWithMultipleRenderTargets = true
        };
        renderBuffers.Validate();
        Assert.Equal(1, renderBuffers.MultiSampleLevel);
        Assert.NotEqual(0u, renderBuffers.CalculatePipelineStateHash());
        var renderBuffersCopy = renderBuffers;
        Assert.True(renderBuffers == renderBuffersCopy);
        Assert.False(renderBuffers != renderBuffersCopy);
        Assert.False(renderBuffers.Equals(new object()));
        _ = renderBuffers.GetHashCode();
        var changedRenderBuffers = renderBuffers;
        changedRenderBuffers.ReadableDepth = false;
        Assert.True(renderBuffers != changedRenderBuffers);

        changedRenderBuffers.MultiSampleLevel = 17;
        changedRenderBuffers.Validate();
        Assert.Equal(16, changedRenderBuffers.MultiSampleLevel);

        var cache = new LightProcessorCacheSettings();
        Assert.Equal(64, cache.Budget);
        Assert.Equal(60, cache.NormalTimeToLive);
        Assert.Equal(2, cache.AggressiveTimeToLive);
        var cacheCopy = cache;
        Assert.True(cache == cacheCopy);
        Assert.False(cache != cacheCopy);
        Assert.False(cache.Equals(new object()));
        _ = cache.GetHashCode();
        var changedCache = cache;
        changedCache.Budget++;
        Assert.True(cache != changedCache);

        var drawable = new DrawableProcessorSettings
        {
            MaxVertexLights = -1,
            MaxPixelLights = 300,
            PcfKernelSize = 4,
            NormalOffsetScale = -1.0f,
            LightProcessorCache = cache
        };
        drawable.Validate();
        Assert.Equal(0, drawable.MaxVertexLights);
        Assert.Equal(256, drawable.MaxPixelLights);
        Assert.Equal(3, drawable.PcfKernelSize);
        Assert.Equal(0.0f, drawable.NormalOffsetScale);
        var drawableCopy = drawable;
        Assert.True(drawable == drawableCopy);
        Assert.False(drawable != drawableCopy);
        Assert.False(drawable.Equals(new object()));
        _ = drawable.CalculatePipelineStateHash();
        _ = drawable.GetHashCode();
        var changedDrawable = drawable;
        changedDrawable.MaxPixelLights--;
        Assert.True(drawable != changedDrawable);

        var instancing = new InstancingBufferSettings();
        Assert.Equal(1, instancing.StepRate);
        _ = instancing.CalculatePipelineStateHash();
        instancing.EnableInstancing = true;
        instancing.FirstInstancingTexCoord = 4;
        instancing.NumInstancingTexCoords = 10;
        instancing.StepRate = 2;
        instancing.Validate();
        var instancingCopy = instancing;
        Assert.True(instancing == instancingCopy);
        Assert.False(instancing != instancingCopy);
        Assert.False(instancing.Equals(new object()));
        _ = instancing.GetHashCode();
        var changedInstancing = instancing;
        changedInstancing.StepRate++;
        Assert.True(instancing != changedInstancing);

        var batch = new BatchRendererSettings();
        Assert.Equal(DrawableAmbientMode.Directional, batch.AmbientMode);
        batch.CubemapBoxProjection = true;
        batch.AmbientMode = DrawableAmbientMode.Flat;
        batch.Validate();
        _ = batch.CalculatePipelineStateHash();
        var batchCopy = batch;
        Assert.True(batch == batchCopy);
        Assert.False(batch != batchCopy);
        Assert.False(batch.Equals(new object()));
        _ = batch.GetHashCode();
        var changedBatch = batch;
        changedBatch.AmbientMode = DrawableAmbientMode.Constant;
        Assert.True(batch != changedBatch);

        var shadows = new ShadowMapAllocatorSettings
        {
            EnableVarianceShadowMaps = true,
            VarianceShadowMapMultiSample = 0,
            Use16BitShadowMaps = true,
            ShadowAtlasPageSize = 100,
            DepthBiasScale = -1.0f,
            DepthBiasOffset = 0.5f
        };
        shadows.Validate();
        Assert.Equal(1, shadows.VarianceShadowMapMultiSample);
        Assert.Equal(128, shadows.ShadowAtlasPageSize);
        Assert.Equal(0.0f, shadows.DepthBiasScale);
        var shadowsCopy = shadows;
        Assert.True(shadows == shadowsCopy);
        Assert.False(shadows != shadowsCopy);
        Assert.False(shadows.Equals(new object()));
        _ = shadows.CalculatePipelineStateHash();
        _ = shadows.GetHashCode();
        var largeShadows = shadows;
        largeShadows.VarianceShadowMapMultiSample = 17;
        largeShadows.ShadowAtlasPageSize = 20_000;
        largeShadows.Validate();
        Assert.Equal(16, largeShadows.VarianceShadowMapMultiSample);
        Assert.Equal(16 * 1024, largeShadows.ShadowAtlasPageSize);

        var occlusion = new OcclusionBufferSettings
        {
            OcclusionBufferSize = 0,
            ThreadedOcclusion = true,
            MaxOccluderTriangles = 1
        };
        occlusion.Validate();
        Assert.Equal(1, occlusion.OcclusionBufferSize);
        Assert.Equal(0u, occlusion.CalculatePipelineStateHash());
        var occlusionCopy = occlusion;
        Assert.True(occlusion == occlusionCopy);
        Assert.False(occlusion != occlusionCopy);
        Assert.False(occlusion.Equals(new object()));
        _ = occlusion.GetHashCode();
        var largeOcclusion = occlusion;
        largeOcclusion.OcclusionBufferSize = 20_000;
        largeOcclusion.Validate();
        Assert.Equal(16 * 1024, largeOcclusion.OcclusionBufferSize);

        var scene = new SceneProcessorSettings
        {
            MaxVertexLights = -3,
            MaxPixelLights = 300,
            PcfKernelSize = 4,
            NormalOffsetScale = -2.0f,
            OcclusionBufferSize = 0,
            DirectionalShadowSize = 3,
            SpotShadowSize = 5,
            PointShadowSize = 7,
            LightingMode = DirectLightingMode.DeferredPBR,
            LinearReflectionMaps = true,
            EnableShadows = false
        };
        scene.Validate();
        Assert.Equal(0, scene.MaxVertexLights);
        Assert.Equal(256, scene.MaxPixelLights);
        Assert.Equal(3, scene.PcfKernelSize);
        Assert.Equal(0.0f, scene.NormalOffsetScale);
        Assert.Equal(1, scene.OcclusionBufferSize);
        Assert.Equal(2, scene.DirectionalShadowSize);
        Assert.Equal(4, scene.SpotShadowSize);
        Assert.Equal(8, scene.PointShadowSize);
        Assert.True(scene.IsDeferredLighting());
        var sceneCopy = scene;
        Assert.True(scene == sceneCopy);
        Assert.False(scene != sceneCopy);
        Assert.False(scene.Equals(new object()));
        _ = scene.CalculatePipelineStateHash();
        _ = scene.GetHashCode();

        DrawableProcessorSettings convertedDrawable = scene;
        OcclusionBufferSettings convertedOcclusion = scene;
        BatchRendererSettings convertedBatch = scene;
        Assert.Equal(scene.MaxPixelLights, convertedDrawable.MaxPixelLights);
        Assert.Equal(scene.OcclusionBufferSize, convertedOcclusion.OcclusionBufferSize);
        Assert.Equal(scene.AmbientMode, convertedBatch.AmbientMode);

        var compositor = new ShaderProgramCompositorSettings
        {
            RenderBufferManager = renderBuffers,
            SceneProcessor = scene,
            ShadowMapAllocator = shadows,
            InstancingBuffer = instancing
        };
        compositor.Validate();
        _ = compositor.CalculatePipelineStateHash();
        var compositorCopy = compositor;
        Assert.True(compositor == compositorCopy);
        Assert.False(compositor != compositorCopy);
        Assert.False(compositor.Equals(new object()));
        _ = compositor.GetHashCode();

        var changedCompositor = compositor;
        var changedInstancingInCompositor = changedCompositor.InstancingBuffer;
        changedInstancingInCompositor.StepRate++;
        changedCompositor.InstancingBuffer = changedInstancingInCompositor;
        Assert.True(compositor != changedCompositor);
    }

    [Fact]
    public void Pipeline_settings_cover_implied_adjusted_and_no_device_paths()
    {
        foreach (var mode in new[]
        {
            DrawableAmbientMode.Constant,
            DrawableAmbientMode.Flat,
            DrawableAmbientMode.Directional,
            (DrawableAmbientMode)int.MaxValue
        })
        {
            var settings = new RenderPipelineSettings();
            var scene = settings.SceneProcessor;
            scene.LightingMode = DirectLightingMode.Forward;
            scene.AmbientMode = mode;
            settings.SceneProcessor = scene;
            var instancing = settings.InstancingBuffer;
            instancing.EnableInstancing = true;
            settings.InstancingBuffer = instancing;

            settings.PropagateImpliedSettings();

            Assert.Equal(4, settings.InstancingBuffer.FirstInstancingTexCoord);
            Assert.Equal(mode switch
            {
                DrawableAmbientMode.Constant => 3,
                DrawableAmbientMode.Flat => 4,
                DrawableAmbientMode.Directional => 10,
                _ => 0
            }, settings.InstancingBuffer.NumInstancingTexCoords);
        }

        var deferred = new RenderPipelineSettings();
        var deferredScene = deferred.SceneProcessor;
        deferredScene.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        deferred.SceneProcessor = deferredScene;
        deferred.PropagateImpliedSettings();
        Assert.True(deferred.RenderBufferManager.ColorUsableWithMultipleRenderTargets);
        Assert.True(deferred.RenderBufferManager.StencilBuffer);
        Assert.True(deferred.RenderBufferManager.ReadableDepth);
        Assert.False(deferred.RenderBufferManager.InheritMultiSampleLevel);

        var forward = new SceneProcessorSettings { LightingMode = DirectLightingMode.Forward };
        Assert.False(forward.IsDeferredLighting());
        forward.LightingMode = DirectLightingMode.DeferredBlinnPhong;
        Assert.True(forward.IsDeferredLighting());
        forward.LightingMode = DirectLightingMode.DeferredPBR;
        Assert.True(forward.IsDeferredLighting());

        var adjusted = new RenderPipelineSettings();
        adjusted.AdjustForRenderPath(RenderOutputFlag.None);
        Assert.False(adjusted.RenderBufferManager.FilteredColor);
        adjusted.AdjustForRenderPath(RenderOutputFlag.NeedColorOutputBilinear);
        Assert.True(adjusted.RenderBufferManager.FilteredColor);

        var pipelineCopy = adjusted;
        Assert.True(adjusted == pipelineCopy);
        Assert.False(adjusted != pipelineCopy);
        Assert.False(adjusted.Equals(new object()));
        pipelineCopy.DrawDebugGeometry = !pipelineCopy.DrawDebugGeometry;
        Assert.True(adjusted != pipelineCopy);
        _ = adjusted.CalculatePipelineStateHash();
        _ = adjusted.GetHashCode();

        using var context = new Context();
        adjusted.AdjustToSupported(context);
    }

    [Fact]
    public void Pipeline_state_helper_records_cover_value_semantics_and_defaults()
    {
        var added = new DrawableProcessorPass.AddBatchResult(true, false);
        var lit = added with { ForwardLitAdded = true };
        Assert.True(added.Added);
        Assert.False(added.ForwardLitAdded);
        Assert.True(lit.ForwardLitAdded);
        var addedCopy = added;
        Assert.True(added == addedCopy);
        Assert.False(added != addedCopy);
        Assert.False(added.Equals(new object()));
        _ = added.GetHashCode();
        Assert.NotEqual(added, lit);

        var traits = new RenderPassTraits(true, false);
        var bilinearTraits = traits with { NeedBilinearColorSampler = true };
        Assert.True(traits.NeedReadWriteColorBuffer);
        Assert.False(traits.NeedBilinearColorSampler);
        Assert.True(bilinearTraits.NeedBilinearColorSampler);
        var traitsCopy = traits;
        Assert.True(traits == traitsCopy);
        Assert.False(traits != traitsCopy);
        Assert.False(traits.Equals(new object()));
        _ = traits.GetHashCode();
        Assert.NotEqual(traits, bilinearTraits);

        var output = new PipelineStateOutputDesc();
        Assert.Equal(TextureFormat.TexFormatUnknown, output.DepthStencilFormat);
        Assert.Equal(1, output.MultiSample);
        output.NumRenderTargets = 1;
        output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        var outputCopy = output;
        Assert.True(output.Equals(outputCopy));
        Assert.Equal(output.ToHash(), outputCopy.ToHash());
        outputCopy.MultiSample = 4;
        Assert.False(output.Equals(outputCopy));

        var cachedBatch = new CachedBatchState();
        var cachedUiBatch = new CachedUIBatchState();
        Assert.True(cachedBatch.Invalidated);
        Assert.True(cachedUiBatch.Invalidated);
        var createContext = new BatchStateCreateContext(null, -1, int.MaxValue);
        Assert.Equal(-1, createContext.SubpassIndex);
        Assert.Equal(int.MaxValue, createContext.ShadowSplitIndex);
        var uiContext = new UIBatchStateCreateContext(null, null);
        Assert.Null(uiContext.VertexBuffer);
        Assert.Null(uiContext.IndexBuffer);
        Assert.Null(uiContext.DefaultSampler);
    }

    [Fact]
    public void Drawable_processor_pass_covers_custom_missing_and_unlit_routes()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var drawable = new CustomGeometry();
        var technique = new Technique { Name = "managed coverage" };
        technique.CreatePass("base");

        using var unlitPass = new CoverageDrawableProcessorPass(
            pipeline,
            DrawableProcessorPassFlag.HasAmbientLighting,
            int.MaxValue,
            Technique.GetPassIndex("base"),
            int.MaxValue,
            int.MaxValue);
        unlitPass.ResetBatches();
        var unlitResult = unlitPass.AddBatch(0, drawable, 0, technique);
        Assert.True(unlitResult.Added);
        Assert.False(unlitResult.ForwardLitAdded);
        Assert.False(unlitPass.HasLightPass);
        Assert.True(unlitPass.IsFlagSet(DrawableProcessorPassFlag.HasAmbientLighting));
        Assert.False(unlitPass.IsFlagSet(DrawableProcessorPassFlag.DepthOnlyPass));
        Assert.Single(unlitPass.Batches);

        using var missingBase = new CoverageDrawableProcessorPass(
            pipeline,
            DrawableProcessorPassFlag.None,
            int.MaxValue,
            Technique.GetPassIndex("missing"),
            int.MaxValue,
            Technique.GetPassIndex("light"));
        var missingResult = missingBase.AddBatch(0, drawable, 0, technique);
        Assert.False(missingResult.Added);
        Assert.False(missingResult.ForwardLitAdded);
        Assert.Empty(missingBase.Batches);

        using var custom = new CoverageDrawableProcessorPass(
            pipeline,
            DrawableProcessorPassFlag.BatchCallback | DrawableProcessorPassFlag.StereoInstancing,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            useCustomBatch: true);
        var customResult = custom.AddBatch(0, drawable, 0, technique);
        Assert.True(customResult.Added);
        Assert.True(customResult.ForwardLitAdded);
        Assert.True(custom.IsFlagSet(DrawableProcessorPassFlag.StereoInstancing));
        Assert.True(custom.IsLinearColorSpace is false);
    }

    [Fact]
    public void Drawable_processor_managed_helpers_cover_updates_lights_penalties_and_sorts()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new CoverageDrawableProcessor(pipeline);
        using var scene = new Scene();
        var frameInfo = new FrameInfo
        {
            Scene = scene,
            Octree = scene.CreateComponent<Octree>(),
            Camera = scene.CreateChild("Camera").CreateComponent<Camera>()
        };
        var workerDrawable = new CoverageDrawable(UpdateGeometryType.WorkerThread);
        var mainThreadDrawable = new CoverageDrawable(UpdateGeometryType.MainThread);
        frameInfo.Octree.AddManualDrawable(workerDrawable);
        frameInfo.Octree.AddManualDrawable(mainThreadDrawable);
        var positive = scene.CreateChild("Positive").CreateComponent<Light>();
        var negative = scene.CreateChild("Negative").CreateComponent<Light>();
        negative.SetColor(new Color(-1.0f, -1.0f, -1.0f, 1.0f));
        processor.OnUpdateBegin(frameInfo);

        InvokePrivate(processor, "UpdateGeometry", 0, workerDrawable);
        Assert.Equal(1, workerDrawable.UpdateCount);

        InvokePrivate(processor, "UpdateGeometry", 0, mainThreadDrawable);
        processor.UpdateGeometries();
        Assert.Equal(1, mainThreadDrawable.UpdateCount);

        var finiteRange = processor.CalculateZRange(
            new BoundingBox(new Vector3f(-1.0f, -1.0f, 9.0f), new Vector3f(1.0f, 1.0f, 11.0f)));
        Assert.True(finiteRange.IsValid());
        var infiniteRange = processor.CalculateZRange(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
        Assert.Equal(default(FloatRange), infiniteRange);

        processor.ProcessVisibleDrawables([positive, negative], null);
        Assert.Same(positive, processor.GetLight(0));
        Assert.Empty(processor.Geometries);
        processor.ProcessForwardLightingForLight(-1, Array.Empty<Drawable>());
        processor.ProcessForwardLightingForLight(processor.Lights.Count, Array.Empty<Drawable>());

        Assert.Equal(-2.0f, InvokePenalty(1.0f, false, LightImportance.Important, LightType.Directional));
        Assert.Equal(-1.0f, InvokePenalty(1.0f, true, LightImportance.Important, LightType.Directional));
        Assert.Equal(-1.0f, InvokePenalty(1.0f, false, LightImportance.Important, LightType.Point));
        Assert.Equal(0.5f, InvokePenalty(0.5f, false, LightImportance.Auto, LightType.Point));
        Assert.Equal(1.5f, InvokePenalty(2.0f, false, LightImportance.Auto, LightType.Point));
        Assert.Equal(3.5f, InvokePenalty(0.5f, false, LightImportance.NotImportant, LightType.Point));
        Assert.Equal(4.5f, InvokePenalty(2.0f, false, LightImportance.NotImportant, LightType.Point));
        Assert.Equal(MathDefs.LargeValue,
            InvokePenalty(1.0f, false, (LightImportance)int.MaxValue, LightType.Point));

        using var firstLight = new Light();
        using var secondLight = new Light();
        using var thirdLight = new Light();
        using var firstProcessor = new LightProcessor(firstLight);
        using var secondProcessor = new LightProcessor(secondLight);
        using var thirdProcessor = new LightProcessor(thirdLight);
        SetPrivateField(firstProcessor, "_numActiveSplits", 1);
        SetPrivateField(secondProcessor, "_numActiveSplits", 1);
        SetPrivateField(thirdProcessor, "_numActiveSplits", 1);
        SetPrivateField(firstProcessor, "_shadowMapSize", new Vector2i(2, 3));
        SetPrivateField(secondProcessor, "_shadowMapSize", new Vector2i(3, 2));
        SetPrivateField(thirdProcessor, "_shadowMapSize", new Vector2i(4, 1));
        processor.SetProcessors(firstProcessor, secondProcessor, thirdProcessor);
        processor.SortByShadowMapSize();
        Assert.Equal(3, processor.LightProcessorsByShadowMapSizeCount);

        using var sharedTexture = new Texture2D { Name = "shared" };
        SetPrivateField(firstProcessor, "_shadowMap", new ShadowMapRegion { Texture = sharedTexture });
        SetPrivateField(secondProcessor, "_shadowMap", default(ShadowMapRegion));
        SetPrivateField(thirdProcessor, "_shadowMap", new ShadowMapRegion { Texture = sharedTexture });
        processor.SortByShadowMapTexture();
        Assert.Equal(3, processor.LightProcessorsByShadowMap.Count);
    }

    private static TextureFormat InvokeColorSpaceFormat(RenderPipelineColorSpace colorSpace)
    {
        var utilityType = typeof(RenderPipelineSettings).Assembly.GetType(
            "RedBolt.RenderPipeline.RenderPipelineColorSpaceUtility");
        Assert.NotNull(utilityType);
        var method = utilityType!.GetMethod("GetTextureFormat", BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(method);
        return (TextureFormat)method!.Invoke(null, [colorSpace])!;
    }

    private static TextureFormat InvokeColorFormat(
        RenderPipelineColorSpace colorSpace, TextureFormat outputTextureFormat)
        => (TextureFormat)InvokePrivateStatic(
            typeof(RenderBufferManager),
            "GetColorTextureFormat",
            colorSpace,
            outputTextureFormat);

    private static Vector4f InvokeClipToUv(int width, int height, IntRect sourceRect)
        => (Vector4f)InvokePrivateStatic(
            typeof(RenderBufferManager),
            "GetClipToUvOffsetAndScale",
            width,
            height,
            sourceRect);

    private static float InvokePenalty(
        float intensityPenalty, bool isNegative, LightImportance importance, LightType type)
        => (float)InvokePrivateStatic(
            typeof(DrawableProcessor),
            "GetDrawableLightPenalty",
            intensityPenalty,
            isNegative,
            importance,
            type);

    private static object InvokePrivateStatic(Type type, string name, params object[] arguments)
    {
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(null, arguments)!;
    }

    private static object InvokePrivate(object instance, string name, params object[] arguments)
    {
        var method = instance.GetType().BaseType!.GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(instance, arguments)!;
    }

    private static void SetPrivateField<T>(object instance, string name, T value)
    {
        var field = instance.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? instance.GetType().BaseType?.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private sealed class CoverageDrawableProcessorPass : DrawableProcessorPass
    {
        private readonly bool _useCustomBatch;

        public CoverageDrawableProcessorPass(
            IRenderPipelineInterface renderPipeline,
            DrawableProcessorPassFlag flags,
            int deferredPassIndex,
            int unlitBasePassIndex,
            int litBasePassIndex,
            int lightPassIndex,
            bool useCustomBatch = false)
            : base(renderPipeline, flags, deferredPassIndex, unlitBasePassIndex,
                litBasePassIndex, lightPassIndex)
        {
            _useCustomBatch = useCustomBatch;
        }

        public IEnumerable<GeometryBatch> Batches => GeometryBatches;

        public bool IsLinearColorSpace => LinearColorSpace;

        public void ResetBatches() => OnUpdateBegin(default);

        public override AddBatchResult AddCustomBatch(
            int threadIndex, Drawable drawable, int sourceBatchIndex, Technique technique)
            => _useCustomBatch ? new AddBatchResult(true, true) : default;
    }

    private sealed class CoverageDrawableProcessor : DrawableProcessor
    {
        public CoverageDrawableProcessor(IRenderPipelineInterface renderPipeline)
            : base(renderPipeline)
        {
        }

        public FloatRange CalculateZRange(BoundingBox boundingBox)
            => CalculateBoundingBoxZRange(boundingBox);

        public void SetProcessors(params LightProcessor?[] processors)
        {
            var field = typeof(DrawableProcessor).GetField(
                "_lightProcessors",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var values = Assert.IsType<List<LightProcessor?>>(field!.GetValue(this));
            values.Clear();
            values.AddRange(processors);
        }

        public void SortByShadowMapSize() => SortLightProcessorsByShadowMapSize();

        public void SortByShadowMapTexture() => SortLightProcessorsByShadowMapTexture();

        public int LightProcessorsByShadowMapSizeCount
        {
            get
            {
                var propertyField = typeof(DrawableProcessor).GetField(
                    "_lightProcessorsByShadowMapSize",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(propertyField);
                return Assert.IsType<List<LightProcessor?>>(propertyField!.GetValue(this)).Count;
            }
        }
    }

    private sealed class CoverageDrawable : Drawable
    {
        private readonly UpdateGeometryType _updateGeometryType;

        public CoverageDrawable(UpdateGeometryType updateGeometryType = UpdateGeometryType.None)
        {
            _updateGeometryType = updateGeometryType;
        }

        public int UpdateCount { get; private set; }

        public override UpdateGeometryType GetUpdateGeometryType() => _updateGeometryType;

        public override void UpdateGeometry(FrameInfo frame) => ++UpdateCount;

        protected override void OnWorldBoundingBoxUpdate()
        {
        }
    }
}
