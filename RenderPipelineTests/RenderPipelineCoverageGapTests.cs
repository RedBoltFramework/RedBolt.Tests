namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

public sealed class RenderPipelineCoverageGapTests
{
    [Fact]
    public void Batch_sort_keys_cover_empty_duplicate_and_boundary_comparisons()
    {
        var empty = new PipelineBatchByState();
        var same = new PipelineBatchByState { PrimaryKey = 0, SecondaryKey = 0 };
        var lowerPrimary = new PipelineBatchByState { PrimaryKey = 1, SecondaryKey = ulong.MaxValue };
        var higherPrimary = new PipelineBatchByState { PrimaryKey = 2, SecondaryKey = 0 };
        var lowerSecondary = new PipelineBatchByState { PrimaryKey = 4, SecondaryKey = 1 };
        var higherSecondary = new PipelineBatchByState { PrimaryKey = 4, SecondaryKey = 2 };

        Assert.Equal(0, empty.CompareTo(same));
        Assert.Equal(0, same.CompareTo(empty));
        Assert.Equal(-1, lowerPrimary.CompareTo(higherPrimary));
        Assert.Equal(1, higherPrimary.CompareTo(lowerPrimary));
        Assert.Equal(-1, lowerSecondary.CompareTo(higherSecondary));
        Assert.Equal(1, higherSecondary.CompareTo(lowerSecondary));

        var front = new PipelineBatchBackToFront { RenderOrder = 1, Distance = 10.0f };
        var back = new PipelineBatchBackToFront { RenderOrder = 1, Distance = 2.0f };
        var laterOrder = new PipelineBatchBackToFront { RenderOrder = 2, Distance = 100.0f };
        var duplicate = new PipelineBatchBackToFront { RenderOrder = 1, Distance = 10.0f };

        Assert.Equal(-1, front.CompareTo(back));
        Assert.Equal(1, back.CompareTo(front));
        Assert.Equal(-1, front.CompareTo(laterOrder));
        Assert.Equal(1, laterOrder.CompareTo(front));
        Assert.Equal(0, front.CompareTo(duplicate));
        Assert.Equal(0, new PipelineBatchBackToFront
        {
            RenderOrder = 1,
            Distance = float.NaN
        }.CompareTo(front));
    }

    [Fact]
    public void Pipeline_defs_cover_value_semantics_and_invalid_settings()
    {
        var renderBuffers = new RenderBufferManagerSettings
        {
            InheritMultiSampleLevel = false,
            MultiSampleLevel = -4,
            ColorSpace = RenderPipelineColorSpace.Optimized,
            FilteredColor = true,
            StencilBuffer = true,
            ReadableDepth = true,
            ColorUsableWithMultipleRenderTargets = true
        };
        renderBuffers.Validate();
        Assert.Equal(1, renderBuffers.MultiSampleLevel);
        Assert.NotEqual(0u, renderBuffers.CalculatePipelineStateHash());
        Assert.True(renderBuffers.Equals((object)renderBuffers));
        Assert.False(renderBuffers.Equals("not settings"));
        var sameRenderBuffers = renderBuffers;
        Assert.True(renderBuffers == sameRenderBuffers);
        Assert.False(renderBuffers != sameRenderBuffers);
        _ = renderBuffers.GetHashCode();

        var drawable = new DrawableProcessorSettings
        {
            MaxVertexLights = -1,
            MaxPixelLights = 300,
            PcfKernelSize = 4,
            NormalOffsetScale = -1.0f
        };
        drawable.Validate();
        Assert.Equal(0, drawable.MaxVertexLights);
        Assert.Equal(256, drawable.MaxPixelLights);
        Assert.Equal(3, drawable.PcfKernelSize);
        Assert.Equal(0.0f, drawable.NormalOffsetScale);
        Assert.True(drawable.Equals((object)drawable));
        Assert.False(drawable.Equals("not settings"));
        _ = drawable.CalculatePipelineStateHash();
        _ = drawable.GetHashCode();

        var instancing = new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 10,
            StepRate = 2
        };
        instancing.Validate();
        Assert.NotEqual(0u, instancing.CalculatePipelineStateHash());
        Assert.True(instancing.Equals((object)instancing));
        Assert.False(instancing.Equals("not settings"));
        _ = instancing.GetHashCode();

