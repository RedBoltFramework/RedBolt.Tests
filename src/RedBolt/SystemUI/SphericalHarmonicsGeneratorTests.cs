// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Xml.Linq;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SphericalHarmonicsGeneratorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SphericalHarmonicsGeneratorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Uniform_cubemap_generates_expected_average_and_rewrites_xml_file()
    {
        _fixture.ResetGraphics();
        using var texture = CreateUniformCube(64, 128, 192);
        using var generator = new SphericalHarmonicsGenerator();

        var harmonics = generator.Generate(texture);
        var average = harmonics.EvaluateAverage();
        AssertClose(64.0f / 255.0f, average.X);
        AssertClose(128.0f / 255.0f, average.Y);
        AssertClose(192.0f / 255.0f, average.Z);
        for (var i = 0; i < 9; ++i)
        {
            Assert.True(float.IsFinite(harmonics.Values[i].X));
            Assert.True(float.IsFinite(harmonics.Values[i].Y));
            Assert.True(float.IsFinite(harmonics.Values[i].Z));
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "RedBolt.SphericalHarmonicsGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var fileName = Path.Combine(tempDirectory, "Cube.xml");
        try
        {
            using var imageXml = new XMLFile();
            imageXml.SetAbsoluteFileName(fileName);
            using (var root = imageXml.CreateRoot("cubemap"))
            {
                using var firstOldHarmonics = root.CreateChild("sh");
                firstOldHarmonics.SetString("old", "first");
                using var secondOldHarmonics = root.CreateChild("sh");
                secondOldHarmonics.SetString("old", "second");
            }

            generator.Generate(texture, imageXml);

            var document = XDocument.Load(fileName);
            var savedHarmonics = Assert.Single(document.Root!.Elements("sh"));
            Assert.Null(savedHarmonics.Attribute("old"));
            foreach (var attributeName in new[] { "ar", "ag", "ab", "br", "bg", "bb", "c" })
                Assert.False(string.IsNullOrWhiteSpace(savedHarmonics.Attribute(attributeName)?.Value));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static TextureCube CreateUniformCube(byte red, byte green, byte blue)
    {
        var texture = new TextureCube { Name = "SphericalHarmonicsGeneratorTests.UniformCube" };
        texture.SetLinear(true);
        using var image = new Image();
        Assert.True(image.SetSize(4, 4, 4));
        var data = new byte[4 * 4 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = red;
            data[i + 1] = green;
            data[i + 2] = blue;
            data[i + 3] = 255;
        }
        image.SetData(data);

        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces;
             face = (CubeMapFace)((int)face + 1))
        {
            Assert.True(texture.SetData(face, image));
        }

        return texture;
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(MathF.Abs(expected - actual), 0.0f, 0.01f);
}
