// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class MaterialInspectorWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public MaterialInspectorWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Single_material_preview_renders_and_controls_camera_and_edit_signals()
    {
        _fixture.ResetGraphics();
        DearImGuiSubsystem? systemUI = null;
        Material? material = null;
        MaterialInspectorWidget? widget = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);

            material = CreateMaterial("Materials/PreviewTest.xml");
            widget = new MaterialInspectorWidget([material], string.Empty);
            widget.UpdateTechniques("Techniques/");

            var editBeginCount = 0;
            var editEndCount = 0;
            widget.OnEditBegin += () => ++editBeginCount;
            widget.OnEditEnd += () => ++editEndCount;

            RenderWidgetFrame(widget, Activation.None);
            Assert.Same(material, Assert.Single(widget.GetMaterials()));

            var previewWidget = GetField<SceneRendererToTexture>(widget, "_previewWidget");
            var cameraNode = previewWidget.CameraNode!;
            cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -2.7f));
            var moveAndOrbit = cameraNode.GetComponent<RedBolt.Input.MoveAndOrbitComponent>()!;
            moveAndOrbit.SetYaw(25.0f);
            moveAndOrbit.SetPitch(10.0f);

            RenderWidgetFrame(widget, Activation.ResetCamera);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Equal(Vector3f.Back * 2.0f, cameraNode.GetPosition());
            Assert.Equal(0.0f, moveAndOrbit.GetYaw());
            Assert.Equal(0.0f, moveAndOrbit.GetPitch());

            var previewScene = GetField<Scene>(widget, "_previewScene");
            var zone = previewScene.FindComponent<Zone>()!;
            var zoneTextureBefore = zone.GetZoneTexture();
            RenderWidgetFrame(widget, Activation.ToggleSkybox);
            RenderWidgetFrame(widget, Activation.None);
            Assert.NotEqual(zoneTextureBefore is null, zone.GetZoneTexture() is null);

            Assert.Equal(0, editBeginCount);
            Assert.Equal(0, editEndCount);
        }
        finally
        {
            widget?.Dispose();
            material?.Dispose();
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
        }
    }

    [NativeBindingFact]
    public void Multiple_materials_edit_techniques_properties_textures_and_shader_parameters()
    {
        _fixture.ResetGraphics();
        DearImGuiSubsystem? systemUI = null;
        Material? first = null;
        Material? second = null;
        MaterialInspectorWidget? widget = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);

            first = CreateMaterial("Materials/FirstTest.xml");
            second = CreateMaterial("Materials/SecondTest.xml");
            second.SetTechniques([]);
            var blackTexture = _fixture.Resources.Get<Texture2D>("Textures/Black.png");
            first.SetTexture(ShaderResources.Albedo, blackTexture);
            second.SetTexture(ShaderResources.Albedo, blackTexture);
            first.SetTexture("CustomMap", blackTexture);
            second.SetTexture("CustomMap", blackTexture);
            first.SetShaderParameter("TestParameter", 2.0f);
            second.SetShaderParameter("TestParameter", 2.0f);

            widget = new MaterialInspectorWidget([first, second], string.Empty);
            widget.UpdateTechniques("Techniques/");
            var editBeginCount = 0;
            var editEndCount = 0;
            widget.OnEditBegin += () => ++editBeginCount;
            widget.OnEditEnd += () => ++editEndCount;

            RenderWidgetFrame(widget, Activation.None);
            RenderWidgetFrame(widget, Activation.MergeTechniques);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Equal(first.GetTechniques(), second.GetTechniques());

            RenderWidgetFrame(widget, Activation.RemoveTechnique);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Empty(first.GetTechniques());
            Assert.Empty(second.GetTechniques());

            RenderWidgetFrame(widget, Activation.AddTechnique);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Single(first.GetTechniques());
            Assert.Single(second.GetTechniques());

            Assert.False(first.GetAlphaToCoverage());
            Assert.False(second.GetAlphaToCoverage());
            RenderWidgetFrame(widget, Activation.ToggleAlphaToCoverage);
            RenderWidgetFrame(widget, Activation.None);
            Assert.True(first.GetAlphaToCoverage());
            Assert.True(second.GetAlphaToCoverage());

            RenderWidgetFrame(widget, Activation.RemoveAlbedoTexture);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Null(first.GetTexture(ShaderResources.Albedo));
            Assert.Null(second.GetTexture(ShaderResources.Albedo));

            RenderWidgetFrame(widget, Activation.RemoveCustomTexture);
            RenderWidgetFrame(widget, Activation.None);
            Assert.Null(first.GetTexture("CustomMap"));
            Assert.Null(second.GetTexture("CustomMap"));

            RenderWidgetFrame(widget, Activation.RemoveShaderParameter);
            RenderWidgetFrame(widget, Activation.None);
            Assert.False(first.TryGetShaderParameter("TestParameter", out _));
            Assert.False(second.TryGetShaderParameter("TestParameter", out _));

            SetField(widget, "_newParameterName", "AddedParameter");
            RenderWidgetFrame(widget, Activation.AddShaderParameter);
            RenderWidgetFrame(widget, Activation.None);
            Assert.True(first.TryGetShaderParameter("AddedParameter", out var firstValue));
            Assert.True(second.TryGetShaderParameter("AddedParameter", out var secondValue));
            Assert.Equal(Vector4f.One, Assert.IsType<Vector4f>(firstValue));
            Assert.Equal(Vector4f.One, Assert.IsType<Vector4f>(secondValue));

            RenderWidgetFrame(widget, Activation.ForceSave);
            RenderWidgetFrame(widget, Activation.None);

            Assert.Equal(editBeginCount, editEndCount);
            Assert.Equal(9, editBeginCount);
        }
        finally
        {
            widget?.Dispose();
            second?.Dispose();
            first?.Dispose();
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
        }
    }

    [NativeBindingFact]
    public void Techniques_outside_core_data_are_never_deprecated_and_core_data_is_detected_by_path_prefix()
    {
        _fixture.ResetGraphics();
        DearImGuiSubsystem? systemUI = null;
        Material? material = null;
        MaterialInspectorWidget? coreWidget = null;
        MaterialInspectorWidget? externalWidget = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);

            material = CreateMaterial("Materials/TechniqueSelectorTest.xml");
            var litOpaquePath = _fixture.Resources.Get<Technique>("Techniques/LitOpaque.xml").GetAbsoluteFileName();
            var corePrefix = litOpaquePath[..litOpaquePath.LastIndexOf("Techniques/", StringComparison.Ordinal)];
            Assert.NotEmpty(corePrefix);

            coreWidget = new MaterialInspectorWidget([material], corePrefix);
            coreWidget.UpdateTechniques("Techniques/");
            var coreTechniques = GetTechniqueDescriptions(coreWidget);
            Assert.NotEmpty(coreTechniques);
            var litOpaque = Assert.Single(
                coreTechniques, static item => item.ResourceName == "Techniques/LitOpaque.xml");
            Assert.True(litOpaque.IsCoreData);
            Assert.False(litOpaque.Deprecated);
            Assert.All(
                coreTechniques.Where(static item =>
                    item.ResourceName.StartsWith("Techniques/PBR/", StringComparison.Ordinal)),
                static item => Assert.True(item.Deprecated));

            externalWidget = new MaterialInspectorWidget([material], corePrefix + "NotCoreData/");
            externalWidget.UpdateTechniques("Techniques/");
            var externalTechniques = GetTechniqueDescriptions(externalWidget);
            Assert.Equal(coreTechniques.Count, externalTechniques.Count);
            Assert.All(externalTechniques, static item => Assert.False(item.IsCoreData));
            Assert.All(externalTechniques, static item => Assert.False(item.Deprecated));
            Assert.Equal(
                externalTechniques.Select(static item => item.DisplayName).Order(StringComparer.Ordinal),
                externalTechniques.Select(static item => item.DisplayName));
        }
        finally
        {
            externalWidget?.Dispose();
            coreWidget?.Dispose();
            material?.Dispose();
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
        }
    }

    [Fact]
    public void Technique_order_is_deprecated_then_core_data_then_display_name()
    {
        var descriptions = new[]
        {
            CreateTechniqueDescription("Deprecated", isCoreData: true, deprecated: true),
            CreateTechniqueDescription("CoreB", isCoreData: true, deprecated: false),
            CreateTechniqueDescription("UserZ", isCoreData: false, deprecated: false),
            CreateTechniqueDescription("CoreA", isCoreData: true, deprecated: false),
            CreateTechniqueDescription("UserA", isCoreData: false, deprecated: false),
        };

        var compareTo = descriptions[0].GetType().GetMethod("CompareTo", [descriptions[0].GetType()])!;
        Array.Sort(descriptions, (lhs, rhs) => (int)compareTo.Invoke(lhs, [rhs])!);

        Assert.Equal(
            ["UserA", "UserZ", "CoreA", "CoreB", "Deprecated"],
            descriptions.Select(static item => GetDescriptionField<string>(item, "DisplayName")));
    }

    [Fact]
    public void Technique_color_distinguishes_user_core_and_deprecated_techniques()
    {
        var getColor = typeof(MaterialInspectorWidget).GetMethod(
            "GetTechniqueColor", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(new Color(0.5f, 1.0f, 0.0f, 1.0f), (Color)getColor.Invoke(null, [false, false])!);
        Assert.Equal(new Color(0.5f, 1.0f, 0.0f, 1.0f), (Color)getColor.Invoke(null, [false, true])!);
        Assert.Equal(new Color(1.0f, 1.0f, 1.0f, 1.0f), (Color)getColor.Invoke(null, [true, false])!);
        Assert.Equal(new Color(0.5f, 0.5f, 0.5f, 1.0f), (Color)getColor.Invoke(null, [true, true])!);
    }

    private readonly record struct TechniqueSnapshot(
        string ResourceName, string DisplayName, bool IsCoreData, bool Deprecated);

    private static List<TechniqueSnapshot> GetTechniqueDescriptions(MaterialInspectorWidget widget)
    {
        return GetField<IList>(widget, "_sortedTechniques").Cast<object>()
            .Select(static item => new TechniqueSnapshot(
                GetDescriptionField<string>(item, "ResourceName"),
                GetDescriptionField<string>(item, "DisplayName"),
                GetDescriptionField<bool>(item, "IsCoreData"),
                GetDescriptionField<bool>(item, "Deprecated")))
            .ToList();
    }

    private static object CreateTechniqueDescription(string displayName, bool isCoreData, bool deprecated)
    {
        var type = typeof(MaterialInspectorWidget).GetNestedType("TechniqueDesc", BindingFlags.NonPublic)!;
        var description = Activator.CreateInstance(type)!;
        type.GetField("DisplayName")!.SetValue(description, displayName);
        type.GetField("IsCoreData")!.SetValue(description, isCoreData);
        type.GetField("Deprecated")!.SetValue(description, deprecated);
        return description;
    }

    private static T GetDescriptionField<T>(object description, string name)
    {
        return (T)description.GetType().GetField(name)!.GetValue(description)!;
    }

    private Material CreateMaterial(string name)
    {
        var material = new Material();
        material.SetName(name);
        material.SetNumTechniques(1);
        material.SetTechnique(0, _fixture.Resources.Get<Technique>("Techniques/LitOpaque.xml"));
        return material;
    }

    private static void RenderWidgetFrame(MaterialInspectorWidget widget, Activation activation)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(800.0f, 1000.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(720.0f, 920.0f));
            var isOpen = true;
            ImGui.Begin("Material Inspector Widget"u8, ref isOpen);
            windowStarted = true;

            widget.RenderTitle();
            ImGui.Separator();
            widget.RenderContent();
            Activate(activation);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static readonly CString _resetCameraLabel = new($"{Icons.ArrowsLeftRightToLine}##ResetCamera");
    private static readonly CString _toggleSkyboxLabel = new($"{Icons.Cloud}##ToggleSkybox");
    private static readonly CString _forceSaveLabel = new($"{Icons.FloppyDisk} Force Save");

    private static void Activate(Activation activation)
    {
        switch (activation)
        {
        case Activation.None:
            break;
        case Activation.ResetCamera:
            ImGui.ActivateItemByID(ImGui.GetID(_resetCameraLabel));
            break;
        case Activation.ToggleSkybox:
            ImGui.ActivateItemByID(ImGui.GetID(_toggleSkyboxLabel));
            break;
        case Activation.ForceSave:
            ImGui.ActivateItemByID(ImGui.GetID(_forceSaveLabel));
            break;
        case Activation.MergeTechniques:
            using (new IdScopeGuard("RenderTechniques"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.CodeMergeU8));
            break;
        case Activation.RemoveTechnique:
            using (new IdScopeGuard("RenderTechniques"u8))
            using (new IdScopeGuard(0))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.TrashCanU8));
            break;
        case Activation.AddTechnique:
            using (new IdScopeGuard("RenderTechniques"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.SquarePlusU8));
            break;
        case Activation.ToggleAlphaToCoverage:
            using (new IdScopeGuard("RenderProperties"u8))
            using (new IdScopeGuard("Alpha To Coverage"u8))
                ImGui.ActivateItemByID(ImGui.GetID(ReadOnlySpan<byte>.Empty));
            break;
        case Activation.RemoveAlbedoTexture:
            using (new IdScopeGuard("RenderTextures"u8))
            using (new IdScopeGuard("Albedo"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.TrashCanU8));
            break;
        case Activation.RemoveCustomTexture:
            using (new IdScopeGuard("RenderTextures"u8))
            using (new IdScopeGuard("CustomMap"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.TrashCanU8));
            break;
        case Activation.RemoveShaderParameter:
            using (new IdScopeGuard("RenderShaderParameters"u8))
            using (new IdScopeGuard("TestParameter"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.TrashCanU8));
            break;
        case Activation.AddShaderParameter:
            using (new IdScopeGuard("RenderShaderParameters"u8))
                ImGui.ActivateItemByID(ImGui.GetID(Icons.SquarePlusU8));
            break;
        }
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private enum Activation
    {
        None,
        ResetCamera,
        ToggleSkybox,
        ForceSave,
        MergeTechniques,
        RemoveTechnique,
        AddTechnique,
        ToggleAlphaToCoverage,
        RemoveAlbedoTexture,
        RemoveCustomTexture,
        RemoveShaderParameter,
        AddShaderParameter,
    }
}
