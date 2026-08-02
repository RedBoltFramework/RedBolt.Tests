namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.RenderAPI;
using DiligentBasicType = global::RedBolt.Bindings.Diligent.ShaderCodeBasicType;
using DiligentShaderCodeBasicType = global::RedBolt.Bindings.Diligent.ShaderCodeBasicType;
using DiligentShaderCodeVariableClass = global::RedBolt.Bindings.Diligent.ShaderCodeVariableClass;
using DiligentShaderType = global::RedBolt.Bindings.Diligent.ShaderType;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderReflectionPrivateCoverageTests
{
    [Fact]
    public void Shader_reflection_private_parsers_and_size_helpers_cover_all_managed_branches()
    {
        using var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());

        foreach (var name in new[] { "Frame", "Camera", "Zone", "Light", "Material", "Object", "Custom" })
        {
            var arguments = new object?[] { name, null };
            Assert.True((bool)InvokeStatic("TryParseConstantBufferName", arguments)!);
            Assert.NotEqual(-1, (int)(ShaderParameterGroup)arguments[1]!);
        }

        var unknownGroup = new object?[] { "Unknown", null };
        Assert.False((bool)InvokeStatic("TryParseConstantBufferName", unknownGroup)!);

        var sanitizedArray = new object?[] { "Material.cDiffuse[0]", null };
        Assert.True((bool)InvokeStatic("TrySanitizeGlUniformName", sanitizedArray)!);
        Assert.Equal("Diffuse", sanitizedArray[1]);
        var sanitizedPlain = new object?[] { "cDiffuse", null };
        Assert.True((bool)InvokeStatic("TrySanitizeGlUniformName", sanitizedPlain)!);
        Assert.Equal("Diffuse", sanitizedPlain[1]);
        foreach (var invalid in new[] { "", "Diffuse", "cDiffuse[1]", "Material." })
        {
            var arguments = new object?[] { invalid, null };
            Assert.False((bool)InvokeStatic("TrySanitizeGlUniformName", arguments)!);
        }

        using var validUniformName = new CString("cDiffuse");
        var validUniformArguments = new object?[] { validUniformName.Pointer, null };
        Assert.True((bool)InvokeStatic("TrySanitizeUniformName", validUniformArguments)!);
        Assert.Equal("Diffuse", validUniformArguments[1]);
        using var invalidUniformName = new CString("uniform");
        Assert.False((bool)InvokeStatic("TrySanitizeUniformName",
            new object?[] { invalidUniformName.Pointer, null })!);
        using var emptyUniformName = new CString("c");
        Assert.False((bool)InvokeStatic("TrySanitizeUniformName",
            new object?[] { emptyUniformName.Pointer, null })!);

        foreach (var valid in new[] { "sAlbedo", "s0" })
        {
            var arguments = new object?[] { valid, null };
            Assert.True((bool)InvokeStatic("TrySanitizeSrvName", arguments)!);
            Assert.NotEqual(default(StringHash), arguments[1]);
        }
        foreach (var invalid in new[] { "", "s", "uAlbedo" })
            Assert.False((bool)InvokeStatic("TrySanitizeSrvName", new object?[] { invalid, null })!);
        foreach (var valid in new[] { "uImage", "u0" })
            Assert.True((bool)InvokeStatic("TrySanitizeUavName", new object?[] { valid, null })!);
        foreach (var invalid in new[] { "", "u", "sImage" })
            Assert.False((bool)InvokeStatic("TrySanitizeUavName", new object?[] { invalid, null })!);

        foreach (var pipelineType in new[] { PipelineStateType.Graphics, PipelineStateType.Compute })
            Assert.NotEmpty((IReadOnlyList<ShaderType>)InvokeStatic("GetShaderTypes", new object?[] { pipelineType })!);
        Assert.Throws<TargetInvocationException>(() => InvokeStatic(
            "GetShaderTypes", new object?[] { (PipelineStateType)99 }));

        foreach (var shaderType in Enum.GetValues<ShaderType>().Where(type => type != ShaderType.MaxShaderTypes))
        {
            var native = (DiligentShaderType)InvokeStatic("ToNativeShaderType", new object?[] { shaderType })!;
            Assert.NotEqual(DiligentShaderType.ShaderTypeUnknown, native);
        }
        Assert.Throws<TargetInvocationException>(() => InvokeStatic(
            "ToNativeShaderType", new object?[] { (ShaderType)99 }));

        foreach (var basicType in Enum.GetValues<DiligentBasicType>())
        {
            _ = InvokeStatic("GetScalarUniformSize", new object?[] { basicType });
            _ = InvokeStatic("GetVectorUniformSize", new object?[] { basicType, 1 });
            _ = InvokeStatic("GetVectorUniformSize", new object?[] { basicType, 2 });
            _ = InvokeStatic("GetVectorUniformSize", new object?[] { basicType, 3 });
            _ = InvokeStatic("GetVectorUniformSize", new object?[] { basicType, 4 });
        }
        Assert.Equal(0, InvokeStatic("GetVectorUniformSize", new object?[] { DiligentBasicType.ShaderCodeBasicTypeFloat, 5 }));
        Assert.Equal(32, InvokeStatic("GetVectorArrayUniformSize", new object?[] {
            DiligentBasicType.ShaderCodeBasicTypeFloat, 3, 2 }));
        Assert.Equal(24, InvokeStatic("GetMatrixUniformSize", new object?[] {
            DiligentBasicType.ShaderCodeBasicTypeFloat, 2, 3 }));
        Assert.Equal(16, InvokeStatic("Align", new object?[] { 13, 16 }));

        InvokeInstance(reflection, "AddUniformBuffer",
            new object?[] { ShaderParameterGroup.Frame, "Frame", 64 });
        InvokeInstance(reflection, "AddUniformBuffer",
            new object?[] { ShaderParameterGroup.Frame, "OtherFrame", 32 });
        InvokeInstance(reflection, "AddUniform",
            new object?[] { "Color", ShaderParameterGroup.Frame, 0, 16,
                default(DiligentShaderCodeVariableClass), default(DiligentShaderCodeBasicType), 1, 4, 1 });
        InvokeInstance(reflection, "AddUniform",
            new object?[] { "Color", ShaderParameterGroup.Camera, 4, 32,
                default(DiligentShaderCodeVariableClass), default(DiligentShaderCodeBasicType), 2, 2, 2 });
        InvokeInstance(reflection, "AddShaderResource",
            new object?[] { new StringHash("Albedo"), "sAlbedo" });
        InvokeInstance(reflection, "AddShaderResource",
            new object?[] { new StringHash("Albedo"), "sAlbedoAgain" });
        InvokeInstance(reflection, "AddUnorderedAccessView",
            new object?[] { new StringHash("Output"), "uOutput" });
        InvokeInstance(reflection, "AddUnorderedAccessView",
            new object?[] { new StringHash("Output"), "uOutputAgain" });

        Assert.Equal(64, reflection.GetUniformBuffer(ShaderParameterGroup.Frame)!.Size);
        Assert.Null(reflection.GetUniformBuffer((ShaderParameterGroup)(-1)));
        Assert.NotNull(reflection.GetUniform(new StringHash("Color")));
        Assert.NotNull(reflection.GetShaderResource(new StringHash("Albedo")));
        Assert.NotNull(reflection.GetUnorderedAccessView(new StringHash("Output")));
    }

    private static object? InvokeStatic(string name, object?[] arguments, int outputIndex = -1)
    {
        var method = typeof(ShaderProgramReflection).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name &&
                candidate.GetParameters().Length == arguments.Length &&
                (arguments.Length == 0 || arguments[0] is null ||
                    candidate.GetParameters()[0].ParameterType.IsInstanceOfType(arguments[0])));
        var result = method.Invoke(null, arguments);
        return outputIndex >= 0 ? arguments[outputIndex] : result;
    }

    private static void InvokeInstance(ShaderProgramReflection instance, string name, object?[] arguments)
    {
        typeof(ShaderProgramReflection).GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: arguments.Select(argument => argument?.GetType() ?? typeof(object)).ToArray(),
            modifiers: null)!.Invoke(instance, arguments);
    }
}