        var batch = new BatchRendererSettings
        {
            CubemapBoxProjection = true,
            AmbientMode = DrawableAmbientMode.Flat,
            VarianceShadowMapParams = new Vector2f(0.25f, 0.75f)
        };
        batch.Validate();
        Assert.NotEqual(0u, batch.CalculatePipelineStateHash());
        Assert.True(batch.Equals((object)batch));
        Assert.False(batch.Equals("not settings"));
        _ = batch.GetHashCode();

        var shadows = new ShadowMapAllocatorSettings
        {
            EnableVarianceShadowMaps = true,
            VarianceShadowMapMultiSample = 17,
            Use16BitShadowMaps = true,
            ShadowAtlasPageSize = 100,
            DepthBiasScale = -1.0f,
            DepthBiasOffset = 0.5f
        };
        shadows.Validate();
        Assert.Equal(16, shadows.VarianceShadowMapMultiSample);
        Assert.Equal(128, shadows.ShadowAtlasPageSize);
        Assert.Equal(0.0f, shadows.DepthBiasScale);
        Assert.NotEqual(0u, shadows.CalculatePipelineStateHash());
        Assert.True(shadows.Equals((object)shadows));
        Assert.False(shadows.Equals("not settings"));
        _ = shadows.GetHashCode();

        var occlusion = new OcclusionBufferSettings
        {
            ThreadedOcclusion = true,
            MaxOccluderTriangles = 1,
            OcclusionBufferSize = 20_000,
            OccluderSizeThreshold = 0.5f
        };
        occlusion.Validate();
        Assert.Equal(16 * 1024, occlusion.OcclusionBufferSize);
        Assert.Equal(0u, occlusion.CalculatePipelineStateHash());
        Assert.True(occlusion.Equals((object)occlusion));
        Assert.False(occlusion.Equals("not settings"));
        _ = occlusion.GetHashCode();

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
        Assert.True(scene.Equals((object)scene));
        Assert.False(scene.Equals("not settings"));
        _ = scene.CalculatePipelineStateHash();
        _ = scene.GetHashCode();

        var compositor = new ShaderProgramCompositorSettings
        {
            RenderBufferManager = renderBuffers,
            SceneProcessor = scene,
            ShadowMapAllocator = shadows,
            InstancingBuffer = instancing
        };
        compositor.Validate();
        _ = compositor.CalculatePipelineStateHash();
        Assert.True(compositor.Equals((object)compositor));
        Assert.False(compositor.Equals("not settings"));
        _ = compositor.GetHashCode();

