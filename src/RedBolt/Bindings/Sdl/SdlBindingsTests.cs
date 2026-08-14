namespace RedBolt.Tests;

using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.Sdl;

public sealed class SdlBindingsTests
{
    private const uint SdlInitVideo = 0x00000020u;

    private static void RequireSdl()
    {
        if (Sdl.SdlInit(0) != 0)
            throw new InvalidOperationException("SDL native capability could not initialize.");
        Sdl.SdlQuit();
    }

    [SdlVideoFact]
    public void Init_and_was_init_work()
    {
        RequireSdl();
        EnsureDummyVideoDriver();
        var result = Sdl.SdlInit(SdlInitVideo);
        try
        {
            Assert.Equal(0, result);
            var initialized = Sdl.SdlWasInit(SdlInitVideo);
            Assert.NotEqual(0u, initialized & SdlInitVideo);
        }
        finally
        {
            Sdl.SdlQuit();
        }
    }

    [SdlVideoFact]
    public void CreateWindow_returns_valid_window()
    {
        RequireSdl();
        EnsureDummyVideoDriver();
        var result = Sdl.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);
        try
        {
            using var title = PinUtf8(string.Empty);
            using var window = Sdl.SdlCreateWindow(
                title.Bytes, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)SdlWindowFlags.SdlWindowHidden);
            Assert.False(window.IsNull);
        }
        finally
        {
            Sdl.SdlQuit();
        }
    }

    [SdlVideoFact]
    public void PollEvent_works()
    {
        RequireSdl();
        EnsureDummyVideoDriver();
        var result = Sdl.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);
        try
        {
            using var title = PinUtf8(string.Empty);
            using var window = Sdl.SdlCreateWindow(
                title.Bytes, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)SdlWindowFlags.SdlWindowHidden);
            Assert.False(window.IsNull);
            using var @event = new SdlEvent();
            _ = Sdl.SdlPollEvent(@event);
        }
        finally
        {
            Sdl.SdlQuit();
        }
    }

    [SdlGraphicsFact]
    public void GlGetProcAddress_generated_utf8_marshaller_resolves_real_symbol()
    {
        RequireSdl();

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        var result = Sdl.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);

        SdlWindow? window = null;
        nint glContext = 0;
        try
        {
            CheckSdlGl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3));
            CheckSdlGl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3));
            CheckSdlGl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore));
            CheckSdlGl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1));

            using var title = PinUtf8("RedBolt Tests");
            window = Sdl.SdlCreateWindow(
                title.Bytes, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)(SdlWindowFlags.SdlWindowHidden | SdlWindowFlags.SdlWindowOpengl));
            Assert.False(window.IsNull);

            glContext = Sdl.SdlGlCreateContext(window);
            Assert.NotEqual(IntPtr.Zero, glContext);
            CheckSdlGl(Sdl.SdlGlMakeCurrent(window, glContext));

            using var proc = PinUtf8("glFinish");
            var address = Sdl.SdlGlGetProcAddress(proc.Bytes);
            Assert.NotEqual(IntPtr.Zero, address);
        }
        finally
        {
            if (glContext != 0)
            {
                if (window is not null && !window.IsNull)
                    _ = Sdl.SdlGlMakeCurrent(window, IntPtr.Zero);
                Sdl.SdlGlDeleteContext(glContext);
            }

            if (window is not null && !window.IsNull)
                Sdl.SdlDestroyWindow(window);

            Sdl.SdlQuit();
        }
    }

    private static void EnsureDummyVideoDriver()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "dummy");
    }

    private static void CheckSdlGl(int result)
    {
        if (result != 0)
            throw new InvalidOperationException($"SDL GL operation failed with {result}.");
    }

    private static Utf8Pin PinUtf8(string value) => new(value);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class SdlVideoFactAttribute : Xunit.FactAttribute
    {
        public SdlVideoFactAttribute()
        {
            if (!HasNativeLibrary())
            {
                Skip = "SDL native capability is unavailable on this test host.";
                return;
            }

            var hasDisplay = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            var driver = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");
            if (!hasDisplay && !string.Equals(driver, "dummy", StringComparison.OrdinalIgnoreCase))
                Skip = "SDL video tests need a display or SDL_VIDEODRIVER=dummy.";
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class SdlGraphicsFactAttribute : Xunit.FactAttribute
    {
        public SdlGraphicsFactAttribute()
        {
            if (!HasNativeLibrary())
            {
                Skip = "SDL native capability is unavailable on this test host.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
                Skip = "SDL OpenGL symbol lookup requires an X11 display; use the software-renderer harness.";
        }
    }

    private static bool HasNativeLibrary()
    {
        return NativeBindingCapability.IsAvailable;
    }

    private sealed class Utf8Pin : IDisposable
    {
        private readonly byte[] _bytes;
        private readonly GCHandle _handle;

        public Utf8Pin(string value)
        {
            _bytes = Encoding.UTF8.GetBytes(value + '\0');
            _handle = GCHandle.Alloc(_bytes, GCHandleType.Pinned);
        }

        public ReadOnlySpan<byte> Bytes => _bytes;

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
