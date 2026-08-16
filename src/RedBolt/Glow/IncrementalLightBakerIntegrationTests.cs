// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Core;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class IncrementalLightBakerIntegrationTests
{
    [VisualFact]
    public void Direct_bake_produces_lit_chart_texels()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(), "RedBoltIncrementalLightBaker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        using var fixture = new VisualRegressionFixture(64, 64);
        GlowTestData.AddResourceDirs(fixture.Resources);
        var context = fixture.EngineFixture.Context;
        context.RegisterAssembly(typeof(IncrementalLightBaker).Assembly);
        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var rootMount = virtualFileSystem.MountRoot();
        try
        {
            var scene = fixture.Resources.Get<SceneResource>("Scenes/BakingTests/Direct.xml").Scene;
            var settings = CreateSettings(outputDirectory);
            using var collector = new DefaultBakedSceneCollector();
            using var cache = new BakedLightMemoryCache();
            using var baker = new IncrementalLightBaker();

            Assert.True(baker.Initialize(settings, scene, collector, cache));
            baker.ProcessScene();

            var chunks = collector.GetChunks();
            var chunkIndex = Assert.Single(chunks);
            var chunk = Assert.IsType<BakedSceneChunk>(cache.LoadBakedChunk(chunkIndex));
            var lightmapIndex = Assert.Single(chunk.Lightmaps);
            var geometryBuffer = Assert.Single(chunk.GeometryBuffers);
            Assert.Contains(geometryBuffer.GeometryIds, static id => id != 0);
            Assert.NotEmpty(chunk.BakedLights);

            var stopped = new StopToken();
            stopped.Stop();
            Assert.False(baker.Bake(stopped));
            Assert.True(baker.Bake(new StopToken()));
            var direct = Assert.IsType<LightmapChartBakedDirect>(cache.LoadDirectLight(lightmapIndex));
            Assert.Contains(direct.DirectLight, static light => light.LengthSquared > 0.000001f);
            var lightmap = Assert.IsType<BakedLightmap>(cache.LoadLightmap(lightmapIndex));
            Assert.Contains(lightmap.Lightmap, static light => light.LengthSquared > 0.000001f);
        }
        finally
        {
            virtualFileSystem.Unmount(rootMount);
            Directory.Delete(outputDirectory, true);
        }
    }

    [VisualFact]
    public void Indirect_bake_exercises_bounce_and_emission_scenes()
    {
        using var fixture = new VisualRegressionFixture(64, 64);
        GlowTestData.AddResourceDirs(fixture.Resources);
        var context = fixture.EngineFixture.Context;
        context.RegisterAssembly(typeof(IncrementalLightBaker).Assembly);
        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var rootMount = virtualFileSystem.MountRoot();
        try
        {
            BakeScene(fixture, "Scenes/BakingTests/BounceIndirect.xml", maxBounces: 1);
            BakeScene(fixture, "Scenes/BakingTests/EmissionIndirect.xml", maxBounces: 1);
        }
        finally
        {
            virtualFileSystem.Unmount(rootMount);
        }
    }

    private static void BakeScene(VisualRegressionFixture fixture, string sceneName, uint maxBounces)
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(), "RedBoltIncrementalLightBaker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var scene = fixture.Resources.Get<SceneResource>(sceneName).Scene;
            var settings = CreateSettings(outputDirectory, maxBounces);
            using var collector = new DefaultBakedSceneCollector();
            using var cache = new BakedLightMemoryCache();
            using var baker = new IncrementalLightBaker();

            Assert.True(baker.Initialize(settings, scene, collector, cache));
            baker.ProcessScene();
            Assert.True(baker.Bake(new StopToken()));
            baker.CommitScene();

            var chunkIndex = Assert.Single(collector.GetChunks());
            var chunk = Assert.IsType<BakedSceneChunk>(cache.LoadBakedChunk(chunkIndex));
            Assert.NotEmpty(chunk.Lightmaps);
            Assert.Equal(IncrementalLightBakerPhase.Finalizing, baker.Status.Phase);
            Assert.Contains(chunk.Lightmaps, lightmapIndex =>
                System.IO.File.Exists(Path.Combine(outputDirectory, "Textures", $"Lightmap-{lightmapIndex}.png")));
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    private static LightBakingSettings CreateSettings(string outputDirectory, uint maxBounces = 0)
    {
        var settings = new LightBakingSettings();
        var charting = settings.Charting;
        charting.LightmapSize = 32;
        charting.TexelDensity = 20.0f;
        settings.Charting = charting;
        var incremental = settings.Incremental;
        incremental.OutputDirectory = FileIdentifier.FromUri(outputDirectory).ToUri();
        settings.Incremental = incremental;
        var directChart = settings.DirectChartTracing;
        directChart.MaxSamples = 2;
        directChart.NumTasks = 32;
        settings.DirectChartTracing = directChart;
        var directProbes = settings.DirectProbesTracing;
        directProbes.MaxSamples = 2;
        directProbes.NumTasks = 32;
        settings.DirectProbesTracing = directProbes;
        var indirectChart = settings.IndirectChartTracing;
        indirectChart.MaxBounces = maxBounces;
        indirectChart.MaxSamples = 1;
        indirectChart.NumTasks = 32;
        settings.IndirectChartTracing = indirectChart;
        var indirectProbes = settings.IndirectProbesTracing;
        indirectProbes.MaxBounces = maxBounces;
        indirectProbes.MaxSamples = 1;
        indirectProbes.NumTasks = 32;
        settings.IndirectProbesTracing = indirectProbes;
        var geometryPreprocessing = settings.GeometryBufferPreprocessing;
        geometryPreprocessing.NumTasks = 32;
        settings.GeometryBufferPreprocessing = geometryPreprocessing;
        var emission = settings.EmissionTracing;
        emission.NumTasks = 32;
        settings.EmissionTracing = emission;
        return settings;
    }
}
