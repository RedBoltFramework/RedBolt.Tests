// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;

using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ResourceBrowserTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ResourceBrowserTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Tab_renders_tracks_selection_and_renames_resources_with_actions()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.ResourceBrowserTabTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var moduleDataPath = Path.Combine(projectPath, "Data.Module");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        Directory.CreateDirectory(moduleDataPath);
        Directory.CreateDirectory(Path.Combine(dataPath, "Folder"));
        Directory.CreateDirectory(Path.Combine(dataPath, "Bundle.asset.d"));
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(Path.Combine(dataPath, "Move.txt"), "move");
        File.WriteAllText(Path.Combine(dataPath, "Bundle.asset"), "bundle");
        File.WriteAllText(Path.Combine(dataPath, "Bundle.asset.d", "Part.bin"), "part");
        File.WriteAllText(Path.Combine(dataPath, "Folder", "Nested.txt"), "nested");
        File.WriteAllText(Path.Combine(editorDataPath, "EditorOnly.txt"), "editor");

        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var coreDataPath = FindMountedResourceDirectory(virtualFileSystem, "CoreData");
        File.WriteAllLines(Path.Combine(projectPath, "ResourceRoot.ini"),
        [
            $"CoreData={coreDataPath}",
            $"Data={dataPath}",
            $"EditorData={editorDataPath}",
            $"Data.Module={moduleDataPath}",
        ]);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        ResourceBrowserTab? tab = null;
        var imguiContext = IntPtr.Zero;
        try
        {
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);

            tab = new ResourceBrowserTab();
            Assert.Equal("Resources", tab.Title);
            Assert.Equal("Resources###96c69b8e-ee83-43de-885c-8a51cef65d59", tab.UniqueId);
            Assert.Equal(EditorTabFlag.OpenByDefault, tab.Flags);
            Assert.Equal(EditorTabPlacement.DockBottom, tab.Placement);
            Assert.True(tab.IsUndoSupported());

            var additionalResourceDirectories = project.ResourceDirectories
                .Where(path => !string.Equals(path, project.CoreDataPath, StringComparison.Ordinal)
                    && !string.Equals(path, project.DataPath, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.DoesNotContain(project.ResourceDirectories,
                path => string.Equals(NormalizeDirectory(path), NormalizeDirectory(dataPath), StringComparison.Ordinal));
            Assert.Contains(project.ResourceDirectories,
                path => string.Equals(NormalizeDirectory(path), NormalizeDirectory(moduleDataPath), StringComparison.Ordinal));
            Assert.Equal(1, CountMountedResourceDirectories(virtualFileSystem, dataPath));

            var normalizedEditorDataPath = NormalizeDirectory(editorDataPath);
            var editorRootIndex = 2 + Array.FindIndex(additionalResourceDirectories,
                path => string.Equals(NormalizeDirectory(path),
                    normalizedEditorDataPath, StringComparison.Ordinal));
            Assert.True(editorRootIndex >= 2);

            tab.SetSelection(Selection(editorRootIndex, string.Empty, "EditorOnly.txt"));
            var oldEditorFileName = Path.Combine(editorDataPath, "EditorOnly.txt");
            var newEditorFileName = Path.Combine(editorDataPath, "EditorRenamed.txt");
            tab.RenameOrMove(oldEditorFileName, newEditorFileName,
                "EditorOnly.txt", "EditorRenamed.txt", true);
            AssertSelection(tab, editorRootIndex, string.Empty, "EditorRenamed.txt");

            tab.SetSelection(Selection(string.Empty, string.Empty));
            var initialSelection = tab.GetSelection();
            Assert.Equal(1, initialSelection.SelectedRoot);
            Assert.Equal(string.Empty, initialSelection.SelectedLeftPath);
            Assert.Empty(initialSelection.SelectedRightPaths);
            initialSelection.SelectedLeftPath = "Changed outside the tab";
            Assert.Equal(string.Empty, tab.GetSelection().SelectedLeftPath);

            tab.ReadIniSettings("SelectedRightPaths=");
            Assert.Empty(tab.GetSelection().SelectedRightPaths);

            var folderSelection = Selection("Folder", "Folder/Nested.txt");
            tab.SetSelection(folderSelection);
            folderSelection.SelectedLeftPath = string.Empty;
            folderSelection.SelectedRightPaths.Clear();
            AssertSelection(tab, "Folder", "Folder/Nested.txt");

            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(900.0f, 700.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            RenderFrame(tab);

            using (var output = new ImGuiTextBuffer())
            {
                tab.WriteIniSettings(output);
                var settings = Encoding.UTF8.GetString(output.CStr());
                Assert.Contains("SelectedRoot=1\n", settings, StringComparison.Ordinal);
                Assert.Contains("SelectedLeftPath=Folder\n", settings, StringComparison.Ordinal);
                Assert.Contains("LastSelectedRightPath=Folder/Nested.txt\n", settings, StringComparison.Ordinal);
                Assert.Contains("SelectedRightPaths=Folder/Nested.txt\n", settings, StringComparison.Ordinal);
            }

            var oldSelection = Selection("Folder", "Folder/Nested.txt");
            var newSelection = Selection(string.Empty, "Move.txt");
            var selectionAction = new ChangeResourceSelectionAction(tab, oldSelection, newSelection);
            oldSelection.SelectedRightPaths.Clear();
            newSelection.SelectedRightPaths.Clear();
            Assert.True(selectionAction.IsTransparent());
            selectionAction.Redo();
            AssertSelection(tab, string.Empty, "Move.txt");

            var mergedSelection = Selection(string.Empty, "Bundle.asset");
            var mergedAction = new ChangeResourceSelectionAction(tab, newSelection, mergedSelection);
            Assert.True(selectionAction.MergeWith(mergedAction));
            mergedSelection.SelectedRightPaths.Clear();
            selectionAction.Redo();
            AssertSelection(tab, string.Empty, "Bundle.asset");
            selectionAction.Undo();
            AssertSelection(tab, "Folder", "Folder/Nested.txt");

            tab.SetSelection(Selection(string.Empty, "Move.txt"));
            var oldFileName = Path.Combine(dataPath, "Move.txt");
            var newFileName = Path.Combine(dataPath, "Renamed.txt");
            var renameAction = new RenameResourceAction(
                tab, oldFileName, newFileName, "Move.txt", "Renamed.txt");
            Assert.True(renameAction.CanRedo());
            renameAction.Redo();
            Assert.False(File.Exists(oldFileName));
            Assert.True(File.Exists(newFileName));
            AssertSelection(tab, string.Empty, "Renamed.txt");
            Assert.True(renameAction.CanUndo());
            renameAction.Undo();
            Assert.True(File.Exists(oldFileName));
            Assert.False(File.Exists(newFileName));
            AssertSelection(tab, string.Empty, "Move.txt");

            tab.Dispose();
            tab = null;
        }
        finally
        {
            tab?.Dispose();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            Directory.Delete(projectPath, true);
        }
    }

    private static string FindMountedResourceDirectory(VirtualFileSystem virtualFileSystem, string name)
    {
        for (var i = 0u; i < virtualFileSystem.NumMountPoints(); ++i)
        {
            if (virtualFileSystem.GetMountPoint(i) is MountedDirectory directory
                && string.Equals(Path.GetFileName(NormalizeDirectory(directory.GetDirectory())), name,
                    StringComparison.Ordinal))
            {
                return directory.GetDirectory();
            }
        }
        throw new InvalidOperationException($"Mounted resource directory '{name}' is not found.");
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static int CountMountedResourceDirectories(VirtualFileSystem virtualFileSystem, string path)
    {
        var count = 0;
        for (var i = 0u; i < virtualFileSystem.NumMountPoints(); ++i)
        {
            if (virtualFileSystem.GetMountPoint(i) is MountedDirectory directory
                && string.Equals(NormalizeDirectory(directory.GetDirectory()), NormalizeDirectory(path),
                    StringComparison.Ordinal))
            {
                ++count;
            }
        }
        return count;
    }

    private static ResourceBrowserTab.Selection Selection(string leftPath, string rightPath)
    {
        return Selection(1, leftPath, rightPath);
    }

    private static ResourceBrowserTab.Selection Selection(int rootIndex, string leftPath, string rightPath)
    {
        var result = new ResourceBrowserTab.Selection
        {
            SelectedRoot = rootIndex,
            SelectedLeftPath = leftPath,
        };
        if (rightPath.Length != 0)
            result.SelectedRightPaths.Add(rightPath);
        return result;
    }

    private static void AssertSelection(
        ResourceBrowserTab tab, string leftPath, string rightPath)
    {
        AssertSelection(tab, 1, leftPath, rightPath);
    }

    private static void AssertSelection(
        ResourceBrowserTab tab, int rootIndex, string leftPath, string rightPath)
    {
        var selection = tab.GetSelection();
        Assert.Equal(rootIndex, selection.SelectedRoot);
        Assert.Equal(leftPath, selection.SelectedLeftPath);
        Assert.Equal([rightPath], selection.SelectedRightPaths);
    }

    private static void RenderFrame(ResourceBrowserTab tab)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(800.0f, 600.0f));
            var open = true;
            Assert.True(ImGui.Begin("Resource browser tab test"u8, ref open));
            windowStarted = true;
            tab.RenderContent();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }
}
