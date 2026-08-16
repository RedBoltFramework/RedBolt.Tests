// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CreateDefaultSceneTests
{
    [Fact]
    public void Default_scene_contains_octree_only()
    {
        var directory = CreateTempDirectory();
        try
        {
            var fileName = Path.Combine(directory, "Default.scene");
            var parameters = new DefaultSceneParameters();

            DefaultSceneFunctions.CreateDefaultScene(fileName, in parameters);

            using var resource = new SceneResource();
            Assert.True(resource.LoadFile(new RedBolt.IO.FileIdentifier(fileName)));
            Assert.NotNull(resource.Scene.GetComponent<Octree>());
            Assert.Null(resource.Scene.GetComponent<RenderPipeline>());
            Assert.Equal(0, resource.Scene.GetNumChildren());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void High_quality_xml_scene_contains_default_objects_and_render_settings()
    {
        var directory = CreateTempDirectory();
        try
        {
            var fileName = Path.Combine(directory, "Default.XML");
            var parameters = new DefaultSceneParameters
            {
                HighQuality = true,
                CreateObjects = true
            };

            DefaultSceneFunctions.CreateDefaultScene(fileName, in parameters);

            using var resource = new SceneResource();
            Assert.True(resource.LoadFile(new RedBolt.IO.FileIdentifier(fileName)));
            var scene = resource.Scene;
            var renderPipeline = scene.GetComponent<RenderPipeline>();
            Assert.NotNull(renderPipeline);
            Assert.Equal(RenderPipelineColorSpace.LinearHDR, renderPipeline!.Settings.RenderBufferManager.ColorSpace);
            Assert.Equal(5, renderPipeline.Settings.SceneProcessor.PcfKernelSize);
            Assert.Contains(renderPipeline.GetRenderPasses(), pass => pass == ("Postprocess: FXAA v3", true));

            var skybox = scene.GetChild("Skybox")?.GetComponent<Skybox>();
            Assert.NotNull(skybox);
            Assert.Equal("Models/Box.mdl", skybox!.GetModel()?.GetName());
            Assert.Equal("Materials/DefaultSkybox.xml", skybox.GetMaterial()?.GetName());

            var zone = scene.GetChild("Global Zone")?.GetComponent<Zone>();
            Assert.NotNull(zone);
            Assert.Equal(Color.Black, zone!.GetAmbientColor());
            Assert.Equal(0.5f, zone.GetBackgroundBrightness());

            var light = scene.GetChild("Global Light")?.GetComponent<Light>();
            Assert.NotNull(light);
            Assert.Equal(LightType.Directional, light!.GetLightType());
            Assert.Equal(Color.White, light.GetColor());
            Assert.Equal(0.5f, light.GetBrightness());
            Assert.True(light.GetCastShadows());

            var cube = scene.GetChild("Sample Cube")?.GetComponent<StaticModel>();
            Assert.NotNull(cube);
            Assert.Equal("Models/TeaPot.mdl", cube!.GetModel()?.GetName());
            Assert.True(cube.GetCastShadows());
            Assert.Equal(new Vector3f(3.0f, 3.0f, 3.0f), cube.Node.Scale);

            var plane = scene.GetChild("Ground Plane")?.GetComponent<StaticModel>();
            Assert.NotNull(plane);
            Assert.Equal("Models/Plane.mdl", plane!.GetModel()?.GetName());
            Assert.Equal(new Vector3f(7.0f, 7.0f, 7.0f), plane.Node.Scale);
            Assert.Null(scene.GetChild("[Prefab Node]"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Prefab_contains_prefab_objects_without_scene_light_or_cube()
    {
        var directory = CreateTempDirectory();
        try
        {
            var fileName = Path.Combine(directory, "Default.prefab");
            var parameters = new DefaultSceneParameters
            {
                CreateObjects = true,
                IsPrefab = true
            };

            DefaultSceneFunctions.CreateDefaultScene(fileName, in parameters);

            using var resource = new PrefabResource();
            Assert.True(resource.LoadFile(new RedBolt.IO.FileIdentifier(fileName)));
            var scenePrefab = resource.GetScenePrefab();
            Assert.Equal(4, scenePrefab.Children.Count);
            Assert.Equal("[Prefab Node]", scenePrefab.Children[0].GetNodeName());
            Assert.Equal("Skybox", scenePrefab.Children[1].GetNodeName());
            Assert.Equal("Global Zone", scenePrefab.Children[2].GetNodeName());
            Assert.Equal("Ground Plane", scenePrefab.Children[3].GetNodeName());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltDefaultScene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
