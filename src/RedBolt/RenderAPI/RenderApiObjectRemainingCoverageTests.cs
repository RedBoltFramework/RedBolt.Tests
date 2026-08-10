namespace RedBolt.Tests;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

using DiligentCullMode = global::RedBolt.Bindings.Diligent.CullMode;
using DiligentFillMode = global::RedBolt.Bindings.Diligent.FillMode;
using DiligentStencilOp = global::RedBolt.Bindings.Diligent.StencilOp;
using DiligentTextureFormat = global::RedBolt.Bindings.Diligent.TextureFormat;
using RenderPipelineStateDesc = global::RedBolt.RenderAPI.PipelineStateDesc;
using RenderShaderType = global::RedBolt.RenderAPI.ShaderType;
using RenderStencilOp = global::RedBolt.RenderAPI.StencilOp;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderApiObjectRemainingCoverageTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RenderApiObjectRemainingCoverageTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Shader_reflection_covers_byte_name_parsing_and_invalid_lookup_paths()
    {
        var parserMethod = PrivateStaticMethod<ShaderProgramReflection>(
            "TryParseConstantBufferName",
            typeof(ReadOnlySpan<byte>),
            typeof(ShaderParameterGroup).MakeByRefType());
        var parser = (ParseByteConstantBufferName)parserMethod.CreateDelegate(
            typeof(ParseByteConstantBufferName));

        foreach (var name in new[] { "Frame", "Camera", "Zone", "Light", "Material", "Object", "Custom" })
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            Assert.True(parser(bytes, out var group));
            Assert.Equal(name, group.ToString());
        }

        Assert.False(parser("Unknown"u8, out var unknownGroup));
        Assert.Equal((ShaderParameterGroup)(-1), unknownGroup);

        using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());
        Assert.Null(reflection.GetUniformBuffer((ShaderParameterGroup)(-1)));
        Assert.Null(reflection.GetUniformBuffer(
            (ShaderParameterGroup)RenderApiLimits.MaxShaderParameterGroups));

        var getShaderTypes = PrivateStaticMethod<ShaderProgramReflection>(
            "GetShaderTypes", typeof(PipelineStateType));
        var exception = Assert.Throws<TargetInvocationException>(() =>
            getShaderTypes.Invoke(null, [(PipelineStateType)99]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Shader_reflection_covers_pointer_sanitization_and_uniform_size_fallbacks()
    {
        var sanitizeMethod = PrivateStaticMethod<ShaderProgramReflection>(
            "TrySanitizeUniformName", typeof(nint), typeof(string).MakeByRefType());

        foreach (var (value, expected, success) in new[]
                 {
                     ("prefixcColor", "Color", true),
                     ("prefix", string.Empty, false),
                     ("prefixc", string.Empty, false)
                 })
        {
            var pointer = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                object?[] arguments = [pointer, null];
                Assert.Equal(success, Assert.IsType<bool>(sanitizeMethod.Invoke(null, arguments)));
                Assert.Equal(expected, Assert.IsType<string>(arguments[1]));
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }

        var vectorSize = PrivateStaticMethod<ShaderProgramReflection>(
            "GetVectorUniformSize", typeof(ShaderCodeBasicType), typeof(int));
        Assert.Equal(0, Invoke<int>(vectorSize, ShaderCodeBasicType.ShaderCodeBasicTypeFloat, 5));

        var matrixSize = PrivateStaticMethod<ShaderProgramReflection>(
            "GetMatrixUniformSize", typeof(ShaderCodeBasicType), typeof(int), typeof(int));
        Assert.Equal(0, Invoke<int>(matrixSize, ShaderCodeBasicType.ShaderCodeBasicTypeFloat, 5, 2));

        var scalarSize = PrivateStaticMethod<ShaderProgramReflection>(
            "GetScalarUniformSize", typeof(ShaderCodeBasicType));
        Assert.Equal(sizeof(float), Invoke<int>(scalarSize, (ShaderCodeBasicType)99));

        var invalidShaderType = PrivateStaticMethod<ShaderProgramReflection>(
            "ToNativeShaderType", typeof(RenderShaderType));
        var exception = Assert.Throws<TargetInvocationException>(() =>
            invalidShaderType.Invoke(null, [(RenderShaderType)99]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Raw_buffer_headless_lifecycle_covers_empty_updates_and_bind_flag_fallbacks()
    {
        using (var empty = new RawBuffer(
                   new RawBufferParams(BufferType.Vertex, 0, 0, BufferFlag.None),
                   flags: DeviceObjectFlag.Headless))
        {
            empty.Restore();
            empty.UpdateRange(Array.Empty<byte>(), 0, 0);
            Assert.False(empty.IsDataLost());
            Assert.Equal(0, empty.Map().Length);
        }

        using var buffer = new RawBuffer(
            new RawBufferParams(BufferType.Uniform, 16, 0, BufferFlag.Shadowed),
            flags: DeviceObjectFlag.Headless);
        buffer.UpdateRange(Array.Empty<byte>(), 0, 0);

        var mapped = buffer.Map();
        mapped.Fill(0x3c);
        buffer.Unmap();
        Assert.All(buffer.GetShadowData(), static value => Assert.Equal(0x3c, value));

        var getBindFlags = PrivateStaticMethod<RawBuffer>(
            "GetBindFlags", typeof(BufferType), typeof(BufferFlag));
        var exception = Assert.Throws<TargetInvocationException>(() =>
            getBindFlags.Invoke(null, [(BufferType)99, BufferFlag.None]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Draw_queue_parameter_overloads_cover_type_size_and_group_mismatch_paths()
    {
        var queue = new DrawCommandQueue(_fixture.RenderDevice);
        using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());
        var uniformBuffers = GetPrivateField<UniformBufferReflection?[]>(reflection, "_uniformBuffers");
        var uniformBuffer = new UniformBufferReflection();
        SetPrivateProperty(uniformBuffer, nameof(UniformBufferReflection.Size), 128);
        SetPrivateProperty(uniformBuffer, nameof(UniformBufferReflection.Hash), 17u);
        uniformBuffers[(int)ShaderParameterGroup.Material] = uniformBuffer;

        var uniforms = GetPrivateField<Dictionary<StringHash, UniformReflection>>(reflection, "_uniforms");
        AddUniform(uniforms, "Variant", ShaderParameterGroup.Material, 0, 1);
        AddUniform(uniforms, "Scalar", ShaderParameterGroup.Material, 8, 8);
        AddUniform(uniforms, "Matrix3", ShaderParameterGroup.Material, 24, 1);
        AddUniform(uniforms, "Matrix3x4", ShaderParameterGroup.Material, 32, 1);
        AddUniform(uniforms, "VectorArray", ShaderParameterGroup.Material, 40, 1);
        AddUniform(uniforms, "Matrix3x4Array", ShaderParameterGroup.Material, 48, 1);
        AddUniform(uniforms, "Matrix4Array", ShaderParameterGroup.Material, 56, 1);
        AddUniform(uniforms, "WrongGroup", ShaderParameterGroup.Frame, 64, 4);

        SetPrivateField(queue, "_currentShaderProgramReflection", reflection);
        queue.Reset();
        SetPrivateField(queue, "_currentShaderProgramReflection", reflection);
        Assert.True(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material, differentFromPrevious: true));

        using var variant = new Variant(new Vector3f(1, 2, 3));
        queue.AddShaderParameter(new StringHash("Variant"), variant);
        queue.AddShaderParameter(new StringHash("Scalar"), 1.0f);
        var matrix3 = Matrix3.Identity;
        var matrix3x4 = new Matrix3x4(Matrix4.Identity);
        queue.AddShaderParameter(new StringHash("Matrix3"), matrix3);
        queue.AddShaderParameter(new StringHash("Matrix3x4"), matrix3x4);
        queue.AddShaderParameter(new StringHash("VectorArray"), new[] { Vector4f.One }.AsSpan());
        queue.AddShaderParameter(new StringHash("Matrix3x4Array"), new[] { matrix3x4 }.AsSpan());
        queue.AddShaderParameter(new StringHash("Matrix4Array"), new[] { Matrix4.Identity }.AsSpan());
        queue.AddShaderParameter(new StringHash("WrongGroup"), 1.0f);
    }

    [Fact]
    public void Draw_queue_headless_misses_and_empty_execution_are_noops()
    {
        var queue = new DrawCommandQueue(_fixture.RenderDevice);
        queue.ExecuteInContext(_fixture.RenderContext);

        using var cache = new PipelineStateCache();
        using var vertexShader = new RawShader(new ShaderBytecode(RenderShaderType.VS));
        using var pixelShader = new RawShader(new ShaderBytecode(RenderShaderType.PS));
        var state = cache.GetGraphicsPipelineState(new GraphicsPipelineStateDesc
        {
            DebugName = "HeadlessQueueCoverage",
            VertexShader = vertexShader,
            PixelShader = pixelShader
        });
        Assert.NotNull(state);

        SetPrivateField(state!, "_reflection", new ShaderProgramReflection(Array.Empty<RawShader?>()));
        queue.SetPipelineState(state!);
        queue.SetScissorRect(IntRect.Zero);
        queue.SetScissorRect(IntRect.Zero);
        queue.SetScissorRect(IntRect.Zero);

        Assert.False(queue.BeginShaderParameterGroup(ShaderParameterGroup.Frame));
        Assert.False(queue.BeginShaderParameterGroup(ShaderParameterGroup.Frame, differentFromPrevious: true));
        queue.AddShaderParameter(new StringHash("Missing"), 1.0f);
        queue.AddShaderResource(new StringHash("Missing"), (ITextureView)null!);
        queue.AddNullableShaderResource(new StringHash("Missing"), TextureType.Texture2D, null);
        queue.AddUnorderedAccessView(new StringHash("Missing"), null!, new RawTextureUAVKey());
        queue.CommitShaderResources();
        queue.CommitUnorderedAccessViews();

        var scissorRects = GetPrivateField<List<IntRect>>(queue, "_scissorRects");
        Assert.Equal(2, scissorRects.Count);
        state.Dispose();
    }

    [Fact]
    public void Render_context_headless_guards_reject_invalid_targets_without_native_calls()
    {
        var context = _fixture.RenderContext;
        context.ResetRenderTargets();
        context.SetFullViewport();
        context.SetViewport(new IntRect(-8, -4, 20, 30));
        Assert.Equal(IntRect.Zero, context.GetCurrentViewport());

        context.ClearDepthStencil(ClearTarget.None);
        context.ClearDepthStencil(ClearTarget.Depth);
        context.ClearRenderTarget(0, Color.Black);
        Assert.False(context.IsBoundAsRenderTarget(null));

        var tooManyTargets = new RenderTargetView[RenderApiLimits.MaxRenderTargets + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            context.SetRenderTargets(null, tooManyTargets));

        var mixedSwapChainViews = new[] { default(RenderTargetView) };
        context.SetRenderTargets(
            RenderTargetView.SwapChainDepthStencil(_fixture.RenderDevice), mixedSwapChainViews);

        var depthFormat = PrivateStaticMethod<RenderContext>(
            "IsBoundDepthStencilFormat", typeof(DiligentTextureFormat));
        Assert.True(Invoke<bool>(depthFormat, DiligentTextureFormat.TexFormatD24UnormS8Uint));
        Assert.True(Invoke<bool>(depthFormat, DiligentTextureFormat.TexFormatD32FloatS8x24Uint));
        Assert.False(Invoke<bool>(depthFormat, DiligentTextureFormat.TexFormatD32Float));
    }

    [Fact]
    public void Render_api_utils_cover_one_level_and_unknown_format_fallbacks()
    {
        Assert.Equal(1, RenderAPIUtils.GetMipLevelCount(Vector3i.One));
        Assert.Equal(Vector3i.One, RenderAPIUtils.GetMipLevelSize(Vector3i.One, 8));

        var unknown = RenderAPIUtils.GetTextureFormatInfo((DiligentTextureFormat)ushort.MaxValue);
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown, unknown.Format);
        Assert.Equal(
            DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.GetTextureFormatFromInternal((RenderBackend)99, 0));
        Assert.Equal(
            ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy((RenderBackend)99));

        var linear = DiligentTextureFormat.TexFormatRgba8Unorm;
        Assert.Equal(linear, RenderAPIUtils.SetTextureFormatSRGB(linear, sRgb: false));
        Assert.Equal(
            DiligentTextureFormat.TexFormatRgba8UnormSrgb,
            RenderAPIUtils.SetTextureFormatSRGB(linear));
    }

    [Fact]
    public void Pipeline_state_private_conversion_tables_cover_unknown_values()
    {
        var stencil = PrivateStaticMethod<PipelineState>("ToNative", typeof(RenderStencilOp));
        Assert.Equal(
            DiligentStencilOp.StencilOpUndefined,
            Invoke<DiligentStencilOp>(stencil, (RenderStencilOp)99));

        var fill = PrivateStaticMethod<PipelineState>("ToNative", typeof(global::RedBolt.Graphics.FillMode));
        Assert.Equal(
            DiligentFillMode.FillModeWireframe,
            Invoke<DiligentFillMode>(fill, (global::RedBolt.Graphics.FillMode)99));

        var cull = PrivateStaticMethod<PipelineState>("ToNative", typeof(global::RedBolt.Graphics.CullMode));
        Assert.Equal(
            DiligentCullMode.CullModeUndefined,
            Invoke<DiligentCullMode>(cull, (global::RedBolt.Graphics.CullMode)99));

        var element = PrivateStaticMethod<PipelineState>("GetElementType", typeof(VertexElementType));
        var exception = Assert.Throws<TargetInvocationException>(() =>
            element.Invoke(null, [(VertexElementType)99]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    private delegate bool ParseByteConstantBufferName(
        ReadOnlySpan<byte> name, out ShaderParameterGroup group);

    private static MethodInfo PrivateStaticMethod<T>(string name, params Type[] parameterTypes)
    {
        return typeof(T).GetMethod(
                   name, BindingFlags.NonPublic | BindingFlags.Static, binder: null,
                   parameterTypes, modifiers: null)
               ?? throw new InvalidOperationException($"Private method '{typeof(T).Name}.{name}' was not found.");
    }

    private static T Invoke<T>(MethodInfo method, params object?[] arguments)
    {
        return Assert.IsType<T>(method.Invoke(null, arguments));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetPrivateProperty<T>(object instance, string propertyName, T value)
    {
        var property = instance.GetType().GetProperty(
            propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    private static void AddUniform(
        Dictionary<StringHash, UniformReflection> uniforms,
        string name,
        ShaderParameterGroup group,
        int offset,
        int size)
    {
        var hash = new StringHash(name);
        uniforms[hash] = new UniformReflection(
            hash,
            group,
            offset,
            size,
            ShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            ShaderCodeBasicType.ShaderCodeBasicTypeFloat,
            1,
            1,
            0);
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PipelineStateCacheHeadlessCoverageTests
{
    public PipelineStateCacheHeadlessCoverageTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Pipeline_state_cache_covers_compute_failure_and_object_owner_guards()
    {
        using var cache = new PipelineStateCache();
        using var computeShader = new RawShader(new ShaderBytecode(RenderShaderType.CS));
        var computeDesc = new ComputePipelineStateDesc
        {
            DebugName = "HeadlessComputeCoverage",
            ComputeShader = computeShader
        };

        var state = cache.GetComputePipelineState(computeDesc);
        Assert.NotNull(state);
        Assert.Equal(PipelineStateType.Compute, state!.GetPipelineType());
        Assert.False(state.IsValid);
        Assert.Null(state.GetHandle());

        state.AcquireObjectID();
        _ = state.GetObjectID();
        state.ReleaseObjectID();
        state.Dispose();

        cache.ReleasePipelineState(new RenderPipelineStateDesc(computeDesc));

        Assert.Throws<ArgumentNullException>(() =>
            new PipelineState(null!, new RenderPipelineStateDesc()));
        Assert.Throws<ArgumentNullException>(() =>
            new PipelineState(cache, null!));
    }
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderApiObjectRemainingCoverageGraphicsTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RenderApiObjectRemainingCoverageGraphicsTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Draw_queue_records_each_graphics_draw_shape_and_compute_dispatch()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var graphicsState = CreateGraphicsPipelineState("RemainingQueueGraphics");
        var indexBuffer = new RawBuffer(
            new RawBufferParams(BufferType.Index, 16, 2, BufferFlag.Shadowed),
            flags: DeviceObjectFlag.Headless);

        queue.Reset();
        queue.SetPipelineState(graphicsState);
        queue.SetIndexBuffer(null);
        queue.Draw(1, 2);
        queue.DrawInstanced(2, 3, 4, 5);
        queue.SetIndexBuffer(indexBuffer);
        queue.DrawIndexed(3, 4);
        queue.DrawIndexed(4, 5, 6);
        queue.DrawIndexedInstanced(5, 6, 7, 8);
        queue.DrawIndexedInstanced(6, 7, 8, 9, 10);

        var commands = GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands");
        Assert.Equal(6, commands.Count);
        Assert.Equal(2, commands[0].IndexCount);
        Assert.Equal(5, commands[1].InstanceCount);
        Assert.Equal(0, commands[2].BaseVertexIndex);
        Assert.Equal(6, commands[3].BaseVertexIndex);
        Assert.Equal(7, commands[4].InstanceStart);
        Assert.Equal(8, commands[5].BaseVertexIndex);
        Assert.Equal(10, commands[5].InstanceCount);

        var computeState = CreateComputePipelineState("RemainingQueueCompute");
        queue.Reset();
        queue.SetPipelineState(computeState);
        queue.Dispatch(new Vector3i(2, 3, 4));

        var dispatch = Assert.Single(GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands"));
        Assert.Equal(new Vector3i(2, 3, 4), dispatch.NumGroups);

        graphicsState.Dispose();
        computeState.Dispose();
    }

    [GraphicsFact]
    public void Pipeline_state_software_path_handles_default_and_comparison_samplers()
    {
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        try
        {
            var vertexShader = _fixture.Graphics.GetShader(RenderShaderType.VS, "X_CopyTexture");
            var pixelShader = _fixture.Graphics.GetShader(RenderShaderType.PS, "X_CopyTexture");
            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);

            var defaultSamplerDesc = CreateGraphicsDescription(
                "RemainingPipelineDefaultSampler", vertexShader!, pixelShader!);
            var defaultSamplerState = _fixture.PipelineStateCache.GetGraphicsPipelineState(defaultSamplerDesc);
            Assert.NotNull(defaultSamplerState);

            var comparisonSamplerDesc = CreateGraphicsDescription(
                "RemainingPipelineComparisonSampler", vertexShader!, pixelShader!);
            var sampler = SamplerStateDesc.Default();
            sampler.FilterMode = TextureFilterMode.NearestAnisotropic;
            sampler.ShadowCompare = true;
            sampler.Anisotropy = 8;
            comparisonSamplerDesc.Samplers.Add(new StringHash("sAlbedo"), sampler);
            var comparisonSamplerState = _fixture.PipelineStateCache.GetGraphicsPipelineState(comparisonSamplerDesc);
            Assert.NotNull(comparisonSamplerState);

            defaultSamplerState!.Dispose();
            comparisonSamplerState!.Dispose();
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [GraphicsFact]
    public void Render_scope_formats_all_supported_placeholders_and_reuses_pool_storage()
    {
        var context = _fixture.RenderContext;
        context.SetSwapChainRenderTargets();
        if (!context.IsDebugScopeEnabled())
            return;

        using (new RenderScope(context, "literal"))
        using (new RenderScope(context, "integer {0}", -7))
        using (new RenderScope(context, "null second {1}", 3))
        using (new RenderScope(context, "both {0} {1}", 4, "name"))
        {
        }

        var first = RenderScope.Acquire(context, "pooled {0}", 1);
        Assert.NotNull(first);
        first!.Dispose();

        var second = RenderScope.Acquire(context, "reused {0}", 2);
        Assert.Same(first, second);
        second!.Dispose();
        Assert.Null(RenderScope.Acquire(null, "disabled {0}", 0));
    }

    private PipelineState CreateGraphicsPipelineState(string name)
    {
        var state = _fixture.PipelineStateCache.GetGraphicsPipelineState(
            CreateGraphicsDescription(
                name,
                new RawShader(new ShaderBytecode(RenderShaderType.VS)),
                new RawShader(new ShaderBytecode(RenderShaderType.PS))));
        Assert.NotNull(state);
        return state!;
    }

    private PipelineState CreateComputePipelineState(string name)
    {
        var state = _fixture.PipelineStateCache.GetComputePipelineState(new ComputePipelineStateDesc
        {
            DebugName = name,
            ComputeShader = new RawShader(new ShaderBytecode(RenderShaderType.CS))
        });
        Assert.NotNull(state);
        return state!;
    }

    private static GraphicsPipelineStateDesc CreateGraphicsDescription(
        string name, RawShader vertexShader, RawShader pixelShader)
    {
        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = name,
            VertexShader = vertexShader,
            PixelShader = pixelShader
        };
        desc.InputLayout.Size = 2;
        desc.InputLayout.Elements[0] = new InputLayoutElementDesc(
            0, 20, 0, 0, VertexElementType.Vector3, VertexElementSemantic.Position, 0);
        desc.InputLayout.Elements[1] = new InputLayoutElementDesc(
            0, 20, 12, 0, VertexElementType.Vector2, VertexElementSemantic.Normal, 0);
        desc.Output.NumRenderTargets = 1;
        desc.Output.RenderTargetFormats[0] = DiligentTextureFormat.TexFormatRgba8Unorm;
        return desc;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }
}
