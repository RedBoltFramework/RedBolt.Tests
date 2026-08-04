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
        if (TestGraphicsBackend.Current == RenderBackend.Vulkan
            && !IsEnabled("REDBOLT_RUN_VULKAN_GRAPHICS_TESTS"))
        {
            Skip = "Set REDBOLT_RUN_VULKAN_GRAPHICS_TESTS=1 to run Vulkan graphics tests; the backend shares one Diligent device per test process.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Native graphics tests require a display; use run_tests.sh for a headless software-rendered run.";
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class GraphicsTheoryAttribute : Xunit.TheoryAttribute
{
    public GraphicsTheoryAttribute()
    {
        if (TestGraphicsBackend.Current == RenderBackend.Vulkan
            && !IsEnabled("REDBOLT_RUN_VULKAN_GRAPHICS_TESTS"))
        {
            Skip = "Set REDBOLT_RUN_VULKAN_GRAPHICS_TESTS=1 to run Vulkan graphics tests; the backend shares one Diligent device per test process.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Native graphics tests require a display; use run_tests.sh for a headless software-rendered run.";
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
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
            Skip = "Native graphics tests require a display; use run_tests.sh for a headless software-rendered run.";
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
                "ShaderCompiler", "Jolt"
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

            var nativeDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "runtimes"));
            var runtimeIds = new[]
            {
                System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                "linux-x64", "linux-arm64", "win-x64", "win-arm64", "osx-x64", "osx-arm64"
            };
            var libraryNames = OperatingSystem.IsWindows()
                ? new[] { "RedBolt.Native.*.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "libRedBolt.Native.*.dylib" }
                    : new[] { "libRedBolt.Native.*.so" };

            foreach (var runtimeId in runtimeIds.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var libraryName in libraryNames)
            {
                var runtimeNativeDirectory = Path.Combine(nativeDirectory, runtimeId, "native");
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

        if (!IsEnabled("REDBOLT_RUN_DILIGENT_GL_SMOKE"))
        {
            Skip = "Set REDBOLT_RUN_DILIGENT_GL_SMOKE=1 to enable the Diligent OpenGL smoke gate.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Skip = "Diligent OpenGL smoke requires a display; use the software-renderer harness.";
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
