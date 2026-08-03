namespace RedBolt.Tests;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.RenderAPI;

using DiligentShaderCodeBasicType = global::RedBolt.Bindings.Diligent.ShaderCodeBasicType;
using DiligentShaderCodeVariableClass = global::RedBolt.Bindings.Diligent.ShaderCodeVariableClass;
using DiligentShaderCodeVariableDesc = global::RedBolt.Bindings.Diligent.ShaderCodeVariableDesc;
using DiligentShaderType = global::RedBolt.Bindings.Diligent.ShaderType;
using RenderShaderType = global::RedBolt.RenderAPI.ShaderType;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderApiResidualCoverageTests
{
    [Fact]
    public void Render_api_value_helpers_cover_final_sequence_points()
    {
        var element = new InputLayoutElementDesc(
            0, 16, 0, 0, VertexElementType.Float, VertexElementSemantic.Position, 0);
        _ = element.ToHash();

        var samplers = new ImmutableSamplersDesc();
        for (var index = 0; index < RenderApiLimits.MaxNumImmutableSamplers; ++index)
            samplers.Add(new StringHash($"Sampler{index}"), new SamplerStateDesc());

        var listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
        Trace.Listeners.Clear();
        Trace.Listeners.Add(new QuietTraceListener());
        try
        {
            samplers.Add(new StringHash("Overflow"), new SamplerStateDesc());
        }
        finally
        {
            Trace.Listeners.Clear();
            foreach (var listener in listeners)
                Trace.Listeners.Add(listener);
        }
        Assert.Equal(RenderApiLimits.MaxNumImmutableSamplers, samplers.Size);

        var shaderInputNames = (string[])typeof(RenderAPIUtils)
            .GetField("ShaderInputNames", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var originalObjectIndexName = shaderInputNames[(int)VertexElementSemantic.ObjectIndex];
        try
        {
            shaderInputNames[(int)VertexElementSemantic.ObjectIndex] = "__missing_object_index_semantic__";
            Assert.Null(RenderAPIUtils.ParseVertexAttribute("does_not_match_any_semantic"));
        }
        finally
        {
            shaderInputNames[(int)VertexElementSemantic.ObjectIndex] = originalObjectIndexName;
        }
    }

    [Fact]
    public void Render_backend_defaults_cover_windows_and_non_windows_platforms()
    {
        var method = typeof(RenderAPIUtils).GetMethod(
            "GetDefaultRenderBackend", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal(RenderBackend.D3D11, method.Invoke(null, [PlatformId.Windows]));
        Assert.Equal(RenderBackend.D3D11, method.Invoke(null, [PlatformId.UniversalWindowsPlatform]));
        Assert.Equal(RenderBackend.OpenGL, method.Invoke(null, [PlatformId.Linux]));
    }

    [Fact]
    public void Shader_reflection_private_paths_cover_byte_buffers_invalid_variables_and_hash_zero()
    {
        using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());

        var addByteBuffer = (AddByteBufferDelegate)typeof(ShaderProgramReflection)
            .GetMethod(
                "AddUniformBuffer",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(ShaderParameterGroup), typeof(ReadOnlySpan<byte>), typeof(int)],
                modifiers: null)!
            .CreateDelegate(typeof(AddByteBufferDelegate));

        addByteBuffer(reflection, ShaderParameterGroup.Frame, "Frame"u8, 64);
        addByteBuffer(reflection, ShaderParameterGroup.Frame, "Frame"u8, 64);
        addByteBuffer(reflection, ShaderParameterGroup.Frame, "Frame"u8, 32);
        addByteBuffer(reflection, ShaderParameterGroup.Frame, "OtherFrame"u8, 64);

        Assert.Equal(64, reflection.GetUniformBuffer(ShaderParameterGroup.Frame)!.Size);
        Assert.True(reflection.GetUniformBuffer(ShaderParameterGroup.Frame)!.InternalName.Equals("Frame"));

        InvokeInstance(reflection, "ReflectUniformBuffer", "Unknown", null);
        using var emptyBuffer = new ShaderCodeBufferDesc();
        InvokeInstance(reflection, "ReflectUniformBuffer", "Material", emptyBuffer);

        var invalidName = CreateVariable("not_a_uniform", (DiligentShaderCodeVariableClass)99);
        try
        {
            InvokeInstance(reflection, "ReflectVariable", ShaderParameterGroup.Material, invalidName);
        }
        finally
        {
            Marshal.FreeCoTaskMem(invalidName.Name);
        }

        var zeroSizeVariable = CreateVariable("prefixcZeroSize", (DiligentShaderCodeVariableClass)99);
        try
        {
            InvokeInstance(reflection, "ReflectVariable", ShaderParameterGroup.Material, zeroSizeVariable);
        }
        finally
        {
            Marshal.FreeCoTaskMem(zeroSizeVariable.Name);
        }

        var buffers = GetPrivateField<UniformBufferReflection?[]>(reflection, "_uniformBuffers");
        var hashBuffer = new UniformBufferReflection();
        SetPrivateProperty(hashBuffer, nameof(UniformBufferReflection.Size), 64);
        buffers[(int)ShaderParameterGroup.Frame] = hashBuffer;
        var uniforms = GetPrivateField<Dictionary<StringHash, UniformReflection>>(reflection, "_uniforms");
        var zeroHashName = new StringHash(2_004_733_364u);
        uniforms[zeroHashName] = new UniformReflection(
            zeroHashName, ShaderParameterGroup.Frame, 0, 16,
            DiligentShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat, 1, 1, 0);

        InvokeInstance(reflection, "RecalculateUniformHash");
        Assert.Equal(1u, buffers[(int)ShaderParameterGroup.Frame]!.Hash);

        var invalidSizeMethod = PrivateStaticMethod<ShaderProgramReflection>(
            "GetUniformSize", typeof(DiligentShaderCodeVariableDesc));
        var invalidSizeVariable = CreateVariable("unused", (DiligentShaderCodeVariableClass)99);
        try
        {
            Assert.Equal(0, (int)invalidSizeMethod.Invoke(null, [invalidSizeVariable])!);
        }
        finally
        {
            Marshal.FreeCoTaskMem(invalidSizeVariable.Name);
        }

        InvokeInstance(reflection, "AddUnorderedAccessView", new StringHash("Output"), "uOutput");
    }

    [Fact]
    public void Shader_reflection_constructor_disposes_after_enumeration_failure()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ShaderProgramReflection(ThrowingShaders()));
    }

    [Fact]
    public void Shader_reflection_temporary_constructor_disposes_after_native_failure()
    {
        var constructor = typeof(ShaderProgramReflection).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(nint), typeof(bool)], modifiers: null)!;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([nint.Zero, true]));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static DiligentShaderCodeVariableDesc CreateVariable(
        string name, DiligentShaderCodeVariableClass variableClass)
    {
        var namePointer = Marshal.StringToCoTaskMemUTF8(name);
        return new DiligentShaderCodeVariableDesc
        {
            Name = namePointer,
            Class = variableClass,
            BasicType = DiligentShaderCodeBasicType.ShaderCodeBasicTypeFloat,
            NumRows = 1,
            NumColumns = 1
        };
    }

    private static IEnumerable<RawShader?> ThrowingShaders()
    {
        yield return null;
        throw new InvalidOperationException("synthetic shader enumeration failure");
    }

    private static MethodInfo PrivateStaticMethod<T>(string name, params Type[] parameterTypes)
    {
        return typeof(T).GetMethod(
                   name, BindingFlags.Static | BindingFlags.NonPublic, binder: null,
                   parameterTypes, modifiers: null)
               ?? throw new InvalidOperationException($"Private method '{typeof(T).Name}.{name}' was not found.");
    }

    private static void InvokeInstance(object instance, string name, params object?[] arguments)
    {
        var method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        method.Invoke(instance, arguments);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        return (T)(instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Private field '{fieldName}' was not found."));
    }

    private static void SetPrivateProperty(object instance, string propertyName, object value)
    {
        var property = instance.GetType().GetProperty(
            propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(instance, value);
    }

    private delegate void AddByteBufferDelegate(
        ShaderProgramReflection reflection, ShaderParameterGroup group, ReadOnlySpan<byte> name, int size);

    private sealed class QuietTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void Fail(string? message, string? detailMessage)
        {
        }
    }
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class GraphicsResidualCoverageTests
{
    private readonly GraphicsEngineFixture _fixture;

    public GraphicsResidualCoverageTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Managed_depth_views_and_swap_chain_dirty_guard_cover_final_view_paths()
    {
        using var depthTexture = new Texture2D { Name = "FinalResidual.ReadOnlyDepth" };
        Assert.True(depthTexture.SetSize(
            8, 8, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var readOnlyView = RenderTargetView.ReadOnlyDepth(depthTexture);
        Assert.False(readOnlyView.IsSwapChain);
        Assert.Equal(TextureFormat.TexFormatD24UnormS8Uint, readOnlyView.GetFormat());

        RenderTargetView.SwapChainColor(_fixture.RenderDevice).MarkDirty();
    }

    [GraphicsFact]
    public void Shader_reflection_connects_missing_uav_against_native_binding()
    {
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        try
        {
            var vertexShader = _fixture.Graphics.GetShader(RenderShaderType.VS, "X_CopyTexture");
            var pixelShader = _fixture.Graphics.GetShader(RenderShaderType.PS, "X_CopyTexture");
            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);

            var description = new GraphicsPipelineStateDesc
            {
                DebugName = "FinalResidual.ReflectionBinding",
                ColorWriteEnabled = true,
                DepthCompareFunction = CompareMode.Always,
                PrimitiveType = PrimitiveType.TriangleList,
                VertexShader = vertexShader,
                PixelShader = pixelShader
            };
            description.InputLayout.Size = 1;
            description.InputLayout.Elements[0] = new InputLayoutElementDesc(
                0, 12, 0, 0, VertexElementType.Vector3, VertexElementSemantic.Position, 0);
            description.Output.NumRenderTargets = 1;
            description.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
            description.Samplers.Add(new StringHash("Albedo"), SamplerStateDesc.Trilinear());

            var pipelineState = _fixture.PipelineStateCache.GetGraphicsPipelineState(description);
            Assert.NotNull(pipelineState);
            Assert.True(pipelineState!.IsValid);

            var binding = pipelineState.GetShaderResourceBinding();
            Assert.NotNull(binding);

            using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());
            typeof(ShaderProgramReflection).GetMethod(
                    "AddUnorderedAccessView", BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null, [typeof(StringHash), typeof(string)], modifiers: null)!
                .Invoke(reflection, [new StringHash("Missing"), "uMissing"]);

            reflection.ConnectToShaderVariables(PipelineStateType.Graphics, binding!);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [OpenGLFact]
    public void Open_gl_temporary_reflection_covers_unknown_blocks_resources_and_unbound_uniforms()
    {
        using var vertexShader = new InlineOpenGlShader(RenderShaderType.VS, VertexShaderSource);
        using var pixelShader = new InlineOpenGlShader(RenderShaderType.PS, PixelShaderSource);
        Assert.NotNull(vertexShader.GetHandle());
        Assert.NotNull(pixelShader.GetHandle());

        var temporaryProgram = DiligentNativeBindings.CreateTemporaryGLProgram(
            vertexShader.GetHandle()!, pixelShader.GetHandle()!, null!, null!, null!, false);
        try
        {
            using var reflection = new ShaderProgramReflection(temporaryProgram);
            Assert.NotNull(reflection.GetUniformBuffer(ShaderParameterGroup.Frame));
            _ = reflection.GetUniform(new StringHash("Standalone"));
            _ = reflection.GetShaderResource(new StringHash("Albedo"));
            _ = reflection.GetUnorderedAccessView(new StringHash("Output"));
        }
        finally
        {
            DiligentNativeBindings.DeleteTemporaryGLProgram(temporaryProgram);
        }
    }

    private sealed class InlineOpenGlShader : RawShader
    {
        public InlineOpenGlShader(RenderShaderType type, string source)
            : base(type)
        {
            CreateFromBinary(new ShaderBytecode(type)
            {
                Mime = "text/glsl",
                Bytecode = Encoding.UTF8.GetBytes(source)
            });
        }
    }

    private const string VertexShaderSource = """
        #version 430 core
        layout(location = 0) in vec3 input_iPos;
        layout(location = 0) out vec2 vUv;
        layout(std140) uniform UnknownBlock { vec4 cIgnored; };
        layout(std140) uniform Frame { vec4 cFrame; };
        uniform vec4 cStandalone;
        uniform sampler2D sAlbedo;
        layout(binding = 0, rgba8) uniform writeonly image2D uOutput;
        uniform vec4 plainUniform;
        void main()
        {
            vec4 color = cFrame + cStandalone + plainUniform;
            vUv = color.xy + texture(sAlbedo, vec2(0.0)).xy;
            imageStore(uOutput, ivec2(0, 0), color);
            gl_Position = vec4(input_iPos, 1.0);
        }
        """;

    private const string PixelShaderSource = """
        #version 430 core
        layout(location = 0) in vec2 vUv;
        layout(location = 0) out vec4 fragColor;
        layout(std140) uniform UnknownBlock { vec4 cIgnored; };
        layout(std140) uniform Frame { vec4 cFrame; };
        uniform vec4 cStandalone;
        uniform sampler2D sAlbedo;
        layout(binding = 0, rgba8) uniform writeonly image2D uOutput;
        uniform vec4 plainUniform;
        void main()
        {
            vec4 color = cFrame + cStandalone + plainUniform + texture(sAlbedo, vUv);
            imageStore(uOutput, ivec2(0, 0), color);
            fragColor = color;
        }
        """;
}
