namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class VariantWidgetTests
{
    [NativeBindingFact]
    public void Numeric_variant_editors_preserve_values_without_input()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(800.0f, 600.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(600.0f, 500.0f));
            var isOpen = true;
            ImGui.Begin("Variant Editors"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;

            Variant[] values =
            [
                new(42),
                new(true),
                new(1.25f),
                new(new Vector2f(1.0f, 2.0f)),
                new(new Vector2d(3.0, 4.0)),
                new(new Vector3f(5.0f, 6.0f, 7.0f)),
                new(new Vector3d(8.0, 9.0, 10.0)),
                new(new Vector4f(11.0f, 12.0f, 13.0f, 14.0f)),
                new(new Quaternion(new Vector3f(15.0f, 16.0f, 17.0f))),
                new(new Color(0.1f, 0.2f, 0.3f, 0.4f)),
                new(new Vector2i(18, 19)),
                new(new Vector3i(20, 21, 22)),
                new(new Rect(23.0f, 24.0f, 25.0f, 26.0f)),
                new(27L),
            ];

            for (var index = 0; index < values.Length; ++index)
            {
                using var idGuard = new IdScopeGuard(index);
                using var expected = values[index].Clone();
                Assert.False(Widgets.EditVariant(ref values[index]));
                Assert.Equal(expected, values[index]);
            }

            var colorVector = new Variant(new Vector3f(0.2f, 0.3f, 0.4f));
            var colorOptions = new Widgets.EditVariantOptions().AsColor();
            using (new IdScopeGuard("ColorVector"u8))
                Assert.False(Widgets.EditVariant(ref colorVector, in colorOptions));
            Assert.Equal(new Vector3f(0.2f, 0.3f, 0.4f), colorVector.GetVector3());

            var bitmask = new Variant(0x12345678u);
            var bitmaskOptions = new Widgets.EditVariantOptions().AsBitmask();
            using (new IdScopeGuard("Bitmask"u8))
                Assert.False(Widgets.EditVariant(ref bitmask, in bitmaskOptions));
            Assert.Equal(0x12345678u, bitmask.GetUInt());

            var enumValue = new Variant(1);
            var enumOptions = new Widgets.EditVariantOptions().Enum(new StringVector(["Zero", "One", "Two"]));
            using (new IdScopeGuard("Enum"u8))
                Assert.False(Widgets.EditVariant(ref enumValue, in enumOptions));
            Assert.Equal(1, enumValue.GetInt());

            foreach (ref var value in values.AsSpan())
                value.Dispose();
            colorVector.Dispose();
            bitmask.Dispose();
            enumValue.Dispose();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(context);
        }
    }
}
