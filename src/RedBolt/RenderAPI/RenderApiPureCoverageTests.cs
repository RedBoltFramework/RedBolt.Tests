namespace RedBolt.Tests;

using System.Reflection;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using DiligentBindFlags = global::RedBolt.Bindings.Diligent.BindFlags;
using DiligentComponentType = global::RedBolt.Bindings.Diligent.ComponentType;
using DiligentComparisonFunction = global::RedBolt.Bindings.Diligent.ComparisonFunction;
using DiligentCullMode = global::RedBolt.Bindings.Diligent.CullMode;
using DiligentFillMode = global::RedBolt.Bindings.Diligent.FillMode;
using DiligentPrimitiveTopology = global::RedBolt.Bindings.Diligent.PrimitiveTopology;
using DiligentShaderCodeBasicType = global::RedBolt.Bindings.Diligent.ShaderCodeBasicType;
using DiligentShaderCodeVariableClass = global::RedBolt.Bindings.Diligent.ShaderCodeVariableClass;
using DiligentShaderCodeVariableDesc = global::RedBolt.Bindings.Diligent.ShaderCodeVariableDesc;
using DiligentShaderType = global::RedBolt.Bindings.Diligent.ShaderType;
using DiligentStencilOp = global::RedBolt.Bindings.Diligent.StencilOp;
using DiligentValueType = global::RedBolt.Bindings.Diligent.ValueType;
using ApiCullMode = global::RedBolt.Graphics.CullMode;
using ApiFillMode = global::RedBolt.Graphics.FillMode;
using ApiInputLayoutDesc = global::RedBolt.RenderAPI.InputLayoutDesc;
using ApiPipelineStateDesc = global::RedBolt.RenderAPI.PipelineStateDesc;
using ApiStencilOp = global::RedBolt.RenderAPI.StencilOp;
using ApiTextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;
using ApiTextureFormatInfo = global::RedBolt.RenderAPI.TextureFormatInfo;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderApiPureCoverageTests
{
    public RenderApiPureCoverageTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Raw_buffer_covers_empty_and_shadowed_managed_lifecycles()
    {
        using (var empty = new RawBuffer(new RawBufferParams(BufferType.Uniform, 0, 0, BufferFlag.None)))
        {
            Assert.Equal(0, empty.GetSize());
            Assert.Null(empty.GetHandle());
            Assert.Equal(0, empty.Map().Length);

            empty.Update(Array.Empty<byte>());
            empty.Resolve();
            empty.Restore();
            empty.Destroy();
        }

        var initial = Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray();
        using var buffer = new RawBuffer(
            new RawBufferParams(BufferType.Vertex, initial.Length, 4, BufferFlag.Shadowed), initial);

        Assert.Equal(BufferType.Vertex, buffer.GetBufferType());
        Assert.Equal(4, buffer.GetStride());
        Assert.Equal(BufferFlag.Shadowed, buffer.GetFlags());
        Assert.True(buffer.IsShadowed());
        Assert.Same(buffer.GetShadowData(), buffer.GetShadowDataShared());
        Assert.Equal(initial, buffer.GetShadowData());

        var update = new byte[] { 91, 92, 93, 94 };
        buffer.UpdateRange(update, 3, update.Length);
        Assert.Equal(update, buffer.GetShadowData().Skip(3).Take(update.Length));

        var shared = buffer.GetShadowDataShared();
        buffer.UpdateRange(shared, 0, shared.Length);
        Assert.False(buffer.IsLocked());

        var mapped = buffer.Map();
        Assert.True(buffer.IsLocked());
        mapped[0] = 0xa5;
        buffer.Unmap();

        Assert.False(buffer.IsLocked());
        Assert.Equal((byte)0xa5, buffer.GetShadowData()[0]);
        buffer.Invalidate();
        Assert.Null(buffer.GetHandle());
    }

    [Fact]
    public void Raw_buffer_bind_flags_cover_each_managed_buffer_kind()
    {
        var method = PrivateStaticMethod<RawBuffer>("GetBindFlags", typeof(BufferType), typeof(BufferFlag));

        Assert.Equal(
            DiligentBindFlags.BindVertexBuffer,
            Invoke<DiligentBindFlags>(method, BufferType.Vertex, BufferFlag.None));
        Assert.Equal(
            DiligentBindFlags.BindIndexBuffer,
            Invoke<DiligentBindFlags>(method, BufferType.Index, BufferFlag.None));
        Assert.Equal(
            DiligentBindFlags.BindUniformBuffer,
            Invoke<DiligentBindFlags>(method, BufferType.Uniform, BufferFlag.None));

        var flags = Invoke<DiligentBindFlags>(
            method, BufferType.Vertex, BufferFlag.BindUnorderedAccess);
        Assert.Equal(
            DiligentBindFlags.BindVertexBuffer | DiligentBindFlags.BindUnorderedAccess,
            flags);
    }

    [Fact]
    public void Raw_buffer_rejects_dynamic_uav_and_initial_data_combinations()
    {
        Assert.Throws<ArgumentException>(() =>
            new RawBuffer(
                new RawBufferParams(
                    BufferType.Vertex,
                    16,
                    4,
                    BufferFlag.Dynamic | BufferFlag.BindUnorderedAccess),
                flags: DeviceObjectFlag.Headless));

        Assert.Throws<ArgumentException>(() =>
            new RawBuffer(
                new RawBufferParams(BufferType.Vertex, 16, 4, BufferFlag.Dynamic),
                new byte[16],
                DeviceObjectFlag.Headless));
    }

    [Fact]
    public void Pipeline_state_conversion_tables_cover_supported_render_values()
    {
        var primitiveTypes = new (PrimitiveType Value, DiligentPrimitiveTopology Expected)[]
        {
            (PrimitiveType.TriangleList, DiligentPrimitiveTopology.PrimitiveTopologyTriangleList),
            (PrimitiveType.LineList, DiligentPrimitiveTopology.PrimitiveTopologyLineList),
            (PrimitiveType.PointList, DiligentPrimitiveTopology.PrimitiveTopologyPointList),
            (PrimitiveType.TriangleStrip, DiligentPrimitiveTopology.PrimitiveTopologyTriangleStrip),
            (PrimitiveType.LineStrip, DiligentPrimitiveTopology.PrimitiveTopologyLineStrip),
            (PrimitiveType.TriangleFan, DiligentPrimitiveTopology.PrimitiveTopologyUndefined)
        };
        foreach (var (value, expected) in primitiveTypes)
        {
            var method = PrivateStaticMethod<PipelineState>("ToNative", typeof(PrimitiveType));
            Assert.Equal(expected, Invoke<DiligentPrimitiveTopology>(method, value));
        }

        var compareModes = new (CompareMode Value, DiligentComparisonFunction Expected)[]
        {
            (CompareMode.Always, DiligentComparisonFunction.ComparisonFuncAlways),
            (CompareMode.Equal, DiligentComparisonFunction.ComparisonFuncEqual),
            (CompareMode.NotEqual, DiligentComparisonFunction.ComparisonFuncNotEqual),
            (CompareMode.Less, DiligentComparisonFunction.ComparisonFuncLess),
            (CompareMode.LessEqual, DiligentComparisonFunction.ComparisonFuncLessEqual),
            (CompareMode.Greater, DiligentComparisonFunction.ComparisonFuncGreater),
            (CompareMode.GreaterEqual, DiligentComparisonFunction.ComparisonFuncGreaterEqual),
            (CompareMode.Max, DiligentComparisonFunction.ComparisonFuncUnknown)
        };
        foreach (var (value, expected) in compareModes)
        {
            var method = PrivateStaticMethod<PipelineState>("ToNative", typeof(CompareMode));
            Assert.Equal(expected, Invoke<DiligentComparisonFunction>(method, value));
        }

        var stencilOperations = new (ApiStencilOp Value, DiligentStencilOp Expected)[]
        {
            (ApiStencilOp.Keep, DiligentStencilOp.StencilOpKeep),
            (ApiStencilOp.Zero, DiligentStencilOp.StencilOpZero),
            (ApiStencilOp.Ref, DiligentStencilOp.StencilOpReplace),
            (ApiStencilOp.Incr, DiligentStencilOp.StencilOpIncrWrap),
            (ApiStencilOp.Decr, DiligentStencilOp.StencilOpDecrWrap)
        };
        foreach (var (value, expected) in stencilOperations)
        {
            var method = PrivateStaticMethod<PipelineState>("ToNative", typeof(ApiStencilOp));
            Assert.Equal(expected, Invoke<DiligentStencilOp>(method, value));
        }

        var fillMethod = PrivateStaticMethod<PipelineState>("ToNative", typeof(ApiFillMode));
        Assert.Equal(
            DiligentFillMode.FillModeSolid,
            Invoke<DiligentFillMode>(fillMethod, ApiFillMode.Solid));
        Assert.Equal(
            DiligentFillMode.FillModeWireframe,
            Invoke<DiligentFillMode>(fillMethod, ApiFillMode.Wireframe));
        Assert.Equal(
            DiligentFillMode.FillModeWireframe,
            Invoke<DiligentFillMode>(fillMethod, ApiFillMode.Point));
        Assert.Equal(
            DiligentFillMode.FillModeWireframe,
            Invoke<DiligentFillMode>(fillMethod, ApiFillMode.Max));

        var cullMethod = PrivateStaticMethod<PipelineState>("ToNative", typeof(ApiCullMode));
        Assert.Equal(
            DiligentCullMode.CullModeNone,
            Invoke<DiligentCullMode>(cullMethod, ApiCullMode.None));
        Assert.Equal(
            DiligentCullMode.CullModeBack,
            Invoke<DiligentCullMode>(cullMethod, ApiCullMode.CounterClockwise));
        Assert.Equal(
            DiligentCullMode.CullModeFront,
            Invoke<DiligentCullMode>(cullMethod, ApiCullMode.Clockwise));
        Assert.Equal(
            DiligentCullMode.CullModeUndefined,
            Invoke<DiligentCullMode>(cullMethod, ApiCullMode.Max));

        var elementMethod = PrivateStaticMethod<PipelineState>("GetElementType", typeof(VertexElementType));
        var elementTypes = new (VertexElementType Value, uint Components, DiligentValueType Type, bool Normalized)[]
        {
            (VertexElementType.Int, 1, DiligentValueType.VtInt32, false),
            (VertexElementType.Float, 1, DiligentValueType.VtFloat32, false),
            (VertexElementType.Vector2, 2, DiligentValueType.VtFloat32, false),
            (VertexElementType.Vector3, 3, DiligentValueType.VtFloat32, false),
            (VertexElementType.Vector4, 4, DiligentValueType.VtFloat32, false),
            (VertexElementType.UByte4, 4, DiligentValueType.VtUint8, false),
            (VertexElementType.UByte4Norm, 4, DiligentValueType.VtUint8, true)
        };
        foreach (var (value, components, type, normalized) in elementTypes)
        {
            var mapped = Assert.IsType<(uint Components, DiligentValueType Type, bool Normalized)>(
                elementMethod.Invoke(null, [value]));
            Assert.Equal(components, mapped.Components);
            Assert.Equal(type, mapped.Type);
            Assert.Equal(normalized, mapped.Normalized);
        }
    }

    [Fact]
    public void Shader_reflection_uniform_size_fallbacks_cover_arrays_and_unknown_classes()
    {
        var method = PrivateStaticMethod<ShaderProgramReflection>(
            "GetUniformSize", typeof(DiligentShaderCodeVariableDesc));

        Assert.Equal(32, Invoke<int>(method, CreateVariable(
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassScalar, arraySize: 2)));
        Assert.Equal(32, Invoke<int>(method, CreateVariable(
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassVector, arraySize: 2, numColumns: 2)));
        Assert.Equal(64, Invoke<int>(method, CreateVariable(
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns,
            arraySize: 2, numRows: 3, numColumns: 2)));
        Assert.Equal(64, Invoke<int>(method, CreateVariable(
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows,
            arraySize: 2, numRows: 2, numColumns: 3)));
        Assert.Equal(0, Invoke<int>(method, CreateVariable(
            (DiligentShaderCodeVariableClass)99, arraySize: 2)));
        Assert.Equal(32, Invoke<int>(method, CreateVariable(
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassMatrixRows,
            numRows: 2, numColumns: 3)));
    }

    [Fact]
    public void Shader_reflection_constructor_disposes_when_shader_enumeration_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ShaderProgramReflection(ThrowingShaders()));
    }

    [Fact]
    public void Render_api_value_keys_and_sampler_descriptors_cover_copy_and_hash_paths()
    {
        var defaultSampler = SamplerStateDesc.Default();
        Assert.Equal(TextureFilterMode.Default, defaultSampler.FilterMode);
        Assert.Equal(ApiTextureAddressMode.Clamp, defaultSampler[TextureCoordinate.U]);
        Assert.Equal(ApiTextureAddressMode.Clamp, defaultSampler[TextureCoordinate.V]);
        Assert.Equal(ApiTextureAddressMode.Clamp, defaultSampler[TextureCoordinate.W]);

        var nearest = SamplerStateDesc.Nearest(ApiTextureAddressMode.Wrap);
        var bilinear = SamplerStateDesc.Bilinear(ApiTextureAddressMode.Mirror);
        var trilinear = SamplerStateDesc.Trilinear(ApiTextureAddressMode.Clamp);
        Assert.Equal(TextureFilterMode.Nearest, nearest.FilterMode);
        Assert.Equal(TextureFilterMode.Bilinear, bilinear.FilterMode);
        Assert.Equal(TextureFilterMode.Trilinear, trilinear.FilterMode);

        var sampler = new SamplerStateDesc
        {
            FilterMode = TextureFilterMode.Anisotropic,
            Anisotropy = 8,
            ShadowCompare = true,
            MinLod = 1.5f,
            MaxLod = 9.5f
        };
        sampler[TextureCoordinate.U] = ApiTextureAddressMode.Wrap;
        sampler[TextureCoordinate.V] = ApiTextureAddressMode.Mirror;
        sampler[TextureCoordinate.W] = ApiTextureAddressMode.Clamp;
        var samplerCopy = sampler;

        Assert.True(sampler.Equals(samplerCopy));
        Assert.True(sampler.Equals((object)samplerCopy));
        Assert.False(sampler.Equals((object?)null));
        Assert.False(sampler.Equals((object)nearest));
        Assert.Equal(sampler.GetHashCode(), samplerCopy.GetHashCode());
        Assert.Equal(sampler.ToHash(), samplerCopy.ToHash());

        var readOnly = new RawTextureUAVKey().ReadOnly();
        var writeOnly = new RawTextureUAVKey().WriteOnly();
        var level = new RawTextureUAVKey().FromLevel(2, 3);
        var slice = level.FromSlice(4, 5);
        Assert.False(readOnly.CanWrite);
        Assert.True(readOnly.CanRead);
        Assert.True(writeOnly.CanWrite);
        Assert.False(writeOnly.CanRead);
        Assert.Equal(2, slice.FirstLevel);
        Assert.Equal(3, slice.NumLevels);
        Assert.Equal(4, slice.FirstSlice);
        Assert.Equal(5, slice.NumSlices);

        var uncompressed = new ApiTextureFormatInfo(
            "rgba", TextureFormat.TexFormatRgba8Unorm, 1, 4,
            DiligentComponentType.ComponentTypeUnorm, false, 1, 1);
        var compressed = new ApiTextureFormatInfo(
            "bc", TextureFormat.TexFormatBc1Unorm, 8, 3,
            DiligentComponentType.ComponentTypeCompressed, false, 4, 4);
        Assert.Equal(4, uncompressed.GetElementSize());
        Assert.Equal(8, compressed.GetElementSize());
    }

    [Fact]
    public void Render_api_helpers_cover_platform_and_policy_fallback_branches()
    {
        Assert.False(RenderAPIUtils.IsOpenGLESBackend(RenderBackend.OpenGL));
        Assert.False(RenderAPIUtils.IsOpenGLESBackend(RenderBackend.Vulkan));
        Assert.False(RenderAPIUtils.IsMetalBackend(RenderBackend.Vulkan));
        Assert.False(RenderAPIUtils.IsMetalBackend(RenderBackend.OpenGL));

        Assert.Equal(
            ShaderTranslationPolicy.Verbatim,
            RenderAPIUtils.SelectShaderTranslationPolicy(
                RenderBackend.OpenGL, (ShaderTranslationPolicy)99));
    }

    [Fact]
    public void Sampler_descriptors_cover_invalid_coordinates_and_capacity_guard()
    {
        var sampler = new SamplerStateDesc();
        var invalidCoordinate = (TextureCoordinate)99;
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = sampler[invalidCoordinate]);
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler[invalidCoordinate] = ApiTextureAddressMode.Clamp);

        var samplers = new ImmutableSamplersDesc();
        for (var index = 0; index < RenderApiLimits.MaxNumImmutableSamplers; ++index)
            samplers.Add(new StringHash($"Sampler{index}"), SamplerStateDesc.Default());

        Assert.Equal(RenderApiLimits.MaxNumImmutableSamplers, samplers.Size);
        var overflowException = Record.Exception(() =>
            samplers.Add(new StringHash("Overflow"), SamplerStateDesc.Default()));
        Assert.True(overflowException is null || overflowException.GetType().Name == "DebugAssertException");
        Assert.Equal(RenderApiLimits.MaxNumImmutableSamplers, samplers.Size);
    }

    [Fact]
    public void Inline_render_descriptions_cover_equality_hash_and_clear_branches()
    {
        var layout = new ApiInputLayoutDesc { Size = 1 };
        layout.Elements[0] = new InputLayoutElementDesc(
            0, 24, 12, 0, VertexElementType.Vector3, VertexElementSemantic.Normal, 0);
        var layoutCopy = layout;
        Assert.True(layout.Equals(layoutCopy));
        Assert.True(layout.Equals((object)layoutCopy));
        Assert.False(layout.Equals((object?)null));
        Assert.False(layout.Equals((object)"layout"));

        var differentLayout = layout;
        differentLayout.Elements[0] = differentLayout.Elements[0] with { ElementOffset = 16 };
        Assert.False(layout.Equals(differentLayout));
        Assert.False(layout.Equals(new ApiInputLayoutDesc()));
        _ = layout.GetHashCode();
        _ = layout.ToHash();

        var samplers = new ImmutableSamplersDesc();
        Assert.True(samplers.Equals(new ImmutableSamplersDesc()));
        samplers.Add(new StringHash("Albedo"), SamplerStateDesc.Bilinear());
        var sameSamplers = samplers;
        Assert.True(samplers.Equals(sameSamplers));
        Assert.True(samplers.Equals((object)sameSamplers));
        Assert.False(samplers.Equals((object?)null));
        Assert.False(samplers.Equals(new ImmutableSamplersDesc()));

        var differentName = samplers;
        differentName.Names[0] = new StringHash("Normal");
        Assert.False(samplers.Equals(differentName));
        var differentDescription = samplers;
        differentDescription.Descriptions[0] = SamplerStateDesc.Trilinear();
        Assert.False(samplers.Equals(differentDescription));
        _ = samplers.GetHashCode();
        _ = samplers.ToHash();
        samplers.Clear();
        Assert.Equal(0, samplers.Size);

        var output = new PipelineStateOutputDesc();
        Assert.Equal(TextureFormat.TexFormatUnknown, output.DepthStencilFormat);
        Assert.Equal(1, output.MultiSample);
        output.DepthStencilFormat = TextureFormat.TexFormatD24UnormS8Uint;
        output.NumRenderTargets = 1;
        output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        var sameOutput = output;
        Assert.True(output.Equals(sameOutput));
        Assert.True(output.Equals((object)sameOutput));
        Assert.False(output.Equals((object?)null));
        Assert.False(output.Equals(new PipelineStateOutputDesc()));

        var differentTarget = output;
        differentTarget.RenderTargetFormats[0] = TextureFormat.TexFormatBgra8Unorm;
        Assert.False(output.Equals(differentTarget));
        var differentDepth = output;
        differentDepth.DepthStencilFormat = TextureFormat.TexFormatD32Float;
        Assert.False(output.Equals(differentDepth));
        var differentCount = output;
        differentCount.NumRenderTargets = 2;
        Assert.False(output.Equals(differentCount));
        var differentSamples = output;
        differentSamples.MultiSample = 4;
        Assert.False(output.Equals(differentSamples));
        _ = output.GetHashCode();
        _ = output.ToHash();
    }

    [Fact]
    public void Pipeline_state_descriptions_cover_variant_value_semantics()
    {
        using var vertexShader = new RawShader(new ShaderBytecode(ShaderType.VS));
        using var pixelShader = new RawShader(new ShaderBytecode(ShaderType.PS));
        using var computeShader = new RawShader(new ShaderBytecode(ShaderType.CS));

        var graphics = new GraphicsPipelineStateDesc
        {
            DebugName = "managed-graphics",
            ColorWriteEnabled = true,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexShader = vertexShader,
            PixelShader = pixelShader
        };
        graphics.InputLayout.Size = 1;
        graphics.InputLayout.Elements[0] = new InputLayoutElementDesc(
            0, 16, 0, 0, VertexElementType.Vector4, VertexElementSemantic.Position, 0);
        graphics.Output.NumRenderTargets = 1;
        graphics.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        graphics.Samplers.Add(new StringHash("Albedo"), SamplerStateDesc.Bilinear());

        Assert.True(graphics.IsInitialized());
        Assert.True(graphics.ToHash() > 0u);
        Assert.Equal(graphics.ToHash(), unchecked((uint)graphics.GetHashCode()));
        Assert.True(graphics.Equals(graphics));
        Assert.True(graphics.Equals((object)graphics));
        Assert.False(graphics.Equals((object?)null));

        var graphicsWithoutPixel = graphics;
        graphicsWithoutPixel.PixelShader = null;
        Assert.False(graphicsWithoutPixel.IsInitialized());

        var differentGraphics = graphics;
        differentGraphics.ReadOnlyDepth = true;
        Assert.False(graphics.Equals(differentGraphics));

        var compute = new ComputePipelineStateDesc
        {
            DebugName = "managed-compute",
            ComputeShader = computeShader
        };
        compute.Samplers.Add(new StringHash("ComputeSampler"), SamplerStateDesc.Trilinear());
        Assert.True(compute.IsInitialized());
        Assert.True(compute.ToHash() > 0u);
        Assert.True(compute.Equals(compute));
        Assert.True(compute.Equals((object)compute));
        Assert.False(compute.Equals((object?)null));

        var computeWithoutShader = compute;
        computeWithoutShader.ComputeShader = null;
        Assert.False(computeWithoutShader.IsInitialized());
        Assert.False(compute.Equals(computeWithoutShader));

        var graphicsDesc = new ApiPipelineStateDesc(graphics);
        var graphicsDescCopy = new ApiPipelineStateDesc(graphics);
        var computeDesc = new ApiPipelineStateDesc(compute);
        var defaultDesc = new ApiPipelineStateDesc();

        Assert.True(graphicsDesc.Equals(graphicsDescCopy));
        Assert.False(graphicsDesc.Equals(computeDesc));
        Assert.False(graphicsDesc.Equals((ApiPipelineStateDesc?)null));
        Assert.False(graphicsDesc.Equals((object?)null));
        Assert.Equal(PipelineStateType.Graphics, graphicsDesc.GetPipelineType());
        Assert.Equal(PipelineStateType.Compute, computeDesc.GetPipelineType());
        Assert.Equal("managed-graphics", graphicsDesc.GetDebugName());
        Assert.Equal("managed-compute", computeDesc.GetDebugName());
        Assert.Equal(PipelineStateType.Graphics, defaultDesc.GetPipelineType());
        Assert.NotNull(defaultDesc.AsGraphics());
        Assert.Null(defaultDesc.AsCompute());
        _ = graphicsDesc.GetHashCode();
        _ = computeDesc.GetHashCode();
        _ = defaultDesc.ToHash();
    }

    [Fact]
    public void Pipeline_state_cache_reuses_and_releases_device_free_invalid_shader_states()
    {
        using var cache = new PipelineStateCache();
        using var vertexShader = new RawShader(new ShaderBytecode(ShaderType.VS));
        using var pixelShader = new RawShader(new ShaderBytecode(ShaderType.PS));

        Assert.Null(cache.GetHandle());
        Assert.Empty(cache.GetCachedData());
        Assert.Null(cache.GetGraphicsPipelineState(new GraphicsPipelineStateDesc()));
        Assert.Null(cache.GetComputePipelineState(new ComputePipelineStateDesc()));

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = "device-free",
            VertexShader = vertexShader,
            PixelShader = pixelShader
        };
        var first = cache.GetGraphicsPipelineState(desc);
        var same = cache.GetGraphicsPipelineState(desc);

        Assert.NotNull(first);
        Assert.Same(first, same);
        Assert.Equal(PipelineStateType.Graphics, first!.GetPipelineType());
        Assert.Equal("device-free", first.GetDesc().GetDebugName());
        Assert.False(first.IsValid);
        Assert.Null(first.GetReflection());
        Assert.Null(first.GetHandle());
        Assert.Null(first.GetShaderResourceBinding());

        first.Restore();
        first.Destroy();
        first.Dispose();

        var recreated = cache.GetGraphicsPipelineState(desc);
        Assert.NotNull(recreated);
        Assert.NotSame(first, recreated);
        recreated!.Dispose();
        Assert.Empty(cache.GetCachedData());
    }

    [Fact]
    public void Shader_reflection_value_wrappers_cover_empty_and_populated_managed_state()
    {
        using var empty = new ShaderProgramReflection(Array.Empty<RawShader?>());
        Assert.Empty(empty.Uniforms);
        Assert.Empty(empty.ShaderResources);
        Assert.Empty(empty.UnorderedAccessViews);
        Assert.Null(empty.GetUniformBuffer(ShaderParameterGroup.Frame));
        Assert.Null(empty.GetUniform(new StringHash("Missing")));
        Assert.Null(empty.GetShaderResource(new StringHash("Missing")));
        Assert.Null(empty.GetUnorderedAccessView(new StringHash("Missing")));

        using var uniformBuffer = new UniformBufferReflection();
        Assert.Equal(0, uniformBuffer.Size);
        Assert.Equal(0u, uniformBuffer.Hash);
        Assert.Empty(uniformBuffer.Variables);
        Assert.True(uniformBuffer.InternalName.Equals(string.Empty));

        using var resource = new ShaderResourceReflection();
        Assert.True(resource.InternalName.Equals(string.Empty));
        Assert.Null(resource.Variable);

        var uniform = new UniformReflection(
            new StringHash("Color"), ShaderParameterGroup.Material, 4, 16,
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassVector,
            DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 4, 1, 0);
        Assert.Equal(uniform, uniform with { });
        Assert.False(uniform.Equals(uniform with { Offset = 8 }));
        Assert.False(uniform.Equals((object?)null));
    }

    [Fact]
    public void Shader_reflection_parsers_and_size_helpers_cover_managed_branches()
    {
        foreach (var name in new[] { "Frame", "Camera", "Zone", "Light", "Material", "Object", "Custom" })
        {
            var parsed = InvokeConstantBufferParser(name);
            Assert.True(parsed.Success);
            Assert.Equal(name, parsed.Group.ToString());
        }
        Assert.False(InvokeConstantBufferParser("Unknown").Success);

        foreach (var (shaderType, nativeShaderType) in new[]
                 {
                     (ShaderType.VS, DiligentShaderType.ShaderTypeVertex),
                     (ShaderType.PS, DiligentShaderType.ShaderTypePixel),
                     (ShaderType.GS, DiligentShaderType.ShaderTypeGeometry),
                     (ShaderType.HS, DiligentShaderType.ShaderTypeHull),
                     (ShaderType.DS, DiligentShaderType.ShaderTypeDomain),
                     (ShaderType.CS, DiligentShaderType.ShaderTypeCompute),
                 })
        {
            var mapped = InvokePrivateStatic<ShaderProgramReflection, DiligentShaderType>(
                "ToNativeShaderType", [typeof(ShaderType)], shaderType);
            Assert.Equal(nativeShaderType, mapped);
        }

        var shaderTypes = InvokePrivateStaticAssignable<ShaderProgramReflection, IReadOnlyList<ShaderType>>(
            "GetShaderTypes", [typeof(PipelineStateType)], PipelineStateType.Graphics);
        Assert.Equal(
            new[] { ShaderType.VS, ShaderType.PS, ShaderType.GS, ShaderType.HS, ShaderType.DS },
            shaderTypes);
        shaderTypes = InvokePrivateStaticAssignable<ShaderProgramReflection, IReadOnlyList<ShaderType>>(
            "GetShaderTypes", [typeof(PipelineStateType)], PipelineStateType.Compute);
        Assert.Equal(new[] { ShaderType.CS }, shaderTypes);

        Assert.True(InvokeGlSanitizer("cLightMatrices[0]").Success);
        Assert.Equal("LightMatrices", InvokeGlSanitizer("Light.cLightMatrices[0]").Name);
        Assert.False(InvokeGlSanitizer(string.Empty).Success);
        Assert.False(InvokeGlSanitizer("cLightMatrices[1]").Success);
        Assert.False(InvokeGlSanitizer("LightMatrices").Success);

        Assert.Equal("Albedo", InvokeResourceSanitizer("sAlbedo", "TrySanitizeSrvName").Name);
        Assert.False(InvokeResourceSanitizer("Albedo", "TrySanitizeSrvName").Success);
        Assert.Equal("Output", InvokeResourceSanitizer("uOutput", "TrySanitizeUavName").Name);
        Assert.False(InvokeResourceSanitizer("Output", "TrySanitizeUavName").Success);

        Assert.Equal("Color", InvokeUniformPointerSanitizer("prefixcColor").Name);
        Assert.False(InvokeUniformPointerSanitizer("prefixc").Success);
        Assert.False(InvokeUniformPointerSanitizer("prefix").Success);
        Assert.False(InvokeUniformPointerSanitizer(nint.Zero).Success);

        Assert.Equal(sizeof(float), InvokeSize("GetScalarUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat));
        Assert.Equal(sizeof(double), InvokeSize("GetScalarUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeInt64));
        Assert.Equal(sizeof(double), InvokeSize("GetScalarUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeUint64));
        Assert.Equal(sizeof(double), InvokeSize("GetScalarUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeDouble));

        foreach (var count in new[] { 1, 2, 3, 4 })
        {
            Assert.Equal(
                count * sizeof(float),
                InvokeSize("GetVectorUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, count));
        }
        Assert.Equal(
            0,
            InvokeSize("GetVectorUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 0));
        Assert.Equal(
            32,
            InvokeSize("GetVectorArrayUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 3, 2));
        Assert.Equal(
            24,
            InvokeSize("GetMatrixUniformSize", DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 2, 3));
        Assert.Equal(16, InvokeSize("Align", 13, 16));

        var combine = PrivateStaticMethod<ShaderProgramReflection>("CombineHash", typeof(uint), typeof(uint));
        Assert.Equal(17u, Invoke<uint>(combine, 0u, 0u));
        Assert.Equal(unchecked(2u * 37u + 3u + 17u), Invoke<uint>(combine, 2u, 3u));
    }

    [Fact]
    public void Shader_reflection_managed_collections_recalculate_hashes_and_keep_names()
    {
        using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());

        InvokePrivateInstance(reflection, "AddUniformBuffer",
            [typeof(ShaderParameterGroup), typeof(string), typeof(int)],
            ShaderParameterGroup.Material, "Material", 32);
        InvokePrivateInstance(reflection, "AddUniformBuffer",
            [typeof(ShaderParameterGroup), typeof(string), typeof(int)],
            ShaderParameterGroup.Material, "DifferentMaterial", 64);
        InvokePrivateInstance(reflection, "AddUniformBuffer",
            [typeof(ShaderParameterGroup), typeof(string), typeof(int)],
            ShaderParameterGroup.Zone, "Zone", 0);

        var color = new StringHash("Color");
        InvokePrivateInstance(reflection, "AddUniform",
            [typeof(string), typeof(ShaderParameterGroup), typeof(int), typeof(int),
             typeof(DiligentShaderCodeVariableClass), typeof(DiligentShaderCodeBasicType),
             typeof(int), typeof(int), typeof(int)],
            "Color", ShaderParameterGroup.Material, 4, 16,
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassVector,
            DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 4, 1, 0);
        InvokePrivateInstance(reflection, "AddUniform",
            [typeof(string), typeof(ShaderParameterGroup), typeof(int), typeof(int),
             typeof(DiligentShaderCodeVariableClass), typeof(DiligentShaderCodeBasicType),
             typeof(int), typeof(int), typeof(int)],
            "Color", ShaderParameterGroup.Custom, 8, 8,
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            DiligentShaderCodeBasicType.ShaderCodeBasicTypeInt, 1, 1, 0);
        InvokePrivateInstance(reflection, "AddUniform",
            [typeof(string), typeof(ShaderParameterGroup), typeof(int), typeof(int),
             typeof(DiligentShaderCodeVariableClass), typeof(DiligentShaderCodeBasicType),
             typeof(int), typeof(int), typeof(int)],
            "NoBuffer", ShaderParameterGroup.Light, 0, 4,
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 1, 1, 0);

        InvokePrivateInstance(reflection, "AddShaderResource",
            [typeof(StringHash), typeof(string)], new StringHash("Albedo"), "sAlbedo");
        InvokePrivateInstance(reflection, "AddShaderResource",
            [typeof(StringHash), typeof(string)], new StringHash("Albedo"), "sAlbedoAgain");
        InvokePrivateInstance(reflection, "AddUnorderedAccessView",
            [typeof(StringHash), typeof(string)], new StringHash("Output"), "uOutput");
        InvokePrivateInstance(reflection, "AddUnorderedAccessView",
            [typeof(StringHash), typeof(string)], new StringHash("Output"), "uOutputAgain");
        InvokePrivateInstance(reflection, "RecalculateUniformHash", []);

        var materialBuffer = reflection.GetUniformBuffer(ShaderParameterGroup.Material);
        Assert.NotNull(materialBuffer);
        Assert.Equal(32, materialBuffer!.Size);
        Assert.NotEqual(0u, materialBuffer.Hash);
        Assert.True(materialBuffer.InternalName.Equals("Material"));
        Assert.Null(reflection.GetUniformBuffer(ShaderParameterGroup.Zone));

        Assert.Equal(2, reflection.Uniforms.Count);
        Assert.NotNull(reflection.GetUniform(color));
        Assert.Equal("Color", InvokePrivateInstance<string>(reflection, "GetUniformName", [typeof(StringHash)], color));
        Assert.Single(reflection.ShaderResources);
        Assert.Single(reflection.UnorderedAccessViews);
        Assert.True(reflection.GetShaderResource(new StringHash("Albedo"))!.InternalName.Equals("sAlbedo"));
        Assert.True(reflection.GetUnorderedAccessView(new StringHash("Output"))!.InternalName.Equals("uOutput"));
    }

    private static MethodInfo PrivateStaticMethod<T>(string name, params Type[] parameterTypes)
    {
        return typeof(T).GetMethod(
                   name, BindingFlags.NonPublic | BindingFlags.Static, binder: null, parameterTypes, modifiers: null)
               ?? throw new InvalidOperationException($"Private method '{typeof(T).Name}.{name}' was not found.");
    }

    private static T Invoke<T>(MethodInfo method, params object?[] arguments)
    {
        return Assert.IsType<T>(method.Invoke(null, arguments));
    }

    private static T InvokePrivateStatic<TClass, T>(string name, Type[] parameterTypes, params object?[] arguments)
    {
        return Invoke<T>(PrivateStaticMethod<TClass>(name, parameterTypes), arguments);
    }

    private static T InvokePrivateStaticAssignable<TClass, T>(
        string name, Type[] parameterTypes, params object?[] arguments)
    {
        return Assert.IsAssignableFrom<T>(
            PrivateStaticMethod<TClass>(name, parameterTypes).Invoke(null, arguments));
    }

    private static void InvokePrivateInstance(object instance, string name, Type[] parameterTypes, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Instance, binder: null, parameterTypes, modifiers: null)
            ?? throw new InvalidOperationException($"Private method '{name}' was not found.");
        method.Invoke(instance, arguments);
    }

    private static T InvokePrivateInstance<T>(object instance, string name, Type[] parameterTypes, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Instance, binder: null, parameterTypes, modifiers: null)
            ?? throw new InvalidOperationException($"Private method '{name}' was not found.");
        return Assert.IsType<T>(method.Invoke(instance, arguments));
    }

    private static (bool Success, ShaderParameterGroup Group) InvokeConstantBufferParser(string value)
    {
        var method = PrivateStaticMethod<ShaderProgramReflection>(
            "TryParseConstantBufferName", typeof(string), typeof(ShaderParameterGroup).MakeByRefType());
        object?[] arguments = [value, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        var group = Assert.IsType<ShaderParameterGroup>(arguments[1]);
        return (success, group);
    }

    private static (bool Success, string Name) InvokeGlSanitizer(string value)
    {
        var method = PrivateStaticMethod<ShaderProgramReflection>(
            "TrySanitizeGlUniformName", typeof(string), typeof(string).MakeByRefType());
        object?[] arguments = [value, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        var name = Assert.IsType<string>(arguments[1]);
        return (success, name);
    }

    private static (bool Success, StringHash Name) InvokeResourceSanitizer(string value, string methodName)
    {
        var method = PrivateStaticMethod<ShaderProgramReflection>(
            methodName, typeof(string), typeof(StringHash).MakeByRefType());
        object?[] arguments = [value, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        var name = Assert.IsType<StringHash>(arguments[1]);
        return (success, name);
    }

    private static (bool Success, string Name) InvokeUniformPointerSanitizer(string value)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return InvokeUniformPointerSanitizer(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static (bool Success, string Name) InvokeUniformPointerSanitizer(nint pointer)
    {
        var method = PrivateStaticMethod<ShaderProgramReflection>(
            "TrySanitizeUniformName", typeof(nint), typeof(string).MakeByRefType());
        object?[] arguments = [pointer, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        var name = Assert.IsType<string>(arguments[1]);
        return (success, name);
    }

    private static int InvokeSize(string name, params object?[] arguments)
    {
        Type[] parameterTypes = name switch
        {
            "GetScalarUniformSize" => [typeof(DiligentShaderCodeBasicType)],
            "GetVectorUniformSize" => [typeof(DiligentShaderCodeBasicType), typeof(int)],
            "GetVectorArrayUniformSize" => [typeof(DiligentShaderCodeBasicType), typeof(int), typeof(int)],
            "GetMatrixUniformSize" => [typeof(DiligentShaderCodeBasicType), typeof(int), typeof(int)],
            "Align" => [typeof(int), typeof(int)],
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        return InvokePrivateStatic<ShaderProgramReflection, int>(name, parameterTypes, arguments);
    }

    private static DiligentShaderCodeVariableDesc CreateVariable(
        DiligentShaderCodeVariableClass variableClass,
        uint arraySize = 0,
        int numRows = 1,
        int numColumns = 1)
    {
        var variable = new DiligentShaderCodeVariableDesc();
        variable.Class = variableClass;
        variable.BasicType = DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat;
        variable.ArraySize = arraySize;
        variable.NumRows = checked((byte)numRows);
        variable.NumColumns = checked((byte)numColumns);
        return variable;
    }

    private static IEnumerable<RawShader?> ThrowingShaders()
    {
        throw new InvalidOperationException("synthetic shader enumeration failure");
    }
}
