// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsStateCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GraphicsStateCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Material_xml_load_save_and_technique_state_cover_edge_paths()
    {
        var knownTechnique = Resources.Get<Technique>("Techniques/NoTexture.xml");
        using var source = new XMLFile();
        var root = source.CreateRoot("material");

        var shader = root.CreateChild("shader");
        shader.SetAttribute("vsdefines", "VS_STATE");
        shader.SetAttribute("psdefines", "PS_STATE");

        root.CreateChild("technique").SetAttribute("name", "");
        var missingTechnique = root.CreateChild("technique");
        missingTechnique.SetAttribute("name", "Techniques/MissingCoverageTechnique.xml");
        missingTechnique.SetInt("quality", (int)MaterialQuality.High);
        missingTechnique.SetFloat("loddistance", 4.0f);
        var validTechnique = root.CreateChild("technique");
        validTechnique.SetAttribute("name", knownTechnique.Name);
        validTechnique.SetInt("quality", (int)MaterialQuality.Medium);
        validTechnique.SetFloat("loddistance", 2.0f);

        foreach (var unit in new[]
                 {
                     "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15",
                     "diffuse", "norm", "specular", "emissive", "environment", "volume", "custom1", "custom2",
                     "lightramp", "lightshape", "shadowmap", "faceselect", "indirection", "depth", "light", "zone", "unknown"
                 })
        {
            var texture = root.CreateChild("texture");
            texture.SetAttribute("unit", unit);
            texture.SetAttribute("name", "Textures/MissingCoverage.png");
        }

        var xmlTexture = root.CreateChild("texture");
        xmlTexture.SetAttribute("unit", "0");
        xmlTexture.SetAttribute("name", "Textures/MissingCoverage.xml");
        xmlTexture.SetAttribute("type", "Texture3D");

        root.CreateChild("parameter").SetAttribute("name", "");
        root.CreateChild("parameter").SetAttribute("name", "MatSpecColor");
        root.GetChild("parameter").GetNext("parameter").SetAttribute("value", "1 0 0 1");
        root.CreateChild("parameter").SetAttribute("name", "CoverageVector");
        root.GetChild("parameter").GetNext("parameter").GetNext("parameter").SetAttribute("value", "1, 2, 3");

        root.CreateChild("cull").SetAttribute("value", "none");
        root.CreateChild("shadowcull").SetAttribute("value", "cw");
        root.CreateChild("fill").SetAttribute("value", "point");
        var depthBias = root.CreateChild("depthbias");
        depthBias.SetFloat("constant", 2.0f);
        depthBias.SetFloat("slopescaled", 20.0f);
        depthBias.SetFloat("normaloffset", -1.0f);
        root.CreateChild("alphatocoverage").SetBool("enable", true);
        root.CreateChild("lineantialias").SetBool("enable", true);
        root.CreateChild("renderorder").SetUInt("value", 300u);
        root.CreateChild("occlusion").SetBool("enable", false);

        using var material = new Material();
        Assert.True(material.LoadXML(root));
        Assert.Equal("VS_STATE", material.GetVertexShaderDefines());
        Assert.Equal("PS_STATE", material.GetPixelShaderDefines());
        Assert.NotNull(material.GetTechnique(0));
        Assert.Equal(knownTechnique.Name, material.GetTechnique(0)!.Name);
        Assert.Equal(MaterialQuality.Medium, material.GetTechniqueEntry(0).QualityLevel);
        Assert.Equal(2.0f, material.GetTechniqueEntry(0).LodDistance);
        Assert.Equal(CullMode.None, material.GetCullMode());
        Assert.Equal(CullMode.Clockwise, material.GetShadowCullMode());
        Assert.Equal(FillMode.Point, material.GetFillMode());
        Assert.Equal(new BiasParameters(1.0f, 16.0f, 0.0f), material.GetDepthBias());
        Assert.True(material.GetAlphaToCoverage());
        Assert.True(material.GetLineAntiAlias());
        Assert.Equal((byte)44, material.GetRenderOrder());
        Assert.False(material.GetOcclusion());
        Assert.True(material.GetSpecular());
        Assert.Equal(new Vector3f(1, 2, 3), material.GetShaderParameter("CoverageVector"));

        using var save = new XMLFile();
        var saveRoot = save.CreateRoot("material");
        using var textureResource = new Texture2D { Name = "Textures/Coverage.png" };
        material.SetTexture("Albedo", textureResource);
        material.SetTechniques([
            new TechniqueEntry(knownTechnique, MaterialQuality.High, 8.0f) { Original = knownTechnique }
        ]);
        Assert.True(material.SaveXML(saveRoot));
        Assert.Equal("Techniques/NoTexture.xml", saveRoot.GetChild("technique").GetAttribute("name"));
        Assert.Equal("Albedo", saveRoot.GetChild("texture").GetAttribute("slot"));
        Assert.NotNull(saveRoot.GetChild("shader"));
        Assert.NotNull(saveRoot.GetChild("depthbias"));
        Assert.NotNull(saveRoot.GetChild("occlusion"));
    }

    [Fact]
    public void Material_shader_parameter_animation_and_copy_paths_are_deterministic()
    {
        using var material = new Material();
        material.SetShaderParameter("Animated", 0.0f);
        material.SetShaderParameter("MatSpecColor", "not-a-color");
        Assert.False(material.GetSpecular());
        material.SetShaderParameter("MatSpecColor", new Vector3f(0, 0, 0));
        Assert.False(material.GetSpecular());
        material.SetShaderParameter("MatSpecColor", new Vector4f(1, 0, 0, 1));
        Assert.True(material.GetSpecular());

        using var animation = new ValueAnimation
        {
            ValueType = VariantType.Float,
            Name = "Animations/Coverage"
        };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 1.0f);

        material.SetShaderParameterAnimation("Missing", animation);
        Assert.Null(material.GetShaderParameterAnimationInfo("Missing"));
        material.SetShaderParameterAnimation("Animated", animation, WrapMode.Once, 2.0f);
        material.SetShaderParameterAnimation("Animated", animation, WrapMode.Clamp, 3.0f);
        Assert.Equal(WrapMode.Clamp, material.GetShaderParameterAnimationWrapMode("Animated"));
        Assert.Equal(3.0f, material.GetShaderParameterAnimationSpeed("Animated"));
        material.SetShaderParameterAnimationWrapMode("Missing", WrapMode.Once);
        material.SetShaderParameterAnimationSpeed("Missing", 4.0f);

        using var replacement = new ValueAnimation
        {
            ValueType = VariantType.Float,
            Name = "Animations/Replacement"
        };
        replacement.SetKeyFrame(0.0f, 2.0f);
        replacement.SetKeyFrame(1.0f, 4.0f);
        material.SetShaderParameterAnimation("Animated", replacement, WrapMode.Loop, 1.0f);

        var update = new CoreEvents.UpdateEvent { TimeStep = 0.25f };
        Context.Instance.SendEvent(ref update);
        Assert.Equal(2.5f, material.GetShaderParameter("Animated"));
        material.SetShaderParameterAnimation("Animated", null);
        material.SetShaderParameterAnimation("Animated", null);

        material.SetVertexShaderDefines(null!);
        material.SetPixelShaderDefines(null!);
        using var clone = material.Clone("Materials/StateClone");
        Assert.Equal("Materials/StateClone", clone.Name);
        Assert.Equal(material.GetShaderParameterHash(), clone.GetShaderParameterHash());
        Assert.True(clone.GetSpecular());
    }

    [Fact]
    public void Texture_metadata_levels_and_sampler_modes_cover_all_public_state_switches()
    {
        using var texture = new Texture2D { Name = "Textures/StateCoverage" };
        using var parameters = new XMLFile();
        var root = parameters.CreateRoot("texture");

        foreach (var (coordinate, mode) in new[]
                 {
                     ("U", "mirror"), ("V", "clamp"), ("W", "wrap"), ("U", "unknown")
                 })
        {
            var address = root.CreateChild("address");
            address.SetAttribute("coord", coordinate);
            address.SetAttribute("mode", mode);
        }

        foreach (var mode in new[] { "nearest", "bilinear", "trilinear", "anisotropic", "nearestanisotropic", "unknown" })
        {
            var filter = root.CreateChild("filter");
            filter.SetAttribute("mode", mode);
            if (mode == "anisotropic")
                filter.SetUInt("anisotropy", 7u);
        }

        var mipmap = root.CreateChild("mipmap");
        mipmap.SetBool("enable", false);
        mipmap.SetFloat("minlod", 1.25f);
        mipmap.SetFloat("maxlod", 5.5f);
        var quality = root.CreateChild("quality");
        quality.SetInt("low", 3);
        quality.SetInt("med", 2);
        quality.SetInt("medium", 1);
        quality.SetInt("high", 0);
        root.CreateChild("srgb").SetBool("enable", true);
        root.CreateChild("linear").SetBool("enable", true);

        texture.SetParameters((XMLFile?)null);
        texture.SetParameters(parameters);
        Assert.Equal(TextureAddressMode.Wrap, texture.GetAddressMode(TextureCoordinate.U));
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));
        Assert.Equal(TextureAddressMode.Wrap, texture.GetAddressMode(TextureCoordinate.W));
        Assert.Equal(TextureFilterMode.Default, texture.GetFilterMode());
        Assert.Equal(7u, texture.GetAnisotropy());
        Assert.Equal(1.25f, texture.GetMinLod());
        Assert.Equal(5.5f, texture.GetMaxLod());
        Assert.Equal(3, texture.GetMipsToSkip(MaterialQuality.Low));
        Assert.Equal(1, texture.GetMipsToSkip(MaterialQuality.Medium));
        Assert.Equal(0, texture.GetMipsToSkip(MaterialQuality.High));
        texture.SetMipsToSkip((MaterialQuality)99, 4);
        Assert.Equal(0, texture.GetMipsToSkip((MaterialQuality)99));

        texture.SetNumLevels(2);
        Assert.True(texture.SetSize(16, 8, TextureFormat.TexFormatRgba8Unorm));
        Assert.True(texture.GetSRGB());
        Assert.True(texture.GetLinear());
        Assert.Equal(new Vector2i(16, 8), texture.GetSize());
        Assert.Equal(new IntRect(0, 0, 16, 8), texture.GetRect());
        Assert.Equal(8, texture.GetLevelWidth(1));
        Assert.Equal(4, texture.GetLevelHeight(1));
        Assert.Equal(0, texture.GetLevelWidth(3));
        Assert.Equal(0, texture.GetLevelHeight(3));
        Assert.Equal(4, texture.GetComponents());
        Assert.Equal(64, texture.GetRowDataSize(16));
        Assert.False(texture.IsCompressed());
        Assert.False(texture.IsRenderTarget());
        Assert.False(texture.IsDepthStencil());
        Assert.False(texture.IsUnorderedAccess());
        Assert.False(texture.GetAutoResolve());
    }

    [Fact]
    public void Texture_formats_flags_and_dimension_boundaries_cover_cpu_state_paths()
    {
        using var empty = new Texture();
        Assert.Equal(0, empty.GetComponents());
        Assert.Equal(1, empty.GetLevelWidth(0));
        Assert.Equal(1, empty.GetLevelHeight(0));
        Assert.Equal(1, empty.GetLevelDepth(0));

        using var compressed = new Texture2D { Name = "Textures/CompressedCoverage" };
        Assert.True(compressed.SetSize(8, 8, TextureFormat.TexFormatBc1Unorm));
        Assert.True(compressed.IsCompressed());
        Assert.Equal(32, compressed.GetDataSize(8, 8));

        using var depth = new Texture2D { Name = "Textures/DepthCoverage" };
        Assert.True(depth.SetSize(8, 8, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));
        Assert.True(depth.IsDepthStencil());
        Assert.False(depth.IsRenderTarget());
        Assert.False(depth.IsUnorderedAccess());

        using var unordered = new Texture2D { Name = "Textures/UavCoverage" };
        Assert.True(unordered.SetSize(8, 8, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess | TextureFlag.NoMultiSampledAutoResolve));
        Assert.True(unordered.IsRenderTarget());
        Assert.True(unordered.IsUnorderedAccess());
        Assert.False(unordered.GetAutoResolve());
        unordered.SetLinear(false);
        unordered.SetSRGB(false);
        unordered.SetShadowCompare(true);
        unordered.SetAnisotropy(3);
        Assert.False(unordered.GetLinear());
        Assert.False(unordered.GetSRGB());
        Assert.True(unordered.GetShadowCompare());
        Assert.Equal(3u, unordered.GetAnisotropy());

        using var volume = new Texture3D { Name = "Textures/VolumeCoverage" };
        Assert.True(volume.SetSize(8, 4, 2, TextureFormat.TexFormatR8Unorm));
        Assert.Equal(1, volume.GetLevelDepth(1));
        Assert.Equal(0, volume.GetLevelDepth(int.MaxValue));
    }

    [Fact]
    public void Animation_tracks_triggers_and_lookup_boundaries_cover_mutation_paths()
    {
        using var animation = new Animation { Name = "Animations/StateCoverage" };
        animation.SetAnimationName("state");
        animation.SetLength(-2.0f);
        Assert.Equal(0.0f, animation.Length);
        animation.SetLength(4.0f);

        var track = animation.CreateTrack("root");
        Assert.Same(track, animation.CreateTrack("root"));
        var variantTrack = animation.CreateVariantTrack("speed");
        Assert.Same(variantTrack, animation.CreateVariantTrack("speed"));
        Assert.Same(track, animation.GetTrack(0));
        Assert.Null(animation.GetTrack(-1));
        Assert.Null(animation.GetTrack(9));
        Assert.Same(track, animation.GetTrack(new StringHash("root")));
        Assert.Same(variantTrack, animation.GetVariantTrack(0));
        Assert.Null(animation.GetVariantTrack(-1));
        Assert.Null(animation.GetVariantTrack(9));

        using var firstData = new Variant("first");
        using var secondData = new Variant("second");
        animation.AddTrigger(3.0f, false, firstData);
        animation.AddTrigger(0.5f, true, secondData);
        Assert.Equal(2.0f, animation.Triggers[0].Time);
        animation.SetTrigger(0, new AnimationTriggerPoint { Time = 2.0f, Data = new Variant("replacement") });
        animation.SetTrigger(animation.NumTriggers, new AnimationTriggerPoint { Time = 1.0f, Data = new Variant("append") });
        animation.SetTrigger(-1, default);
        animation.RemoveTrigger(-1);
        animation.RemoveTrigger(99);
        Assert.Equal(3, animation.NumTriggers);
        animation.SetNumTriggers(5);
        animation.SetNumTriggers(2);
        Assert.Equal(2, animation.NumTriggers);
        animation.RemoveAllTriggers();
        Assert.Empty(animation.Triggers);

        Assert.True(animation.RemoveTrack("root"));
        Assert.True(animation.RemoveTrack("speed"));
        Assert.False(animation.RemoveTrack("missing"));
        animation.RemoveAllTracks();

        using var invalidXml = new XMLFile();
        var invalidRoot = invalidXml.CreateRoot("animation");
        Assert.False(animation.LoadXML(invalidRoot));
    }

    [Fact]
    public void Animation_parameters_serialize_all_flags_and_cover_looping_modes()
    {
        using var animation = new Animation { Name = "Animations/Parameters" };
        animation.SetAnimationName("parameters");
        animation.SetLength(3.0f);
        var parameters = new AnimationParameters(animation, 0.25f, 2.5f)
        {
            InstanceIndex = 4,
            IsLooped = false,
            RemoveOnCompletion = true,
            LayerIndex = 2,
            BlendMode = AnimationBlendMode.Additive,
            StartBoneName = string.Empty,
            AutoFadeOutTime = 0.25f,
            SpeedValue = 2.0f,
            RemoveOnZeroWeight = true,
            WeightValue = 0.75f,
            TargetWeight = 0.5f,
            TargetWeightDelay = 0.4f
        };
        parameters.SetTime(2.0f);
        Assert.False(parameters.Update(4.0f).IsEmpty());

        using var encoded = new VectorBuffer();
        parameters.Serialize(encoded);
        encoded.Seek(0);
        var decoded = AnimationParameters.Deserialize(animation, encoded);
        Assert.Equal(parameters.InstanceIndex, decoded.InstanceIndex);
        Assert.Equal(parameters.LayerIndex, decoded.LayerIndex);
        Assert.Equal(parameters.BlendMode, decoded.BlendMode);
        Assert.Equal(parameters.GetTime(), decoded.GetTime());
        Assert.Equal(parameters.GetAnimationTime().Min(), decoded.GetAnimationTime().Min());
        Assert.Equal(parameters.GetAnimationTime().Max(), decoded.GetAnimationTime().Max());
        Assert.Equal(parameters.SpeedValue, decoded.SpeedValue);
        Assert.Equal(parameters.TargetWeightDelay, decoded.TargetWeightDelay);
        Assert.True(parameters.IsMergeableWith(decoded));
        Assert.False(parameters.Equals((AnimationParameters?)null));
        Assert.False(parameters.Equals(new object()));
        Assert.NotEqual(parameters, new AnimationParameters(animation, 0, 1));

        var defaultParameters = new AnimationParameters();
        using var defaultEncoded = new VectorBuffer();
        defaultParameters.Serialize(defaultEncoded);
        defaultEncoded.Seek(0);
        var defaultDecoded = AnimationParameters.Deserialize(null, defaultEncoded);
        Assert.Equal(0.0f, defaultDecoded.GetTime());
        Assert.Equal(AnimationBlendMode.Lerp, defaultDecoded.BlendMode);
    }

    [Fact]
    public void Camera_projection_and_view_state_cover_perspective_orthographic_and_custom_paths()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("CoverageCamera");
        node.SetPosition(new Vector3f(1, 2, 3));
        var camera = node.CreateComponent<Camera>();

        camera.SetAspectRatio(2.0f);
        camera.SetNearClip(-1.0f);
        camera.SetFarClip(100.0f);
        camera.SetFov(200.0f);
        camera.SetZoom(0.0f);
        camera.SetLodBias(0.0f);
        camera.SetProjectionOffset(new Vector2f(0.1f, -0.2f));
        camera.SetViewMask(0x12u);
        camera.SetViewOverrideFlags(ViewOverride.LowMaterialQuality | ViewOverride.DisableShadows);
        camera.SetUseClipping(true);
        camera.SetReflectionPlane(new Plane(Vector3f.Up, Vector3f.Zero));
        camera.SetUseReflection(true);
        camera.SetClipPlane(new Plane(Vector3f.Forward, Vector3f.Zero));
        camera.SetMousePosition(new Vector2f(0.25f, 0.75f));

        Assert.Equal(MathDefs.MinNearClip, camera.GetNearClip());
        Assert.Equal(160.0f, camera.GetFov());
        Assert.Equal(MathDefs.Epsilon, camera.GetZoom());
        Assert.Equal(MathDefs.Epsilon, camera.GetLodBias());
        Assert.Equal(0x12u, camera.GetViewMask());
        Assert.True(camera.HasMousePosition());
        Assert.NotEqual(Matrix4.Zero, camera.GetProjection());
        Assert.NotEqual(Matrix4.Zero, camera.GetViewProj());
        Assert.NotEmpty(camera.GetFrustum().Vertices);
        Assert.NotEmpty(camera.GetSplitFrustum(-1, 200).Vertices);
        Assert.NotEmpty(camera.GetViewSpaceFrustum().Vertices);
        Assert.NotEmpty(camera.GetViewSpaceSplitFrustum(-1, 200).Vertices);
        Assert.NotEqual(default, camera.GetScreenRayFromMouse());
        Assert.True(camera.GetReverseCulling());

        camera.SetOrthographic(true);
        camera.SetOrthoSize(new Vector2f(2.0f, 4.0f));
        camera.SetZoom(1.0f);
        camera.SetFlipVertical(true);
        camera.SetMousePosition(new Vector2f(-1, 2));
        Assert.False(camera.HasMousePosition());
        Assert.Equal(new Vector2f(1.0f, 2.0f), camera.GetViewSizeAt(5.0f));
        Assert.NotEqual(Matrix4.Zero, camera.GetGPUProjection());
        Assert.NotEqual(Matrix4.Zero, camera.GetInverseProjection());

        camera.SetProjection(Matrix4.Identity);
        Assert.True(camera.IsProjectionValid());
        Assert.NotEqual(Matrix4.Zero, camera.GetProjection());
        Assert.NotEmpty(camera.GetViewSpaceFrustum().Vertices);
    }

    [Fact]
    public void Light_state_and_shadow_parameters_cover_clamps_modes_and_effective_values()
    {
        using var scene = new Scene();
        var light = scene.CreateChild("CoverageLight").CreateComponent<Light>();
        light.SetLightType(LightType.Spot);
        light.SetLightImportance(LightImportance.NotImportant);
        light.SetLightMode(LightMode.Baked);
        light.SetColor(new Color(0.25f, 0.5f, 0.75f));
        light.SetTemperature(20000.0f);
        light.SetUsePhysicalValues(true);
        light.SetSpecularIntensity(-1.0f);
        light.SetBrightness(-2.0f);
        light.SetIndirectBrightness(3.0f);
        light.SetRange(-1.0f);
        light.SetFov(200.0f);
        light.SetAspectRatio(0.0f);
        light.SetFadeDistance(4.0f);
        light.SetShadowFadeDistance(5.0f);
        light.SetShadowBias(new BiasParameters(2.0f, 20.0f, -1.0f));
        light.SetShadowCascade(new CascadeParameters(1, 2, 1, 4, 0.5f));
        light.SetShadowFocus(new FocusParameters(true, true, true, 0, 0));
        light.SetShadowIntensity(2.0f);
        light.SetShadowResolution(0.0f);
        light.SetShadowNearFarRatio(1.0f);
        light.SetShadowMaxExtrusion(-1.0f);

        Assert.Equal(LightType.Spot, light.GetLightType());
        Assert.True(light.GetPerVertex());
        Assert.Equal(0u, light.GetLightMaskEffective());
        Assert.Equal(10000.0f, light.GetTemperature());
        Assert.Equal(0.0f, light.GetRange());
        Assert.Equal(MathDefs.MaxFov, light.GetFov());
        Assert.Equal(MathDefs.Epsilon, light.GetAspectRatio());
        Assert.True(MathDefs.Equals(light.GetEffectiveSpecularIntensity(), 0.0f));
        Assert.Equal(new BiasParameters(1, 16, 0), light.GetShadowBias());
        Assert.Equal(2, light.GetNumShadowSplits());
        Assert.Equal(Light.ShadowMinQuantize, light.GetShadowFocus().Quantize);
        Assert.Equal(1.0f, light.GetShadowIntensity());
        Assert.Equal(0.125f, light.GetShadowResolution());
        Assert.Equal(0.5f, light.GetShadowNearFarRatio());
        Assert.Equal(0.0f, light.GetShadowMaxExtrusion());
        light.SetUsePhysicalValues(false);
        light.SetColor(new Color(-1, -1, -1));
        light.SetBrightness(1.0f);
        Assert.True(light.IsNegative());
        Assert.True(light.GetCutoffParams().Cutoff >= 0.0f);
        Assert.True(light.GetIntensityDivisor() > 0.0f);
    }

    [Fact]
    public void Decal_and_ribbon_state_helpers_cover_empty_and_boundary_cases()
    {
        var vertex = new DecalVertex(Vector3f.One, Vector3f.Up);
        for (var i = 0; i < 4; ++i)
        {
            vertex.SetBlendWeight(i, i + 0.25f);
            vertex.SetBlendIndex(i, (byte)(i + 1));
        }
        Assert.Equal(0.25f, vertex.GetBlendWeight(0));
        Assert.Equal(3.25f, vertex.GetBlendWeight(3));
        Assert.Equal((byte)4, vertex.GetBlendIndex(3));
        Assert.Equal(0x04030201u, vertex.BlendIndicesPacked);

        var decal = new Decal();
        decal.AddVertex(vertex);
        decal.AddVertex(new DecalVertex(new Vector3f(-1, 0, 2), Vector3f.Up));
        decal.CalculateBoundingBox();
        Assert.True(decal.BoundingBox.Defined);
        Assert.NotEqual(Intersection.Outside, decal.BoundingBox.IsInside(Vector3f.One));

        using var scene = new Scene();
        var decals = scene.CreateChild("CoverageDecals").CreateComponent<DecalSet>();
        decals.SetMaxVertices(0);
        decals.SetMaxIndices(0);
        decals.SetOptimizeBufferSize(true);
        decals.RemoveDecals(0);
        decals.RemoveDecals(1);
        decals.RemoveAllDecals();
        Assert.Equal(4u, decals.GetMaxVertices());
        Assert.Equal(6u, decals.GetMaxIndices());
        Assert.True(decals.GetOptimizeBufferSize());

        var trailParent = scene.CreateChild("CoverageTrailParent");
        var trail = trailParent.CreateChild("CoverageTrail").CreateComponent<RibbonTrail>();
        var camera = scene.CreateChild("CoverageTrailCamera").CreateComponent<Camera>();
        trail.SetEmitting(false);
        trail.SetTrailType(TrailType.Bone);
        trail.SetTailColumn(0);
        trail.SetTailColumn(100);
        trail.SetAnimationLodBias(-1);
        trail.SetLifetime(-1);
        trail.SetVertexDistance(-1);
        trail.SetWidth(-1);
        trail.Update(new FrameInfo { TimeStep = 0.1f, Scene = scene });
        trail.UpdateBatches(new FrameInfo { Camera = camera, TimeStep = 0.1f, Scene = scene });
        Assert.False(trail.IsEmitting());
        Assert.Equal(TrailType.Bone, trail.GetTrailType());
        Assert.Equal(16u, trail.GetTailColumn());
        Assert.Equal(0.0f, trail.GetAnimationLodBias());
        Assert.Equal(-1.0f, trail.GetLifetime());
        Assert.Equal(-1.0f, trail.GetVertexDistance());
        Assert.Equal(-1.0f, trail.GetWidth());
    }

    [Fact]
    public void Model_view_empty_normalization_and_primitive_edges_cover_managed_state()
    {
        using var view = new ModelView();
        Assert.Equal(new BoundingBox(Vector3f.Zero, Vector3f.Zero), view.CalculateBoundingBox());
        Assert.Empty(view.ExportMaterialList().Names);
        view.Normalize();
        view.RepairBoneWeights();
        view.RecalculateBoneBoundingBoxes();

        var empty = new GeometryLODView { PrimitiveType = PrimitiveType.TriangleList };
        Assert.Equal(0u, empty.GetNumPrimitives());
        Assert.Equal(Vector3f.Zero, empty.CalculateCenter());
        empty.Normalize();
        empty.ForEachTriangle(static (_, _, _) => throw new InvalidOperationException());
        empty.InvalidateNormalsAndTangents();

        var points = new GeometryLODView { PrimitiveType = PrimitiveType.PointList };
        points.Indices.AddRange([0, 1]);
        Assert.Equal(2u, points.GetNumPrimitives());
        Assert.True(points.IsPointGeometry());
        Assert.False(points.IsTriangleGeometry());
        Assert.False(points.IsLineGeometry());
        var lines = new GeometryLODView { PrimitiveType = PrimitiveType.LineStrip };
        lines.Indices.AddRange([0, 1, 2]);
        Assert.Equal(2u, lines.GetNumPrimitives());
        Assert.True(lines.IsLineGeometry());
    }
}
