namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.Resource;
using RedBolt.RmlUI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RmlSystemTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RmlSystemTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void RmlSystem_matches_engine_system_behavior()
    {
        using var localization = new Localization();
        _fixture.Context.RegisterSubsystem(localization);
        try
        {
            using var input = new Std.String();
            using var translated = new Std.String();
            using var message = new Std.String();
            using var system = new RmlSystem();
            input.Assign("Bye");
            message.Assign("RmlSystem test");

            Assert.Equal(0, system.TranslateString(translated, input));
            Assert.Equal("Bye", translated.ToString());

            using var json = new JSONValue();
            Assert.True(JSONFile.ParseJSON("{\"Bye\":\"Moi\"}", json));
            localization.LoadSingleLanguageJSON(json, "fi");
            localization.SetLanguage("fi");

            Assert.Equal(1, system.TranslateString(translated, input));
            Assert.Equal("Moi", translated.ToString());
            Assert.True(system.GetElapsedTime() >= 0.0);
            Assert.True(system.LogMessage(LogType.LtAlways, message));
            Assert.True(system.LogMessage(LogType.LtWarning, message));
            Assert.True(system.LogMessage(LogType.LtInfo, message));
            Assert.False(system.LogMessage(LogType.LtMax, message));

            system.SetMouseCursor(message);
            system.ActivateKeyboard(Vector2f.Zero, 0.0f);
            Assert.True(system.TextInputActivatedThisFrame());
            system.DeactivateKeyboard();
        }
        finally
        {
            _fixture.Context.RemoveSubsystem<Localization>();
        }
    }

    [RmlClipboardFact]
    public void RmlSystem_clipboard_round_trip_uses_the_available_sdl_backend()
    {
        using var message = new Std.String();
        using var retrieved = new Std.String();
        using var system = new RmlSystem();
        message.Assign("RmlSystem test");

        system.SetClipboardText(message);
        system.GetClipboardText(retrieved);

        Assert.Equal("RmlSystem test", retrieved.ToString());
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class RmlClipboardFactAttribute : Xunit.FactAttribute
    {
        public RmlClipboardFactAttribute()
        {
            if (!NativeBindingCapability.IsAvailable)
            {
                Skip = "SDL/Rml native capability is unavailable on this test host.";
                return;
            }

            var hasDisplay = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            var dummyVideo = string.Equals(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER"),
                "dummy", StringComparison.OrdinalIgnoreCase);
            if (!hasDisplay && !dummyVideo)
                Skip = "SDL clipboard requires a display or SDL_VIDEODRIVER=dummy; the core RmlSystem checks remain headless.";
        }
    }
}
