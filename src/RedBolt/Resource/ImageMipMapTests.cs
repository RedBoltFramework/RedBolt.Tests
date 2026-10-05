// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ImageMipMapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ImageMipMapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class MipMapParamsTexture : Texture
    {
        public static ImageMipMapParams Parse(XMLFile? file) => CreateMipMapParamsFromXML(file);
    }

    private static Image CreateImage(int width, int height, int depth, int components, byte[] data)
    {
        var image = new Image();
        Assert.True(image.SetSize(width, height, depth, components));
        image.SetData(data);
        return image;
    }

    [Fact]
    public void Default_mipmap_params_use_average_algorithm_and_unit_alpha_scale()
    {
        var defaults = new ImageMipMapParams();
        Assert.Equal(ImageMipMapAlgorithm.Average, defaults.Algorithm);
        Assert.Equal(1.0f, defaults.AlphaScaleFactor);

        using var image = new Image();
        Assert.Equal(ImageMipMapAlgorithm.Average, image.GetMipMapParams().Algorithm);
        Assert.Equal(1.0f, image.GetMipMapParams().AlphaScaleFactor);
    }

    [Fact]
    public void Average_algorithm_averages_all_components_including_alpha()
    {
        using var image = CreateImage(2, 2, 1, 4,
        [
            255, 0, 0, 255,
            0, 255, 0, 0,
            0, 0, 255, 255,
            255, 255, 255, 0,
        ]);

        using var mip = image.GetNextLevel()!;
        Assert.Equal(new byte[] { 127, 127, 127, 127 }, mip.GetData());
    }

    [Fact]
    public void Alpha_weighted_algorithm_weights_color_by_alpha()
    {
        using var image = CreateImage(2, 1, 1, 4, [255, 0, 0, 255, 0, 255, 0, 0]);
        image.SetMipMapParams(new ImageMipMapParams { Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear });

        using var mip = image.GetNextLevel()!;
        Assert.Equal(new byte[] { 255, 0, 0, 127 }, mip.GetData());
    }

    [Fact]
    public void Alpha_weighted_algorithm_scales_and_clamps_alpha()
    {
        var data = new byte[] { 255, 0, 0, 255, 0, 255, 0, 0 };
        using var image = CreateImage(2, 1, 1, 4, data);

        image.SetMipMapParams(new ImageMipMapParams
        {
            Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear,
            AlphaScaleFactor = 1.5f,
        });
        using (var scaled = image.GetNextLevel()!)
            Assert.Equal(new byte[] { 255, 0, 0, 191 }, scaled.GetData());

        image.SetMipMapParams(new ImageMipMapParams
        {
            Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear,
            AlphaScaleFactor = 8.0f,
        });
        using (var clamped = image.GetNextLevel()!)
            Assert.Equal(255, clamped.GetData()[3]);
    }

    [Fact]
    public void Alpha_weighted_algorithm_handles_fully_transparent_input()
    {
        using var image = CreateImage(2, 2, 1, 4,
        [
            255, 0, 0, 0,
            0, 255, 0, 0,
            0, 0, 255, 0,
            255, 255, 255, 0,
        ]);
        image.SetMipMapParams(new ImageMipMapParams { Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear });

        using var mip = image.GetNextLevel()!;
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, mip.GetData());
    }

    [Fact]
    public void Alpha_weighted_algorithm_averages_images_without_four_components()
    {
        using var image = CreateImage(2, 1, 1, 3, [10, 20, 30, 30, 40, 50]);
        image.SetMipMapParams(new ImageMipMapParams { Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear });

        using var mip = image.GetNextLevel()!;
        Assert.Equal(new byte[] { 20, 30, 40 }, mip.GetData());
    }

    [Fact]
    public void Alpha_weighted_algorithm_applies_to_volume_images()
    {
        var data = new byte[4 * 2 * 2 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            // Opaque red voxels in the first column pair, transparent green voxels in the second.
            var isRed = i / 4 % 4 < 2;
            data[i] = isRed ? (byte)255 : (byte)0;
            data[i + 1] = isRed ? (byte)0 : (byte)255;
            data[i + 3] = isRed ? (byte)255 : (byte)0;
        }

        using var image = CreateImage(4, 2, 2, 4, data);
        image.SetMipMapParams(new ImageMipMapParams { Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear });

        using var mip = image.GetNextLevel()!;
        Assert.Equal(new Vector3i(2, 1, 1), mip.GetSize());
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 0, 0 }, mip.GetData());
    }

    [Fact]
    public void Average_algorithm_fills_every_output_pixel_of_volume_images()
    {
        var data = new byte[4 * 2 * 2];
        for (var i = 0; i < data.Length; ++i)
            data[i] = (byte)(i < 8 ? 40 : 200);

        using var image = CreateImage(4, 2, 2, 1, data);
        using var mip = image.GetNextLevel()!;

        Assert.Equal(new Vector3i(2, 1, 1), mip.GetSize());
        Assert.Equal(new byte[] { 120, 120 }, mip.GetData());
    }

    [Fact]
    public void Mipmap_params_propagate_to_derived_images()
    {
        var parameters = new ImageMipMapParams
        {
            Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear,
            AlphaScaleFactor = 2.0f,
        };
        using var image = CreateImage(2, 2, 1, 4, new byte[16]);
        image.SetMipMapParams(parameters);

        using var next = image.GetNextLevel()!;
        using var rgba = image.ConvertToRGBA()!;
        using var subimage = image.GetSubimage(new IntRect(0, 0, 1, 1))!;

        foreach (var derived in new[] { next, rgba, subimage })
        {
            Assert.Equal(ImageMipMapAlgorithm.AlphaWeightedLinear, derived.GetMipMapParams().Algorithm);
            Assert.Equal(2.0f, derived.GetMipMapParams().AlphaScaleFactor);
        }
    }

    [Fact]
    public void Precalculated_levels_use_the_mipmap_params()
    {
        using var image = CreateImage(4, 1, 1, 4,
        [
            255, 0, 0, 255, 0, 255, 0, 0, 255, 0, 0, 255, 0, 255, 0, 0,
        ]);
        image.SetMipMapParams(new ImageMipMapParams { Algorithm = ImageMipMapAlgorithm.AlphaWeightedLinear });
        image.PrecalculateLevels();

        var first = image.GetNextLevel()!;
        Assert.Equal(new byte[] { 255, 0, 0, 127, 255, 0, 0, 127 }, first.GetData());
        var second = first.GetNextLevel()!;
        Assert.Equal(new byte[] { 255, 0, 0, 127 }, second.GetData());
    }

    [Fact]
    public void Texture_reads_mipmap_params_from_xml()
    {
        using var file = new XMLFile();
        Assert.True(file.FromString(
            "<texture><mipmapgen algorithm=\"AlphaWeighted\" alphascale=\"1.5\"/></texture>"));

        var parsed = MipMapParamsTexture.Parse(file);
        Assert.Equal(ImageMipMapAlgorithm.AlphaWeightedLinear, parsed.Algorithm);
        Assert.Equal(1.5f, parsed.AlphaScaleFactor);

        using var average = new XMLFile();
        Assert.True(average.FromString("<texture><mipmapgen algorithm=\"average\" alphascale=\"0.5\"/></texture>"));
        parsed = MipMapParamsTexture.Parse(average);
        Assert.Equal(ImageMipMapAlgorithm.Average, parsed.Algorithm);
        Assert.Equal(0.5f, parsed.AlphaScaleFactor);

        using var unknown = new XMLFile();
        Assert.True(unknown.FromString("<texture><mipmapgen algorithm=\"bogus\" alphascale=\"2\"/></texture>"));
        Assert.Equal(ImageMipMapAlgorithm.Average, MipMapParamsTexture.Parse(unknown).Algorithm);
    }

    [Fact]
    public void Texture_uses_default_mipmap_params_without_xml_element()
    {
        var fromNull = MipMapParamsTexture.Parse(null);
        Assert.Equal(ImageMipMapAlgorithm.Average, fromNull.Algorithm);
        Assert.Equal(1.0f, fromNull.AlphaScaleFactor);

        using var file = new XMLFile();
        Assert.True(file.FromString("<texture><address coord=\"u\" mode=\"wrap\"/></texture>"));
        var parsed = MipMapParamsTexture.Parse(file);
        Assert.Equal(ImageMipMapAlgorithm.Average, parsed.Algorithm);
        Assert.Equal(1.0f, parsed.AlphaScaleFactor);
    }
}
