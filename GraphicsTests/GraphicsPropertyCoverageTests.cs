namespace RedBolt.Tests;

using System.Text;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsPropertyCoverageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GraphicsPropertyCoverageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Pass_and_technique_properties_cover_managed_state_branches()
    {
        using var technique = new Technique { Name = "Techniques/PropertyCoverage" };
        var pass = technique.CreatePass("BASE");

        Assert.Equal("base", pass.Name);
        Assert.Equal(Technique.BasePassIndex, pass.Index);
        Assert.Equal(BlendMode.Replace, pass.BlendMode);
        Assert.Equal(CullMode.Max, pass.CullMode);
        Assert.Equal(CompareMode.LessEqual, pass.DepthTestMode);
        Assert.True(pass.ColorWrite);
        Assert.True(pass.DepthWrite);
        Assert.False(pass.AlphaToCoverage);
        Assert.False(pass.IsAlphaMask);
        Assert.Equal(string.Empty, pass.VertexShader);
        Assert.Equal(string.Empty, pass.PixelShader);
        Assert.Equal(string.Empty, pass.VertexShaderDefines);
        Assert.Equal(string.Empty, pass.PixelShaderDefines);
        Assert.Equal(string.Empty, pass.VertexShaderDefineExcludes);
        Assert.Equal(string.Empty, pass.PixelShaderDefineExcludes);

        pass.SetBlendMode(BlendMode.PremulAlpha);
        pass.SetCullMode(CullMode.CounterClockwise);
        pass.SetDepthTestMode(CompareMode.Greater);
        pass.SetColorWrite(false);
        pass.SetDepthWrite(false);
        pass.SetAlphaToCoverage(true);
        pass.SetVertexShader("CoverageVS");
        pass.SetPixelShader("CoveragePS");
        pass.SetVertexShaderDefines("KEEP DROP");
        pass.SetPixelShaderDefines("ALPHAMASK KEEP DROP");
        pass.SetVertexShaderDefineExcludes("DROP");
        pass.SetPixelShaderDefineExcludes("DROP");
        pass.SetVertexTextureDefines(["VertexTexture"]);
        pass.SetPixelTextureDefines(["PixelTexture"]);
        pass.MarkShadersLoaded(17);

        Assert.Equal(BlendMode.PremulAlpha, pass.BlendMode);
        Assert.Equal(CullMode.CounterClockwise, pass.CullMode);
        Assert.Equal(CompareMode.Greater, pass.DepthTestMode);
        Assert.False(pass.ColorWrite);
        Assert.False(pass.DepthWrite);
        Assert.True(pass.AlphaToCoverage);
        Assert.True(pass.IsAlphaMask);
        Assert.Equal("CoverageVS", pass.VertexShader);
        Assert.Equal("CoveragePS", pass.PixelShader);
        Assert.Equal("KEEP DROP", pass.VertexShaderDefines);
        Assert.Equal("ALPHAMASK KEEP DROP", pass.PixelShaderDefines);
        Assert.Equal("DROP", pass.VertexShaderDefineExcludes);
        Assert.Equal("DROP", pass.PixelShaderDefineExcludes);
        Assert.Equal(17u, pass.ShadersLoadedFrameNumber);
        Assert.Equal(["VertexTexture"], pass.VertexTextureDefines);
        Assert.Equal(["PixelTexture"], pass.PixelTextureDefines);
        Assert.Equal("KEEP", pass.GetEffectiveVertexShaderDefines());
        Assert.Equal("ALPHAMASK KEEP", pass.GetEffectivePixelShaderDefines());

        var initialHash = pass.GetPipelineStateHash();
        Assert.Equal(initialHash, pass.GetPipelineStateHash());
        pass.SetDepthWrite(true);
        Assert.NotEqual(initialHash, pass.GetPipelineStateHash());

        var extraDefines = new StringHash("CoverageExtra");
        var extraVertexShaders = pass.GetVertexShaders(extraDefines);
        var extraPixelShaders = pass.GetPixelShaders(extraDefines);
        Assert.Same(extraVertexShaders, pass.GetVertexShaders(extraDefines));
        Assert.Same(extraPixelShaders, pass.GetPixelShaders(extraDefines));
        Assert.NotSame(pass.GetVertexShaders(), extraVertexShaders);
        Assert.NotSame(pass.GetPixelShaders(), extraPixelShaders);
        extraVertexShaders.Add(null);
        extraPixelShaders.Add(null);
        pass.ReleaseShaders();
        Assert.Empty(pass.GetVertexShaders());
        Assert.Empty(pass.GetPixelShaders());
        Assert.Single(extraVertexShaders);
        Assert.Null(extraVertexShaders[0]);
        Assert.Single(extraPixelShaders);
        Assert.Null(extraPixelShaders[0]);
        Assert.Same(pass.GetVertexShaders(), pass.GetVertexShaders(StringHash.Empty));
        Assert.Same(pass.GetPixelShaders(), pass.GetPixelShaders(StringHash.Empty));

        Assert.Same(pass, technique.CreatePass("base"));
        Assert.True(technique.HasPass(pass.Index));
        Assert.True(technique.HasPass("BASE"));
        Assert.False(technique.HasPass(-1));
        Assert.False(technique.HasPass(int.MaxValue));
        Assert.Same(pass, technique.GetPass("BASE"));
        Assert.Null(technique.GetPass(-1));
        Assert.Null(technique.GetPass(int.MaxValue));
        Assert.Contains("base", technique.GetPassNames());
        Assert.Contains(pass, technique.GetPasses());

        Assert.Same(technique, technique.CloneWithDefines(string.Empty, string.Empty));
        var clone = technique.CloneWithDefines("CLONE_VS", "CLONE_PS");
        Assert.Same(clone, technique.CloneWithDefines("CLONE_VS", "CLONE_PS"));
        Assert.Equal("CoverageVS", clone.GetPass("base")!.VertexShader);
        Assert.Equal("KEEP DROP CLONE_VS", clone.GetPass("base")!.VertexShaderDefines);
        Assert.Equal("ALPHAMASK KEEP DROP CLONE_PS", clone.GetPass("base")!.PixelShaderDefines);
        Assert.Equal("KEEP CLONE_VS", clone.GetPass("base")!.GetEffectiveVertexShaderDefines());
        Assert.Equal("ALPHAMASK KEEP CLONE_PS", clone.GetPass("base")!.GetEffectivePixelShaderDefines());

        Assert.Equal(int.MaxValue, Technique.GetPassIndex(string.Empty));
        Assert.Equal(Technique.BasePassIndex, Technique.GetPassIndex("BASE"));
        technique.RemovePass("missing");
        technique.RemovePass("base");
        Assert.False(technique.HasPass("base"));
        Assert.Empty(technique.GetPassNames());
    }

    [Fact]
    public void Technique_xml_parser_covers_render_state_literals()
    {
        var blendCases = new[]
        {
            ("blend-add", "add", BlendMode.Add),
            ("blend-multiply", "multiply", BlendMode.Multiply),
            ("blend-alpha", "alpha", BlendMode.Alpha),
            ("blend-addalpha", "addalpha", BlendMode.AddAlpha),
            ("blend-premulalpha", "premulalpha", BlendMode.PremulAlpha),
            ("blend-invdestalpha", "invdestalpha", BlendMode.InvDestAlpha),
            ("blend-subtract", "subtract", BlendMode.Subtract),
            ("blend-subtractalpha", "subtractalpha", BlendMode.SubtractAlpha),
            ("blend-decal", "decal", BlendMode.Decal),
            ("blend-default", "unknown", BlendMode.Replace)
        };
        var compareCases = new[]
        {
            ("compare-false", "false", CompareMode.Always),
            ("compare-always", "always", CompareMode.Always),
            ("compare-equal", "equal", CompareMode.Equal),
            ("compare-notequal", "notequal", CompareMode.NotEqual),
            ("compare-less", "less", CompareMode.Less),
            ("compare-lessequal", "lessequal", CompareMode.LessEqual),
            ("compare-greater", "greater", CompareMode.Greater),
            ("compare-greaterequal", "greaterequal", CompareMode.GreaterEqual),
            ("compare-default", "unknown", CompareMode.Less)
        };
        var cullCases = new[]
        {
            ("cull-none", "none", CullMode.None),
            ("cull-cw", "cw", CullMode.Clockwise),
            ("cull-ccw", "ccw", CullMode.CounterClockwise),
            ("cull-default", "unknown", CullMode.Max)
        };

        var xml = new StringBuilder(
            "<technique vs=\"GlobalVS\" ps=\"GlobalPS\" vsdefines=\"GLOBAL_VS\" psdefines=\"GLOBAL_PS\">");
        xml.Append("<pass name=\"shadow\" />");
        foreach (var (name, value, _) in blendCases)
            xml.Append($"<pass name=\"{name}\" blend=\"{value}\" />");
        foreach (var (name, value, _) in compareCases)
            xml.Append($"<pass name=\"{name}\" depthtest=\"{value}\" />");
        foreach (var (name, value, _) in cullCases)
            xml.Append($"<pass name=\"{name}\" cull=\"{value}\" />");
        xml.Append(
            "<pass name=\"explicit\" vs=\"ExplicitVS\" ps=\"ExplicitPS\" " +
            "vsdefines=\"LOCAL_VS\" psdefines=\"LOCAL_PS\" />");
        xml.Append("</technique>");

        using var source = new MemoryBuffer(Encoding.UTF8.GetBytes(xml.ToString()));
        using var technique = new Technique { Name = "Techniques/ParserCoverage" };
        Assert.True(technique.Load(source));

        foreach (var (name, _, expected) in blendCases)
            Assert.Equal(expected, technique.GetPass(name)!.BlendMode);
        foreach (var (name, _, expected) in compareCases)
            Assert.Equal(expected, technique.GetPass(name)!.DepthTestMode);
        foreach (var (name, _, expected) in cullCases)
            Assert.Equal(expected, technique.GetPass(name)!.CullMode);

        var shadow = technique.GetPass("shadow")!;
        Assert.Empty(shadow.VertexTextureDefines);
        Assert.Empty(shadow.PixelTextureDefines);
        Assert.Equal("GlobalVS", shadow.VertexShader);
        Assert.Equal("GLOBAL_VS ", shadow.VertexShaderDefines);
        Assert.Equal("GlobalPS", shadow.PixelShader);
        Assert.Equal("GLOBAL_PS ", shadow.PixelShaderDefines);

        var explicitPass = technique.GetPass("explicit")!;
        Assert.Equal("ExplicitVS", explicitPass.VertexShader);
        Assert.Equal("ExplicitPS", explicitPass.PixelShader);
        Assert.Equal("LOCAL_VS", explicitPass.VertexShaderDefines);
        Assert.Equal("LOCAL_PS", explicitPass.PixelShaderDefines);
        Assert.Contains("explicit", technique.GetPassNames());
        Assert.Equal(technique.GetPassNames().Count, technique.GetNumPasses());

        technique.ReleaseShaders();
        technique.RemovePass("shadow");
        Assert.False(technique.HasPass("shadow"));
    }

    [Fact]
    public void Animation_controller_properties_and_variant_attributes_cover_managed_paths()
    {
        const string animationName = "Animations/PropertyCoverage";
        var animation = new Animation { Name = animationName };
        animation.SetAnimationName("property");
        animation.SetLength(2.0f);
        Assert.True(Resources.AddManualResource(animation));

        using var scene = new Scene();
        var controller = scene.CreateChild("PropertyController").CreateComponent<AnimationController>();

        Assert.True(controller.IsAnimationThrottlingAllowed());
        Assert.False(controller.IsSkeletonReset());
        Assert.Equal(0u, controller.GetRevision());
        Assert.Equal(0, controller.GetAnimationLayer(-1));
        Assert.Equal(AnimationParameters.Empty, controller.GetAnimationParameters(-1));
        Assert.Equal(-1, controller.FindLastAnimation(null));
        Assert.Null(controller.GetLastAnimationParameters(null));
        Assert.Null(controller.GetMutableLastAnimationParameters(null));
        Assert.False(controller.IsPlaying((Animation?)null));
        Assert.Empty(controller.GetAnimationParameters());

        var destination = new List<AnimationParameters> { new() };
        controller.GetAnimationParameters(destination);
        Assert.Empty(destination);
        Assert.Throws<ArgumentNullException>(() =>
            controller.GetAnimationParameters((IList<AnimationParameters>)null!));

        controller.SetSkeletonReset(true);
        Assert.True(controller.IsSkeletonReset());
        Assert.False(controller.IsAnimationThrottlingAllowed());
        controller.SetSkeletonReset(false);

        var initial = new AnimationParameters(animation).Layer(1).Weight(0.5f);
        var index = controller.PlayNew(initial, 0.25f);
        Assert.Equal(0, index);
        Assert.Equal(1, controller.GetNumAnimations());
        Assert.Equal(1, controller.GetAnimationLayer(index));
        Assert.Equal(0.0f, controller.GetWeight(animationName));
        Assert.True(controller.UpdateAnimationWeight(animation, 0.5f));
        Assert.True(controller.UpdateAnimationSpeed(animation, 2.0f));
        Assert.True(controller.UpdateAnimationTime(animation, 0.75f));
        Assert.Equal(0.75f, controller.GetTime(animationName));
        Assert.Equal(2.0f, controller.GetSpeed(animationName));
        Assert.True(controller.IsPlaying(animationName));

        var mutable = controller.GetMutableLastAnimationParameters(animation);
        Assert.NotNull(mutable);
        mutable!.TargetWeightDelay = 0.0f;
        controller.UpdateAnimation(index, controller.GetAnimationParameters(index));

        var secondIndex = controller.PlayNewExclusive(new AnimationParameters(animation).Layer(2), 0.1f);
        Assert.Equal(1, secondIndex);
        Assert.Equal(2, controller.GetNumAnimations());
        Assert.Equal(secondIndex,
            controller.PlayExistingExclusive(new AnimationParameters(animation).Layer(2).Speed(3.0f)));
        controller.StopLayerExcept(2, secondIndex);
        controller.StopLayer(1);
        controller.StopLayer(99);
        controller.Stop(animation, 0.0f);
        controller.Fade(animation, 0.25f, 0.0f);
        Assert.True(controller.UpdateAnimationWeight(animation, 0.25f));
        Assert.True(controller.UpdateAnimationSpeed(animation, 1.0f));
        Assert.True(controller.UpdateAnimationTime(animation, 0.5f));

        var copiedParameters = controller.GetAnimationParameters();
        Assert.NotEmpty(copiedParameters);
        Assert.NotSame(controller.GetAnimationParameters(0), copiedParameters[0]);

        using var encoded = controller.GetAnimationsAttr();
        Assert.Equal(controller.GetNumAnimations(), encoded[0].GetInt());
        controller.SetAnimationsAttr(encoded);
        Assert.Equal(encoded[0].GetInt(), controller.GetNumAnimations());

        using var missingVariants = new VariantVector { new Variant(2) };
        controller.SetAnimationsAttr(missingVariants);
        Assert.Equal(2, controller.GetNumAnimations());
        Assert.Null(controller.GetAnimationParameters(0).GetAnimation());

        using var wrongType = new VariantVector { new Variant("not-a-count") };
        controller.SetAnimationsAttr(wrongType);
        Assert.Empty(controller.GetAnimationParameters());

        using var empty = new VariantVector();
        controller.SetAnimationsAttr(empty);
        Assert.Empty(controller.GetAnimationParameters());

        Assert.True(controller.Play(animationName, 3, false));
        Assert.True(controller.PlayExclusive(animationName, 4, true, 0.1f));
        Assert.True(controller.SetTime(animationName, 0.25f));
        Assert.True(controller.SetWeight(animationName, 0.5f));
        Assert.True(controller.SetSpeed(animationName, 1.5f));
        Assert.True(controller.Fade(animationName, 0.25f, 0.0f));
        Assert.True(controller.Stop(animationName, 0.0f));

        controller.Update(0.0f);
        controller.UpdatePose();
        controller.MarkAnimationStateTracksDirty();
        controller.SetEnabled(false);
        controller.SetEnabled(true);
        controller.RemoveAnimation(0);
        Assert.True(controller.GetRevision() > 0u);
    }

    [Fact]
    public void Animation_state_transform_and_attribute_blending_cover_managed_paths()
    {
        const AnimationChannel allChannels =
            AnimationChannel.Position | AnimationChannel.Rotation | AnimationChannel.Scale;

        using var animation = new Animation { Name = "Animations/StatePropertyCoverage" };
        animation.SetLength(1.0f);
        var transformTrack = animation.CreateTrack("Transform");
        transformTrack.ChannelMask = allChannels;
        transformTrack.AddKeyFrame(new AnimationKeyFrame(
            0.0f, Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One));
        transformTrack.AddKeyFrame(new AnimationKeyFrame(
            1.0f, new Vector3f(2.0f, 0.0f, 0.0f),
            new Quaternion(90.0f, Vector3f.Up), new Vector3f(3.0f, 1.0f, 1.0f)));

        using var scene = new Scene();
        var node = scene.CreateChild("AnimatedNode");

        using var fullState = new AnimationState();
        fullState.Initialize(animation, string.Empty, AnimationBlendMode.Additive);
        fullState.Update(false, 1.0f, 1.0f);
        fullState.AddNodeTrack(new NodeAnimationStateTrack { Track = transformTrack, Node = node });
        var fullOutput = new Dictionary<Node, NodeAnimationOutput>
        {
            [node] = new NodeAnimationOutput
            {
                Dirty = allChannels,
                LocalToParent = Transform.Identity
            }
        };
        fullState.CalculateNodeTracks(fullOutput);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), fullOutput[node].LocalToParent.Position);
        Assert.Equal(new Vector3f(3.0f, 1.0f, 1.0f), fullOutput[node].LocalToParent.Scale);
        Assert.NotEqual(Quaternion.IDENTITY, fullOutput[node].LocalToParent.Rotation);

        using var partialState = new AnimationState();
        partialState.Initialize(animation, string.Empty, AnimationBlendMode.Additive);
        partialState.Update(false, 1.0f, 0.5f);
        partialState.AddNodeTrack(new NodeAnimationStateTrack { Track = transformTrack, Node = node });
        var partialOutput = new Dictionary<Node, NodeAnimationOutput>
        {
            [node] = new NodeAnimationOutput
            {
                Dirty = allChannels,
                LocalToParent = Transform.Identity
            }
        };
        partialState.CalculateNodeTracks(partialOutput);
        Assert.Equal(1.0f, partialOutput[node].LocalToParent.Position.X);
        Assert.Equal(2.0f, partialOutput[node].LocalToParent.Scale.X);
        Assert.NotEqual(Quaternion.IDENTITY, partialOutput[node].LocalToParent.Rotation);

        var emptyTrack = animation.CreateTrack("Empty");
        emptyTrack.ChannelMask = AnimationChannel.Position;
        fullState.AddNodeTrack(new NodeAnimationStateTrack { Track = emptyTrack, Node = node });
        fullState.CalculateNodeTracks(fullOutput);

        var modelOutput = new List<ModelAnimationOutput>
        {
            new() { Dirty = allChannels, LocalToParent = Transform.Identity }
        };
        fullState.AddModelTrack(new ModelAnimationStateTrack
        {
            Track = transformTrack,
            Bone = new Bone { Animated = true },
            BoneIndex = 0
        });
        fullState.CalculateModelTracks(modelOutput);
        Assert.Equal(2.0f, modelOutput[0].LocalToParent.Position.X);

        var scaleTrack = animation.CreateTrack("ScaleOnly");
        scaleTrack.ChannelMask = AnimationChannel.Scale;
        scaleTrack.AddKeyFrame(new AnimationKeyFrame(0.0f, Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One));
        scaleTrack.AddKeyFrame(new AnimationKeyFrame(1.0f, Vector3f.Zero, Quaternion.IDENTITY, new Vector3f(3, 1, 1)));
        using var lerpState = new AnimationState();
        lerpState.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        lerpState.Update(false, 1.0f, 0.5f);
        lerpState.AddNodeTrack(new NodeAnimationStateTrack { Track = scaleTrack, Node = node });
        var lerpOutput = new Dictionary<Node, NodeAnimationOutput>
        {
            [node] = new NodeAnimationOutput
            {
                Dirty = AnimationChannel.Scale,
                LocalToParent = Transform.Identity
            }
        };
        lerpState.CalculateNodeTracks(lerpOutput);
        Assert.Equal(2.0f, lerpOutput[node].LocalToParent.Scale.X);

        AssertAdditiveAttribute(() => new Variant(2.0f), () => new Variant(6.0f), () => new Variant(10.0f),
            value => Assert.Equal(11.0f, value.GetFloat()));
        AssertAdditiveAttribute(() => new Variant(2.0), () => new Variant(6.0), () => new Variant(10.0),
            value => Assert.Equal(11.0, value.GetDouble()));
        AssertAdditiveAttribute(() => new Variant(2), () => new Variant(6), () => new Variant(10),
            value => Assert.Equal(11, value.GetInt()));
        AssertAdditiveAttribute(() => new Variant(2L), () => new Variant(6L), () => new Variant(10L),
            value => Assert.Equal(11L, value.GetInt64()));
        AssertAdditiveAttribute(() => new Variant(new Vector2f(2, 4)), () => new Variant(new Vector2f(6, 8)),
            () => new Variant(new Vector2f(10, 20)), value => Assert.Equal(new Vector2f(11, 21), value.GetVector2()));
        AssertAdditiveAttribute(() => new Variant(new Vector3f(2, 4, 6)),
            () => new Variant(new Vector3f(6, 8, 10)), () => new Variant(new Vector3f(10, 20, 30)),
            value => Assert.Equal(new Vector3f(11, 21, 31), value.GetVector3()));
        AssertAdditiveAttribute(() => new Variant(new Vector4f(2, 4, 6, 8)),
            () => new Variant(new Vector4f(6, 8, 10, 12)), () => new Variant(new Vector4f(10, 20, 30, 40)),
            value => Assert.Equal(new Vector4f(11, 21, 31, 41), value.GetVector4()));
        AssertAdditiveAttribute(() => new Variant(Quaternion.IDENTITY),
            () => new Variant(new Quaternion(90.0f, Vector3f.Up)), () => new Variant(Quaternion.IDENTITY),
            value => Assert.NotEqual(Quaternion.IDENTITY, value.GetQuaternion()));
        AssertAdditiveAttribute(() => new Variant(Color.Black), () => new Variant(Color.White),
            () => new Variant(new Color(0.25f, 0.25f, 0.25f, 1.0f)),
            value => Assert.NotEqual(new Color(0.25f, 0.25f, 0.25f, 1.0f), value.GetColor()));
        AssertAdditiveAttribute(() => new Variant(new Vector2i(2, 4)), () => new Variant(new Vector2i(6, 8)),
            () => new Variant(new Vector2i(10, 20)), value => Assert.Equal(new Vector2i(11, 21), value.GetIntVector2()));
        AssertAdditiveAttribute(() => new Variant(new Vector3i(2, 4, 6)),
            () => new Variant(new Vector3i(6, 8, 10)), () => new Variant(new Vector3i(10, 20, 30)),
            value => Assert.Equal(new Vector3i(11, 21, 31), value.GetIntVector3()));
        AssertAdditiveAttribute(() => new Variant("base"), () => new Variant("sample"), () => new Variant("old"),
            value => Assert.Equal("old", value.GetString()));

        using var nullState = new AnimationState();
        Assert.Null(nullState.GetAnimation());
        Assert.Null(nullState.GetModel());
        Assert.Equal(0.0f, nullState.GetLength());
        nullState.SetWeight(1.0f);
        nullState.SetTime(1.0f);
        nullState.Update(true, 1.0f, 1.0f);
        Assert.True(nullState.IsLooped());
        Assert.False(nullState.IsEnabled());
        nullState.OnTracksReady();
        Assert.False(nullState.AreTracksDirty());
        nullState.CalculateNodeTracks(new Dictionary<Node, NodeAnimationOutput>());
        nullState.CalculateModelTracks(new List<ModelAnimationOutput>());
        nullState.CalculateAttributeTracks(new Dictionary<AnimatedAttributeReference, Variant>());
    }

    [Fact]
    public void Animated_attribute_and_weak_state_properties_cover_reference_branches()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("AttributeNode");

        var nameIndex = node.Attributes
            .Select((attribute, index) => (attribute, index))
            .Single(item => item.attribute.Name == "Name")
            .index;
        var defaultReference = new AnimatedAttributeReference
        {
            Serializable = node,
            AttributeIndex = nameIndex,
            AttributeType = AnimatedAttributeType.Default
        };
        using var name = new Variant("Renamed");
        defaultReference.SetValue(name);
        Assert.Equal("Renamed", node.Name);

        const string variableName = "CoverageVariable";
        node.SetVar(variableName, 1);
        var variableReference = new AnimatedAttributeReference
        {
            Serializable = node,
            AttributeType = AnimatedAttributeType.NodeVariables,
            SubAttributeKey = new StringHash(variableName).Value
        };
        using var variableValue = new Variant(7);
        variableReference.SetValue(variableValue);
        Assert.Equal(7, node.GetVar(variableName));

        var animatedModel = node.CreateComponent<AnimatedModel>();
        var morphReference = new AnimatedAttributeReference
        {
            Serializable = animatedModel,
            AttributeType = AnimatedAttributeType.AnimatedModelMorphs,
            SubAttributeKey = 0
        };
        using var morphValue = new Variant(0.75f);
        morphReference.SetValue(morphValue);
        Assert.Equal(0.0f, animatedModel.GetMorphWeight(0));

        var equalReference = defaultReference;
        Assert.True(equalReference.Equals((object)defaultReference));
        Assert.False(equalReference.Equals(new object()));
        Assert.True(equalReference == defaultReference);
        Assert.False(equalReference != defaultReference);
        equalReference.AttributeIndex++;
        Assert.True(equalReference != defaultReference);
        _ = new AnimatedAttributeReference().ToHash();

        var nodeTrack = new NodeAnimationStateTrack { Node = node };
        Assert.Same(node, nodeTrack.Node);
        nodeTrack.Node = null;
        Assert.Null(nodeTrack.Node);

        var modelTrack = new ModelAnimationStateTrack { Node = node };
        Assert.Same(node, modelTrack.Node);
        modelTrack.Node = null;
        Assert.Null(modelTrack.Node);

        defaultReference.Serializable = null;
        Assert.Null(defaultReference.Serializable);

        using var state = new AnimationState();
        state.ConnectToAnimatedModel(animatedModel);
        Assert.Same(animatedModel, state.GetModel());
        state.OnTracksReady();
        state.ConnectToAnimatedModel(null);
        state.Initialize(null, "Root", AnimationBlendMode.Additive);
        state.MarkTracksDirty();
        Assert.True(state.AreTracksDirty());
        state.Dispose();
        Assert.Null(state.GetAnimation());
        Assert.Equal(string.Empty, state.GetStartBone());
    }

    [Fact]
    public void Decal_set_properties_and_attribute_serialization_stay_cpu_only()
    {
        using var scene = new Scene();
        var staticSet = scene.CreateChild("StaticDecals").CreateComponent<DecalSet>();
        var skinnedSet = scene.CreateChild("SkinnedDecals").CreateComponent<DecalSet>();

        var staticData = CreateDecalAttribute(false);
        staticSet.SetDecalsAttr(staticData);
        Assert.Equal(1u, staticSet.GetNumDecals());
        Assert.Equal(4u, staticSet.GetNumVertices());
        Assert.Equal(6u, staticSet.GetNumIndices());
        Assert.Equal(UpdateGeometryType.None, staticSet.GetUpdateGeometryType());
        Assert.Equal(staticData, staticSet.GetDecalsAttr());

        staticSet.SetMaxVertices(0);
        staticSet.SetMaxIndices(0);
        Assert.Equal(4u, staticSet.GetMaxVertices());
        Assert.Equal(6u, staticSet.GetMaxIndices());
        staticSet.SetOptimizeBufferSize(true);
        staticSet.SetOptimizeBufferSize(false);
        Assert.False(staticSet.GetOptimizeBufferSize());
        staticSet.RemoveDecals(0);
        staticSet.RemoveAllDecals();
        Assert.Equal(0u, staticSet.GetNumDecals());

        var skinnedData = CreateDecalAttribute(true);
        skinnedSet.SetDecalsAttr(skinnedData);
        Assert.Equal(1u, skinnedSet.GetNumDecals());
        Assert.Equal(4u, skinnedSet.GetNumVertices());
        Assert.Equal(6u, skinnedSet.GetNumIndices());
        Assert.Equal(UpdateGeometryType.WorkerThread, skinnedSet.GetUpdateGeometryType());
        Assert.Equal(skinnedData, skinnedSet.GetDecalsAttr());
        skinnedSet.RemoveAllDecals();
        Assert.Equal(0u, skinnedSet.GetNumDecals());

        FirstCubeTestData.AddResourceDirs(Resources);
        var material = Resources.Get<Material>("Materials/FirstCube");
        var materialSet = scene.CreateChild("MaterialDecals").CreateComponent<DecalSet>();
        materialSet.SetMaterialAttr(new ResourceRef(nameof(Material), material.Name));
        Assert.Same(material, materialSet.GetMaterial());
        materialSet.SetMaterialAttr(new ResourceRef(nameof(Material), string.Empty));
        Assert.Null(materialSet.GetMaterial());
    }

    private static void AssertAdditiveAttribute(
        Func<Variant> baseValueFactory, Func<Variant> sampledValueFactory, Func<Variant> oldValueFactory,
        Action<Variant> assertion)
    {
        using var animation = new Animation { Name = "Animations/AttributePropertyCoverage" };
        animation.SetLength(1.0f);
        var track = animation.CreateVariantTrack("Value");
        track.Interpolation = KeyFrameInterpolation.None;
        using var baseValue = baseValueFactory();
        using var sampledValue = sampledValueFactory();
        using var oldValue = oldValueFactory();
        track.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = baseValue });
        track.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = sampledValue });
        track.Commit();

        using var state = new AnimationState();
        state.Initialize(animation, string.Empty, AnimationBlendMode.Additive);
        state.Update(false, 1.0f, 0.25f);
        var attribute = new AnimatedAttributeReference { AttributeIndex = 1 };
        state.AddAttributeTrack(new AttributeAnimationStateTrack { Track = track, Attribute = attribute });

        var output = new Dictionary<AnimatedAttributeReference, Variant>
        {
            [attribute] = oldValue.Clone()
        };
        state.CalculateAttributeTracks(output);
        assertion(output[attribute]);
        output[attribute].Dispose();
    }

    private static byte[] CreateDecalAttribute(bool skinned)
    {
        using var buffer = new VectorBuffer();
        buffer.WriteBool(skinned);
        buffer.WriteVLE(1);
        buffer.WriteFloat(0.25f);
        buffer.WriteFloat(2.0f);
        buffer.WriteVLE(4);
        buffer.WriteVLE(6);

        for (var i = 0; i < 4; ++i)
        {
            var vertex = new DecalVertex(
                new Vector3f(i & 1, (i >> 1) & 1, 0.0f), Vector3f.Up)
            {
                TexCoord = new Vector2f(i * 0.25f, 1.0f - i * 0.25f),
                Tangent = new Vector4f(1.0f, 0.0f, 0.0f, 1.0f)
            };
            buffer.WriteVector3(vertex.Position);
            buffer.WriteVector3(vertex.Normal);
            buffer.WriteVector2(vertex.TexCoord);
            buffer.WriteVector4(vertex.Tangent);
            if (skinned)
            {
                for (var weight = 0; weight < 4; ++weight)
                    buffer.WriteFloat((weight + 1) * 0.1f);
                for (var index = 0; index < 4; ++index)
                    buffer.WriteUByte((byte)(index + 1));
            }
        }

        foreach (var index in new ushort[] { 0, 1, 2, 0, 2, 3 })
            buffer.WriteUShort(index);

        if (skinned)
        {
            buffer.WriteVLE(2);
            buffer.WriteString("Root");
            buffer.WriteUByte((byte)(BoneCollisionShape.Sphere | BoneCollisionShape.Box));
            buffer.WriteFloat(1.25f);
            buffer.WriteBoundingBox(new BoundingBox(new Vector3f(-1, -2, -3), new Vector3f(1, 2, 3)));
            buffer.WriteMatrix3x4(Matrix3x4.Identity);
            buffer.WriteString("NoShape");
            buffer.WriteUByte((byte)BoneCollisionShape.None);
            buffer.WriteMatrix3x4(Matrix3x4.Identity);
        }

        return buffer.GetBuffer();
    }
}