        var pipeline = new RenderPipelineSettings
        {
            RenderBufferManager = renderBuffers,
            SceneProcessor = scene,
            ShadowMapAllocator = shadows,
            InstancingBuffer = instancing,
            DrawDebugGeometry = false
        };
        pipeline.Validate();
        Assert.Equal((ShaderProgramCompositorSettings)pipeline, (ShaderProgramCompositorSettings)pipeline);
        _ = pipeline.CalculatePipelineStateHash();
        Assert.True(pipeline.Equals((object)pipeline));
        Assert.False(pipeline.Equals("not settings"));
        _ = pipeline.GetHashCode();
    }

    [Fact]
    public void Pipeline_settings_cover_ambient_modes_empty_flags_and_fallbacks()
    {
        foreach (var mode in Enum.GetValues<DrawableAmbientMode>())
        {
            var settings = new RenderPipelineSettings();
            var scene = settings.SceneProcessor;
            scene.AmbientMode = mode;
            scene.LightingMode = DirectLightingMode.Forward;
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
                _ => 10
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

        var settingsWithoutBilinear = new RenderPipelineSettings();
        settingsWithoutBilinear.AdjustForRenderPath(RenderOutputFlag.None);
        Assert.False(settingsWithoutBilinear.RenderBufferManager.FilteredColor);
        settingsWithoutBilinear.AdjustForRenderPath(RenderOutputFlag.NeedColorOutputBilinear);
        Assert.True(settingsWithoutBilinear.RenderBufferManager.FilteredColor);

        var empty = new SceneProcessorSettings();
        empty.LightingMode = DirectLightingMode.Forward;
        Assert.False(empty.IsDeferredLighting());
        _ = empty == new SceneProcessorSettings();
        _ = empty != new SceneProcessorSettings { MaxPixelLights = 3 };
    }

    [Fact]
    public void Batch_state_keys_cover_null_object_and_duplicate_inputs()
    {
        var empty = new BatchStateLookupKey(0, 0, GeometryType.Static, null, null, null);
        var duplicate = new BatchStateLookupKey(0, 0, GeometryType.Static, null, null, null);
        var changedHash = new BatchStateLookupKey(1, 0, GeometryType.Static, null, null, null);
        var changedType = new BatchStateLookupKey(0, 0, GeometryType.Skinned, null, null, null);

        Assert.True(empty.Equals(duplicate));
        Assert.False(empty.Equals(changedHash));
        Assert.False(empty.Equals(changedType));
        Assert.True(empty.Equals((object)duplicate));
        Assert.False(empty.Equals("not a key"));
        Assert.False(empty.Equals(null));
        Assert.Equal(empty.GetHashCode(), duplicate.GetHashCode());
        Assert.NotEqual(empty.ToHash(), changedHash.ToHash());
        Assert.NotEqual(empty.ToHash(), changedType.ToHash());
        Assert.Equal(empty, duplicate);
        Assert.NotEqual(empty, changedHash);

        var uiEmpty = new UIBatchStateKey(false, new PipelineStateOutputDesc(), null, null, BlendMode.Replace, 0);
        var uiDuplicate = new UIBatchStateKey(false, new PipelineStateOutputDesc(), null, null, BlendMode.Replace, 0);
        var uiChanged = new UIBatchStateKey(true, new PipelineStateOutputDesc(), null, null, BlendMode.Replace, 0);

        Assert.True(uiEmpty.Equals(uiDuplicate));
        Assert.False(uiEmpty.Equals(uiChanged));
        Assert.True(uiEmpty.Equals((object)uiDuplicate));
        Assert.False(uiEmpty.Equals("not a key"));
        Assert.False(uiEmpty.Equals(null));
        Assert.Equal(uiEmpty.GetHashCode(), uiDuplicate.GetHashCode());
        Assert.NotEqual(uiEmpty.ToHash(), uiChanged.ToHash());

        var createKey = new BatchStateCreateKey(1, 2, GeometryType.Static, null, null, null,
            null, null, 3);
        Assert.Equal(createKey.LookupKey, new BatchStateLookupKey(1, 2, GeometryType.Static, null, null, null));
        Assert.Equal(3, createKey.PixelLightIndex);
        var context = new BatchStateCreateContext(null, -1, int.MaxValue);
        Assert.Equal(-1, context.SubpassIndex);
        Assert.Equal(int.MaxValue, context.ShadowSplitIndex);
    }

    [Fact]
    public void Light_accumulator_covers_zero_distance_spot_clamps_and_cached_cook()
    {
        var pointAtOrigin = new LightDataForAccumulator
        {
            Position = Vector3f.Zero,
            LightType = LightType.Point,
            Range = 0.0f
        };
        var zeroDistance = pointAtOrigin.GetDirectionToLight(Vector3f.Zero);
        Assert.Equal(Vector3f.Right, zeroDistance.Direction);
        Assert.Equal(1.0f, zeroDistance.Attenuation);

        var spot = new LightDataForAccumulator
        {
            Direction = Vector3f.Right,
            LightType = LightType.Spot,
            SpotCutoff = 0.5f,
            InverseSpotCutoff = 2.0f,
            Color = Color.White
        };
        Assert.Equal(0.0f, spot.GetSpotAttenuation(Vector3f.Left));
        Assert.Equal(1.0f, spot.GetSpotAttenuation(Vector3f.Right));
        _ = spot.GetLightingAtPoint(Vector3f.Zero);

        var accumulator = new LightAccumulator();
        accumulator.Cook();
        var firstHash = accumulator.VertexLightsHash;
        accumulator.Cook();
        Assert.Equal(1u, firstHash);
        Assert.Equal(firstHash, accumulator.VertexLightsHash);
        Assert.Equal(int.MaxValue, accumulator.VertexLights[0]);
    }

    [Fact]
    public void Render_path_repairs_duplicates_and_rejects_invalid_mutations()
    {
        using var path = new RenderPath();
        var alpha = new CoverageRenderPass("Alpha", enabledByDefault: false,
            new RenderPassTraits(true, false));
        var beta = new CoverageRenderPass("Beta", enabledByDefault: true,
            new RenderPassTraits(false, true));
        path.AddPass(alpha);
        path.AddPass(beta);

        var repaired = path.RepairEnabledRenderPasses(
        [
            ("Unknown", true),
            ("Unknown", false)
        ]);
        Assert.Equal([("Alpha", false), ("Beta", true)], repaired);

        var duplicateSource = path.RepairEnabledRenderPasses(
        [
            ("Alpha", true),
            ("Alpha", false)
        ]);
        Assert.Equal([("Alpha", true), ("Beta", true)], duplicateSource);

        var ghost = new CoverageRenderPass("Ghost", true, default);
        path.ReorderPass(ghost, 0);
        path.ReorderPass(beta, -1);
        path.ReorderPass(beta, path.Passes.Count);
        ghost.Dispose();

        using var parameters = new StringVariantMap();
        path.UpdateParameters(new RenderPipelineSettings(), [ ("Alpha", true), ("Beta", false) ], parameters);
        Assert.Equal(new RenderPassTraits(true, true), path.AggregatedPassTraits);
        var state = new SharedRenderPassState();
        path.Update(state);
        path.Render(state);
        Assert.Equal(1, alpha.UpdateCalls);
        Assert.Equal(1, alpha.RenderCalls);
        Assert.Equal(0, beta.UpdateCalls);
        Assert.Equal(0, beta.RenderCalls);

    }

    [Fact]
    public void Render_path_clone_skips_a_missing_clone_and_preserves_real_passes()
    {
        using var path = new RenderPath();
        path.AddPass(new CoverageRenderPass("NullClone", true, default) { ReturnNullClone = true });
        path.AddPass(new CoverageRenderPass("RealClone", true, default));

        using var clone = path.Clone();

        var pass = Assert.Single(clone.Passes);
        Assert.Equal("RealClone", pass.GetPassName());
    }

    private sealed class CoverageRenderPass : RenderPass
    {
        private readonly string _name;

        public CoverageRenderPass(string name, bool enabledByDefault, RenderPassTraits traits)
        {
            _name = name;
            _enabledByDefault = enabledByDefault;
            _traits = traits;
        }

        public int UpdateCalls { get; private set; }
        public int RenderCalls { get; private set; }
        public bool ReturnNullClone { get; init; }

        public override string GetPassName() => _name;

        public override void Update(SharedRenderPassState sharedState) => ++UpdateCalls;

        public override void Render(SharedRenderPassState sharedState) => ++RenderCalls;

        public override RenderPass? Clone() => ReturnNullClone
            ? null
            : new CoverageRenderPass(_name, _enabledByDefault, _traits);
    }
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderPipelineGraphicsCoverageGapTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RenderPipelineGraphicsCoverageGapTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Shadow_allocator_covers_empty_invalid_full_page_and_reset_paths()
    {
        using var allocator = new ShadowMapAllocator();
        var settings = allocator.Settings;
        settings.EnableVarianceShadowMaps = false;
        settings.ShadowAtlasPageSize = 128;
        allocator.SetSettings(in settings);
        allocator.SetSettings(in settings);

        var zeroPage = settings;
        zeroPage.ShadowAtlasPageSize = 0;
        allocator.SetSettings(in zeroPage);
        Assert.False(allocator.AllocateShadowMap(new Vector2i(16, 16)));
        allocator.SetSettings(in settings);

        Assert.False(allocator.BeginShadowMapRendering(default));
        Assert.False(allocator.BeginShadowMapRendering(new ShadowMapRegion
        {
            PageIndex = 99,
            Texture = new Texture2D()
        }));

        var first = allocator.AllocateShadowMap(new Vector2i(512, 512));
        var second = allocator.AllocateShadowMap(new Vector2i(512, 512));
        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, first.PageIndex);
        Assert.Equal(1, second.PageIndex);
        Assert.Equal(new IntRect(0, 0, 128, 128), first.Rect);
        Assert.True(allocator.BeginShadowMapRendering(first));

        allocator.ResetAllShadowMaps();
        Assert.True(allocator.BeginShadowMapRendering(first));
    }

    [GraphicsFact]
    public void Shadow_allocator_covers_variance_map_render_target_and_settings_fallback()
    {
        using var allocator = new ShadowMapAllocator();
        var settings = allocator.Settings;
        settings.EnableVarianceShadowMaps = true;
        settings.ShadowAtlasPageSize = 128;
        settings.VarianceShadowMapMultiSample = 1;
        allocator.SetSettings(in settings);

        var region = allocator.AllocateShadowMap(new Vector2i(64, 32));
        Assert.True(region);
        Assert.Equal(new IntRect(0, 0, 64, 32), region.Rect);
        Assert.True(allocator.BeginShadowMapRendering(region));
        Assert.Equal(TextureFormat.TexFormatRg32Float, allocator.ShadowOutputDesc.RenderTargetFormats[0]);
        Assert.False(allocator.SamplerStateDesc.ShadowCompare);
    }

    [GraphicsFact]
    public void Shadow_split_covers_empty_and_finalized_world_to_shadow_paths()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("CoverageLight");
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Spot);
        light.SetRange(20.0f);
        light.SetFov(60.0f);

        using var processor = new LightProcessor(light);
        using var split = new ShadowSplitProcessor(processor, 0);

        Assert.Equal(Matrix4.Identity, split.GetWorldToShadowSpaceMatrix(0.0f));
        split.InitializeSpot();
        using var texture = new Texture2D { Name = "CoverageShadow" };
        Assert.True(texture.SetSize(64, 64, TextureFormat.TexFormatD24UnormS8Uint,
            TextureFlag.BindDepthStencil));
        var region = new ShadowMapRegion
        {
            PageIndex = 0,
            Texture = texture,
            Rect = new IntRect(0, 0, 64, 64)
        };

        split.FinalizeShadow(region, 11);
        Assert.Equal(4, split.ShadowMapPadding);
        Assert.True(split.ShadowMapTexelSizeInWorldSpace > 0.0f);
        Assert.NotEqual(Matrix4.Identity, split.GetWorldToShadowSpaceMatrix(0.25f));
    }

    [GraphicsFact]
    public void Render_buffer_manager_covers_empty_targets_subrect_clear_and_buffer_swaps()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var manager = new RenderBufferManager(pipeline);
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true,
            ReadableColor = true
        });

        var frame = pipeline.BeginFrame(manager, null, new IntRect(0, 0, 64, 64));
        manager.SetRenderTargetsRect(IntRect.Zero, null, Array.Empty<RenderBuffer>());
        manager.ClearOutputRect(new IntRect(0, 0, 32, 32),
            ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil,
            Color.Black, 1.0f, 0);
        manager.SwapColorBuffers(synchronizeContents: false);
        manager.SwapColorBuffers(synchronizeContents: true);
        pipeline.EndFrame(frame);
    }

    [GraphicsFact]
    public void Batch_cache_covers_unknown_null_and_recreated_entries()
    {
        using var geometry = new Geometry();
        using var material = new Material { Name = "CoverageMaterial" };
        using var technique = new Technique { Name = "CoverageTechnique" };
        var pass = technique.CreatePass("base");
        var key = new BatchStateCreateKey(1, 2, GeometryType.Static, geometry, material, pass,
            null, null, 0);
        var callback = new NullBatchCallback();
        using var cache = new BatchStateCache();
        cache.SetOutputDesc(new PipelineStateOutputDesc());

        Assert.Null(cache.GetPipelineState(key.LookupKey));
        Assert.Null(cache.GetOrCreatePipelineState(key, new BatchStateCreateContext(null, 0, 0), callback));
        Assert.Null(cache.GetPipelineState(key.LookupKey));
        cache.SetOutputDesc(new PipelineStateOutputDesc { MultiSample = 4 });
        Assert.Null(cache.GetPipelineState(key.LookupKey));
        Assert.Equal(1, callback.CreateCount);

        var uiKey = new UIBatchStateKey(false, new PipelineStateOutputDesc(), material, pass,
            BlendMode.Alpha, 42);
        var uiCallback = new NullUIBatchCallback();
        using var uiCache = new UIBatchStateCache();
        Assert.Null(uiCache.GetPipelineState(uiKey));
        Assert.Null(uiCache.GetOrCreatePipelineState(uiKey, default, uiCallback));
        Assert.Null(uiCache.GetPipelineState(uiKey));
        Assert.Equal(1, uiCallback.CreateCount);
    }

    private sealed class NullBatchCallback : BatchStateCacheCallback
    {
        public int CreateCount { get; private set; }

        public PipelineState? CreateBatchPipelineState(BatchStateCreateKey key,
            BatchStateCreateContext context, PipelineStateOutputDesc outputDesc)
        {
            ++CreateCount;
            return null;
        }

        public PipelineState? CreateBatchPipelineStatePlaceholder(int vertexStride,
            PipelineStateOutputDesc outputDesc) => null;
    }

    private sealed class NullUIBatchCallback : UIBatchStateCacheCallback
    {
        public int CreateCount { get; private set; }

        public PipelineState? CreateUIBatchPipelineState(UIBatchStateKey key, UIBatchStateCreateContext context)
        {
            ++CreateCount;
            return null;
        }
    }
}
