namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Core;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class LightBakerVisualTests
{
    [VisualFact]
    public void Generated_direct_lightmap_renders_the_baked_scene()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(), "RedBoltLightBakerVisual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        using var fixture = new VisualRegressionFixture(320, 180);
        GlowTestData.AddResourceDirs(fixture.Resources);
        var context = fixture.EngineFixture.Context;
        context.RegisterAssembly(typeof(IncrementalLightBaker).Assembly);
        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var rootMount = virtualFileSystem.MountRoot();
        try
        {
            var sceneResource = fixture.Resources.Get<SceneResource>("Scenes/BakingTests/Direct.xml");
            var scene = sceneResource.Scene;
            var baker = Assert.IsType<LightBaker>(scene.GetComponent<LightBaker>());

            Assert.True(baker.SetAttribute("Output Directory", FileIdentifier.FromUri(outputDirectory).ToUri()));
            Assert.True(baker.SetAttribute("Lightmap Size", 32u));
            Assert.True(baker.SetAttribute("Direct Samples (Lightmap)", 2u));
            Assert.True(baker.SetAttribute("Direct Samples (Light Probes)", 2u));
            Assert.True(baker.SetAttribute("Indirect Bounces", 0u));
            Assert.True(baker.SetAttribute("Indirect Samples (Texture)", 1u));
            Assert.True(baker.SetAttribute("Indirect Samples (Light Probes)", 1u));
            Assert.True(baker.SetAttribute("Stitch Iterations", 0u));

            var bakeAction = baker.Attributes.Single(attribute => attribute.Name == "Bake");
            Assert.True(bakeAction.GetMetadata<bool>(AttributeMetadata.IsAction));
            Assert.Equal("Re-bake lightmaps and light probes!", bakeAction.Accessor!.GetObject(baker));

            baker.Bake();

            var lightmapFileName = Path.Combine(outputDirectory, "Textures", "Lightmap-0.png");
            Assert.True(System.IO.File.Exists(lightmapFileName));
            var generatedLightmap = ScaleNearest(VisualRegressionImage.Load(lightmapFileName), 8);
            Assert.True(fixture.Resources.ReloadResource(scene.GetLightmapTexture(0)));
            var cameraNode = scene.CreateChild("GeneratedLightmapCamera");
            cameraNode.SetPosition(new Vector3f(0.0f, 0.6f, -4.5f));
            cameraNode.LookAt(Vector3f.Zero, Vector3f.Up);
            var camera = cameraNode.CreateComponent<Camera>();
            camera.SetAspectRatio(320.0f / 180.0f);
            camera.SetFov(36.0f);
            camera.SetNearClip(0.1f);
            camera.SetFarClip(100.0f);

            fixture.AssertMatches(
                new VisualRegressionImageCapture(generatedLightmap,
                    "lighting/generated-lightmap-texture-direct.png",
                    ChannelTolerance: 10, MaxDifferingPixelRatio: 0.03, MaxAverageChannelError: 3.0),
                new VisualRegressionImageCapture(fixture.Capture(scene, camera),
                    "lighting/generated-lightmap-direct.png",
                    ChannelTolerance: 10, MaxDifferingPixelRatio: 0.03, MaxAverageChannelError: 3.0));
        }
        finally
        {
            virtualFileSystem.Unmount(rootMount);
            Directory.Delete(outputDirectory, true);
        }
    }

    private static VisualRegressionImage ScaleNearest(VisualRegressionImage source, int scale)
    {
        var width = source.Width * scale;
        var height = source.Height * scale;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; ++y)
        for (var x = 0; x < width; ++x)
        {
            var sourceOffset = (x / scale + y / scale * source.Width) * 4;
            var destinationOffset = (x + y * width) * 4;
            source.Pixels.AsSpan(sourceOffset, 4).CopyTo(pixels.AsSpan(destinationOffset, 4));
        }
        return VisualRegressionImage.FromRgba(width, height, pixels);
    }
}
