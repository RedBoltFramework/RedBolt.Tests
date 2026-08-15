namespace RedBolt.Tests;

using RedBolt.RenderAPI;

/// <summary>
/// Discovery-time gate for tests that construct the hidden SDL graphics fixture.
/// Keeping the decision in the fact attribute prevents xUnit from constructing
/// a graphics collection fixture on a display-free worker.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class GraphicsFactAttribute : Xunit.FactAttribute
{
    public GraphicsFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Native graphics tests require a display; run with the workspace software-renderer helper.";
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class GraphicsTheoryAttribute : Xunit.TheoryAttribute
{
    public GraphicsTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Native graphics tests require a display; run with the workspace software-renderer helper.";
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class OpenGLFactAttribute : Xunit.FactAttribute
{
    public OpenGLFactAttribute()
    {
        if (TestGraphicsBackend.Current != RenderBackend.OpenGL)
        {
            Skip = "This test exercises an OpenGL-specific native reflection API.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Native graphics tests require a display; run with the workspace software-renderer helper.";
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class NativeBindingFactAttribute : Xunit.FactAttribute
{
    public NativeBindingFactAttribute()
    {
        if (!NativeBindingCapability.IsAvailable)
            Skip = "No RedBolt native module is available or the module architecture is incompatible with this test host.";
    }
}

internal static class NativeBindingCapability
{
    public static bool IsAvailable
    {
        get
        {
            var moduleNames = new[]
            {
                "Sdl", "Diligent", "DearImGui", "Stb", "Lz4", "HarfBuzz", "RmlUi",
                "WebP", "XAtlas", "Embree", "Glslang", "SpirVCross", "SpirVTools",
                "ShaderCompiler", "Box2D", "Jolt"
            };

            foreach (var moduleName in moduleNames)
            {
                if (System.Runtime.InteropServices.NativeLibrary.TryLoad(
                    "RedBolt.Native." + moduleName, out var directHandle))
                {
                    System.Runtime.InteropServices.NativeLibrary.Free(directHandle);
                    return true;
                }
            }

            var nativeSearchDirectories =
                AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string ?? string.Empty;
            var nativeLibraryPatterns = OperatingSystem.IsWindows()
                ? new[] { "RedBolt.Native.*.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "libRedBolt.Native.*.dylib" }
                    : new[] { "libRedBolt.Native.*.so" };
            foreach (var directory in nativeSearchDirectories.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var pattern in nativeLibraryPatterns)
            {
                if (!Directory.Exists(directory))
                    continue;
                foreach (var candidate in Directory.EnumerateFiles(directory, pattern))
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                    {
                        System.Runtime.InteropServices.NativeLibrary.Free(handle);
                        return true;
                    }
                }
            }

            var nativeRoot = Path.Combine(RbfxTestData.BuildRoot, "native");
            var runtimeIds = new[]
            {
                System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                "linux-x64", "linux-arm64", "win-x64", "win-arm64", "osx-x64", "osx-arm64"
            };

            foreach (var runtimeId in runtimeIds.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var libraryName in nativeLibraryPatterns)
            {
                var runtimeNativeDirectory = Path.Combine(
                    nativeRoot, runtimeId, "runtime", RbfxTestData.BuildConfiguration);
                if (!Directory.Exists(runtimeNativeDirectory))
                    continue;
                foreach (var candidate in Directory.EnumerateFiles(runtimeNativeDirectory, libraryName))
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                    {
                        System.Runtime.InteropServices.NativeLibrary.Free(handle);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DiligentGraphicsFactAttribute : Xunit.FactAttribute
{
    public DiligentGraphicsFactAttribute()
    {
        if (TestGraphicsBackend.Current != RenderBackend.OpenGL)
        {
            Skip = "The Diligent smoke test exercises the OpenGL backend; use the OpenGL coverage run.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Diligent OpenGL smoke requires a display; run with the workspace software-renderer helper.";
    }
}
