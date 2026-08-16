// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Audio;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class StandardFileTypesTests
{
    private readonly GraphicsEngineFixture _fixture;

    public StandardFileTypesTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Standard_extensions_and_xml_roots_are_tagged_in_native_order()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.StandardFileTypesTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);

        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();
        Project? project = null;
        try
        {
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            StandardFileTypes.Register(project);

            WriteFile(dataPath, "Generic.bin", "binary");
            WriteFile(dataPath, "Generic.xml", "<root />");
            WriteFile(dataPath, "Generic.json", "{}");
            WriteFile(dataPath, "Sound.wav", "sound");
            WriteFile(dataPath, "World.scene", "scene");
            WriteFile(dataPath, "LegacyScene.xml", "<scene />");
            WriteFile(dataPath, "NewScene.xml", "<resource _id=\"1\" />");
            WriteFile(dataPath, "Surface.material", "material");
            WriteFile(dataPath, "Material.xml", "<material />");
            WriteFile(dataPath, "Object.serializable", "serializable");
            WriteFile(dataPath, "Pipeline.renderpath", "renderpath");
            WriteFile(dataPath, "Image.png", "image");
            WriteFile(dataPath, "Cube.xml", "<cubemap />");
            WriteFile(dataPath, "Volume.xml", "<texture3d />");
            WriteFile(dataPath, "Array.xml", "<texturearray />");
            WriteFile(dataPath, "Mesh.mdl", "model");
            WriteFile(dataPath, "Motion.ani", "animation");
            WriteFile(dataPath, "Animation.xml", "<animation />");
            WriteFile(dataPath, "Import.assetpipeline", "pipeline");
            WriteFile(dataPath, "Import.AssetPipeline.json", "{}");
            WriteFile(dataPath, "Typeface.ttf", "font");
            WriteFile(dataPath, "Object.prefab", "prefab");

            AssertTypes<BinaryFile>(project, "Generic.bin");
            AssertTypes<BinaryFile, XMLFile>(project, "Generic.xml");
            AssertTypes<BinaryFile, JSONFile>(project, "Generic.json");
            AssertTypes<BinaryFile, Sound>(project, "Sound.wav");
            AssertTypes<BinaryFile, Scene, SceneResource>(project, "World.scene");
            AssertTypes<BinaryFile, XMLFile, Scene, SceneResource, PrefabResource>(
                project, "LegacyScene.xml");
            AssertTypes<BinaryFile, XMLFile, Scene, SceneResource>(project, "NewScene.xml");
            AssertTypes<BinaryFile, Material>(project, "Surface.material");
            AssertTypes<BinaryFile, XMLFile, Material>(project, "Material.xml");
            AssertTypes<BinaryFile, SerializableResource>(project, "Object.serializable");
            AssertTypes<BinaryFile, RenderPath>(project, "Pipeline.renderpath");
            AssertTypes<BinaryFile, Texture, Texture2D>(project, "Image.png");
            AssertTypes<BinaryFile, XMLFile, Texture, TextureCube>(project, "Cube.xml");
            AssertTypes<BinaryFile, XMLFile, Texture, Texture3D>(project, "Volume.xml");
            AssertTypes<BinaryFile, XMLFile, Texture, Texture2DArray>(project, "Array.xml");
            AssertTypes<BinaryFile, Model>(project, "Mesh.mdl");
            AssertTypes<BinaryFile, Animation>(project, "Motion.ani");
            AssertTypes<BinaryFile, XMLFile, Animation>(project, "Animation.xml");
            AssertTypes<BinaryFile, AssetPipeline>(project, "Import.assetpipeline");
            AssertTypes<BinaryFile, JSONFile, AssetPipeline>(project, "Import.AssetPipeline.json");
            AssertTypes<BinaryFile, Font>(project, "Typeface.ttf");
            AssertTypes<BinaryFile, PrefabResource>(project, "Object.prefab");
        }
        finally
        {
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static void AssertTypes<T1>(Project project, string resourceName)
    {
        var descriptor = project.GetResourceDescriptor(resourceName);
        Assert.True(descriptor.HasObjectType<T1>());
        Assert.Equal(typeof(T1).GetClassName(), descriptor.MostDerivedType);
    }

    private static void AssertTypes<T1, T2>(Project project, string resourceName)
    {
        var descriptor = project.GetResourceDescriptor(resourceName);
        Assert.True(descriptor.HasObjectType<T1>());
        Assert.True(descriptor.HasObjectType<T2>());
        Assert.Equal(typeof(T2).GetClassName(), descriptor.MostDerivedType);
    }

    private static void AssertTypes<T1, T2, T3>(Project project, string resourceName)
    {
        var descriptor = project.GetResourceDescriptor(resourceName);
        Assert.True(descriptor.HasObjectType<T1>());
        Assert.True(descriptor.HasObjectType<T2>());
        Assert.True(descriptor.HasObjectType<T3>());
        Assert.Equal(typeof(T3).GetClassName(), descriptor.MostDerivedType);
    }

    private static void AssertTypes<T1, T2, T3, T4>(Project project, string resourceName)
    {
        var descriptor = project.GetResourceDescriptor(resourceName);
        Assert.True(descriptor.HasObjectType<T1>());
        Assert.True(descriptor.HasObjectType<T2>());
        Assert.True(descriptor.HasObjectType<T3>());
        Assert.True(descriptor.HasObjectType<T4>());
        Assert.Equal(typeof(T4).GetClassName(), descriptor.MostDerivedType);
    }

    private static void AssertTypes<T1, T2, T3, T4, T5>(Project project, string resourceName)
    {
        var descriptor = project.GetResourceDescriptor(resourceName);
        Assert.True(descriptor.HasObjectType<T1>());
        Assert.True(descriptor.HasObjectType<T2>());
        Assert.True(descriptor.HasObjectType<T3>());
        Assert.True(descriptor.HasObjectType<T4>());
        Assert.True(descriptor.HasObjectType<T5>());
        Assert.Equal(typeof(T5).GetClassName(), descriptor.MostDerivedType);
    }

    private static void WriteFile(string dataPath, string resourceName, string contents)
    {
        File.WriteAllText(Path.Combine(dataPath, resourceName), contents);
    }
}
