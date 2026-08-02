namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Math;
using RedBolt.RenderAPI;
using ResourceDimension = global::RedBolt.Bindings.Diligent.ResourceDimension;

public sealed class RawTextureValidationReflectionCoverageTests
{
    [Fact]
    public void Raw_texture_dimension_validation_covers_all_texture_kinds_and_rejections()
    {
        var twoD = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 4, 9), ArraySize: 7);
        Assert.True(InvokeValidation("ValidateDimensions", ref twoD));
        Assert.Equal(new Vector3i(8, 4, 1), twoD.Size);
        Assert.Equal(1, twoD.ArraySize);

        var cube = new RawTextureParams(
            TextureType.TextureCube, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 4, 9), ArraySize: 2);
        Assert.True(InvokeValidation("ValidateDimensions", ref cube));
        Assert.Equal(new Vector3i(8, 8, 1), cube.Size);
        Assert.Equal(6, cube.ArraySize);

        var volume = new RawTextureParams(
            TextureType.Texture3D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 4, 3), ArraySize: 4);
        Assert.True(InvokeValidation("ValidateDimensions", ref volume));
        Assert.Equal(1, volume.ArraySize);

        var array = new RawTextureParams(
            TextureType.Texture2DArray, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 4, 9), ArraySize: 3);
        Assert.True(InvokeValidation("ValidateDimensions", ref array));
        Assert.Equal(new Vector3i(8, 4, 1), array.Size);
        Assert.Equal(3, array.ArraySize);

        foreach (var invalid in new[]
        {
            new RawTextureParams(TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(0, 4, 1)),
            new RawTextureParams(TextureType.TextureCube, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(-1, 4, 1)),
            new RawTextureParams(TextureType.Texture3D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(4, 4, 0)),
            new RawTextureParams(TextureType.Texture2DArray, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None, new Vector3i(4, 4, 1), ArraySize: 0)
        })
        {
            var parameters = invalid;
            Assert.False(InvokeValidation("ValidateDimensions", ref parameters));
        }

        var unsupported = new RawTextureParams(
            (TextureType)99, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, Vector3i.One);
        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeValidation("ValidateDimensions", ref unsupported));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Raw_texture_binding_multisample_and_level_validation_covers_normalization()
    {
        var validBindings = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget, Vector3i.One);
        Assert.True(InvokeValidation("ValidateBindings", ref validBindings));

        var invalidBindings = validBindings with { Flags = TextureFlag.BindRenderTarget | TextureFlag.BindDepthStencil };
        Assert.False(InvokeValidation("ValidateBindings", ref invalidBindings));

        var simple = validBindings with { MultiSample = 0, Flags = TextureFlag.None };
        Assert.True(InvokeValidation("ValidateMultiSample", ref simple));
        Assert.Equal(1, simple.MultiSample);
        Assert.True((simple.Flags & TextureFlag.NoMultiSampledAutoResolve) != 0);

        var clamped = validBindings with { MultiSample = 99 };
        Assert.True(InvokeValidation("ValidateMultiSample", ref clamped));
        Assert.Equal(16, clamped.MultiSample);

        var unsupported = validBindings with { MultiSample = 4, Flags = TextureFlag.None };
        Assert.False(InvokeValidation("ValidateMultiSample", ref unsupported));

        var inferred = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None,
            new Vector3i(8, 4, 1), NumLevels: 99);
        Assert.True(InvokeValidation("ValidateLevels", ref inferred));
        Assert.Equal(4, inferred.NumLevels);
        Assert.Equal(4, inferred.NumLevelsRtv);

        var depth = inferred with
        {
            Flags = TextureFlag.BindDepthStencil,
            NumLevels = 4,
            MultiSample = 1
        };
        Assert.True(InvokeValidation("ValidateLevels", ref depth));
        Assert.Equal(1, depth.NumLevels);

        var multisample = inferred with
        {
            Flags = TextureFlag.BindRenderTarget | TextureFlag.NoMultiSampledAutoResolve,
            NumLevels = 4,
            MultiSample = 4
        };
        Assert.True(InvokeValidation("ValidateLevels", ref multisample));
        Assert.Equal(1, multisample.NumLevels);
        Assert.Equal(1, multisample.NumLevelsRtv);
    }

    [Fact]
    public void Raw_texture_private_dimension_and_mip_helpers_cover_switches_and_boundaries()
    {
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex2d,
            InvokePrivate<ResourceDimension>("GetResourceDimension", TextureType.Texture2D));
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTexCube,
            InvokePrivate<ResourceDimension>("GetResourceDimension", TextureType.TextureCube));
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex3d,
            InvokePrivate<ResourceDimension>("GetResourceDimension", TextureType.Texture3D));
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex2dArray,
            InvokePrivate<ResourceDimension>("GetResourceDimension", TextureType.Texture2DArray));

        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex2d,
            InvokePrivate<ResourceDimension>("GetViewResourceDimension", TextureType.Texture2D));
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex2dArray,
            InvokePrivate<ResourceDimension>("GetViewResourceDimension", TextureType.TextureCube));
        Assert.Equal(global::RedBolt.Bindings.Diligent.ResourceDimension.ResourceDimTex3d,
            InvokePrivate<ResourceDimension>("GetViewResourceDimension", TextureType.Texture3D));

        foreach (var name in new[] { "GetResourceDimension", "GetViewResourceDimension" })
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => InvokePrivate<ResourceDimension>(name, (TextureType)99));
            Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
        }

        Assert.Equal(1, InvokePrivate<int>("CalculateMipLevelCount", new Vector3i(1, 1, 1)));
        Assert.Equal(4, InvokePrivate<int>("CalculateMipLevelCount", new Vector3i(8, 3, 2)));
        Assert.Equal(new Vector3i(1, 1, 1),
            InvokePrivate<Vector3i>("GetMipLevelSize", new Vector3i(8, 4, 2), 99));
        Assert.Equal(new Vector3i(4, 2, 1),
            InvokePrivate<Vector3i>("GetMipLevelSize", new Vector3i(8, 4, 2), 1));
    }

    private static bool InvokeValidation(string name, ref RawTextureParams parameters)
    {
        var arguments = new object?[] { parameters };
        var result = (bool)typeof(RawTexture).GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;
        parameters = (RawTextureParams)arguments[0]!;
        return result;
    }

    private static T InvokePrivate<T>(string name, params object?[] arguments)
    {
        return (T)typeof(RawTexture).GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;
    }
}
