// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

using RedBolt.Core;
using RedBolt.Editor.Assets;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DilateTextureTransformerTests
{
    private static readonly Color Red = new(1.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color Blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    private static readonly Color Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Color TransparentRed = new(1.0f, 0.0f, 0.0f, 0.0f);
    private static readonly Color TransparentBlue = new(0.0f, 0.0f, 1.0f, 0.0f);
    private static readonly Color TransparentGreen = new(0.0f, 1.0f, 0.0f, 0.0f);

    private readonly HeadlessEngineFixture _fixture;

    public DilateTextureTransformerTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Transparent_pixels_take_the_colour_of_the_nearest_opaque_pixel_and_keep_their_alpha()
    {
        using var image = CreateImage(3, 1, Red, TransparentBlue, TransparentGreen);

        Dilate(image, 0.0f);

        AssertPixels(image, Red, TransparentRed, TransparentRed);
    }

    [Fact]
    public void Dilation_spreads_in_four_directions_from_every_opaque_pixel()
    {
        using var image = CreateImage(
            3, 3,
            TransparentBlue, TransparentBlue, TransparentBlue,
            TransparentBlue, Red, TransparentBlue,
            TransparentBlue, TransparentBlue, TransparentBlue);

        Dilate(image, 0.0f);

        AssertPixels(
            image,
            TransparentRed, TransparentRed, TransparentRed,
            TransparentRed, Red, TransparentRed,
            TransparentRed, TransparentRed, TransparentRed);
    }

    [Fact]
    public void Transparent_pixels_between_two_opaque_regions_follow_the_breadth_first_order()
    {
        using var image = CreateImage(5, 1, Red, TransparentGreen, TransparentGreen, TransparentGreen, Blue);

        Dilate(image, 0.0f);

        // Pixels 1 and 3 are one step away from a region. Pixel 2 is reached from pixel 1 first.
        AssertPixels(image, Red, TransparentRed, TransparentRed, TransparentBlue, Blue);
    }

    [Fact]
    public void Fully_transparent_image_is_not_changed()
    {
        using var image = CreateImage(2, 2, TransparentBlue, TransparentRed, TransparentGreen, TransparentBlue);

        Dilate(image, 0.0f);

        AssertPixels(image, TransparentBlue, TransparentRed, TransparentGreen, TransparentBlue);
    }

    [Fact]
    public void Fully_opaque_image_is_not_changed()
    {
        using var image = CreateImage(2, 2, Red, Green, Blue, Red);

        Dilate(image, 0.0f);

        AssertPixels(image, Red, Green, Blue, Red);
    }

    [Fact]
    public void Pixels_above_the_threshold_are_sources_and_keep_their_own_colour_and_alpha()
    {
        var halfRed = new Color(1.0f, 0.0f, 0.0f, 128.0f / 255.0f);
        using var image = CreateImage(3, 1, halfRed, Green, TransparentBlue);

        Dilate(image, 0.25f);

        // Pixel 1 has alpha 1.0 and stays the source for pixel 2. Pixel 0 stays unchanged.
        AssertPixels(image, halfRed, Green, new Color(0.0f, 1.0f, 0.0f, 0.0f));
    }

    [Fact]
    public void Pixels_with_alpha_not_above_the_threshold_are_not_sources()
    {
        var halfRed = new Color(1.0f, 0.0f, 0.0f, 128.0f / 255.0f);
        using var image = CreateImage(2, 1, halfRed, TransparentBlue);
        var threshold = image.GetPixel(0, 0).A;

        Dilate(image, threshold);
        AssertPixels(image, halfRed, TransparentBlue);

        Dilate(image, threshold - 0.01f);
        AssertPixels(image, halfRed, new Color(1.0f, 0.0f, 0.0f, 0.0f));
    }

    [NativeBindingFact]
    public void Execute_dilates_the_input_png_in_place_and_marks_the_source_as_modified()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBolt.DilateTextureTransformerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var fileName = Path.Combine(directory, "Texture.png");
            using (var source = CreateImage(2, 1, Red, TransparentBlue))
                Assert.True(source.SavePNG(fileName));

            using var transformer = new DilateTextureTransformer();
            var input = new AssetTransformerInput { ResourceName = "Texture.png", InputFileName = fileName };
            var output = new AssetTransformerOutput();

            Assert.True(transformer.Execute(input, output, [transformer]));
            Assert.True(output.SourceModified);

            using var result = new Image();
            Assert.True(result.LoadFile(new FileIdentifier(fileName)));
            AssertPixels(result, Red, TransparentRed);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [NativeBindingFact]
    public void Execute_fails_without_modifying_the_source_when_the_image_cannot_be_loaded()
    {
        using var transformer = new DilateTextureTransformer();
        var input = new AssetTransformerInput
        {
            ResourceName = "Missing.png",
            InputFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.png"),
        };
        var output = new AssetTransformerOutput();

        Assert.False(transformer.Execute(input, output, [transformer]));
        Assert.False(output.SourceModified);
    }

    [Fact]
    public void Transformer_is_applicable_to_resources_that_fully_match_a_case_insensitive_pattern()
    {
        _fixture.Context.RegisterAssembly(typeof(DilateTextureTransformer).Assembly);
        using var transformer = CreateTransformer();

        Assert.True(IsApplicable(transformer, "Textures/Albedo.png"));
        Assert.True(IsApplicable(transformer, "Textures/Albedo.PNG"));
        Assert.False(IsApplicable(transformer, "Textures/Albedo.jpg"));
        Assert.False(IsApplicable(transformer, "Textures/Albedo.png.bak"));

        SetPatterns(transformer, "Textures/Sprites/.+", "(");

        Assert.True(IsApplicable(transformer, "Textures/Sprites/Hero.png"));
        Assert.False(IsApplicable(transformer, "Textures/Albedo.png"));
    }

    [Fact]
    public void Transformer_reflects_native_attributes_defaults_and_serialization()
    {
        _fixture.Context.RegisterAssembly(typeof(DilateTextureTransformer).Assembly);
        using var transformer = CreateTransformer();

        Assert.Contains(
            ObjectReflection<DilateTextureTransformer>.TypeId,
            _fixture.Context.GetObjectCategories()[ObjectCategory.Category_Transformer]);
        var attributes = transformer.GetAttributes();
        Assert.Equal(["File Name Regex", "Alpha Threshold"], attributes.Select(static attribute => attribute.Name));
        Assert.Equal([@".+\.png"], attributes[0].DefaultValue.GetStringVector());
        Assert.Equal(0.0f, attributes[1].DefaultValue.GetFloat());
        Assert.Equal([@".+\.png"], Assert.IsType<StringVector>(attributes[0].Accessor!.GetObject(transformer)));
        Assert.Equal(0.0f, attributes[1].Accessor!.GetObject(transformer));

        SetPatterns(transformer, @".+\.tga");
        attributes[1].Accessor!.SetObject(transformer, 0.5f);

        using var json = new JSONValue(JSONValueType.Object);
        Assert.True(transformer.SaveJSON(json));
        using var restored = CreateTransformer();
        Assert.True(restored.LoadJSON(json));
        var restoredAttributes = restored.GetAttributes();
        Assert.Equal([@".+\.tga"], Assert.IsType<StringVector>(restoredAttributes[0].Accessor!.GetObject(restored)));
        Assert.Equal(0.5f, restoredAttributes[1].Accessor!.GetObject(restored));
        Assert.True(IsApplicable(restored, "Textures/Albedo.tga"));
        Assert.False(IsApplicable(restored, "Textures/Albedo.png"));
    }

    private DilateTextureTransformer CreateTransformer()
    {
        return Assert.IsType<DilateTextureTransformer>(
            _fixture.Context.CreateObject(ObjectReflection<DilateTextureTransformer>.TypeId));
    }

    private static void SetPatterns(DilateTextureTransformer transformer, params string[] patterns)
    {
        transformer.GetAttributes()[0].Accessor!.SetObject(transformer, new StringVector(patterns));
    }

    private static bool IsApplicable(DilateTextureTransformer transformer, string resourceName)
    {
        return transformer.IsApplicable(new AssetTransformerInput { ResourceName = resourceName });
    }

    private static void Dilate(Image image, float alphaThreshold)
    {
        var dilateImage = typeof(DilateTextureTransformer).GetMethod(
            "DilateImage", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(dilateImage);
        dilateImage.Invoke(null, [image, alphaThreshold]);
    }

    private static Image CreateImage(int width, int height, params Color[] pixels)
    {
        Assert.Equal(width * height, pixels.Length);
        var image = new Image();
        Assert.True(image.SetSize(width, height, 4));
        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
                image.SetPixel(x, y, pixels[y * width + x]);
        }
        return image;
    }

    private static void AssertPixels(Image image, params Color[] expected)
    {
        Assert.Equal(expected.Length, image.Width * image.Height);
        for (var index = 0; index < expected.Length; ++index)
        {
            var x = index % image.Width;
            var y = index / image.Width;
            Assert.True(
                expected[index].ToUInt() == image.GetPixelInt(x, y),
                $"Pixel ({x}, {y}): expected 0x{expected[index].ToUInt():x8}, actual 0x{image.GetPixelInt(x, y):x8}.");
        }
    }
}
