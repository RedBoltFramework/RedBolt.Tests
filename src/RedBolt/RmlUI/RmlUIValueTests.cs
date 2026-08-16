// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.Math;
using RedBolt.RmlUI;

[Collection(nameof(RmlUiBindingsCollection))]
public sealed class RmlUIValueTests
{
    [Fact]
    public void Detached_component_preserves_local_state_and_rejects_early_binding()
    {
        using var component = new RmlUIComponent();

        Assert.Equal(Vector2f.Zero, component.GetPosition());
        Assert.Equal(Vector2f.Zero, component.GetSize());
        Assert.True(component.GetAutoSize());
        Assert.False(component.GetUseNormalizedCoordinates());
        Assert.False(component.IsModal());
        Assert.Null(component.GetDocument());

        component.SetPosition(new Vector2f(10.0f, 20.0f));
        component.SetSize(new Vector2f(30.0f, 40.0f));
        component.SetAutoSize(true);
        component.SetUseNormalizedCoordinates(true);
        component.SetModal(true);
        component.SetEmSize(18.0f);
        component.SetResource("UI/missing.rml");

        Assert.Equal(new Vector2f(10.0f, 20.0f), component.GetPosition());
        Assert.Equal(new Vector2f(30.0f, 40.0f), component.GetSize());
        Assert.True(component.GetAutoSize());
        Assert.True(component.GetUseNormalizedCoordinates());
        Assert.True(component.IsModal());
        Assert.Equal(0.0f, component.GetEmSize());
        Assert.False(component.BindDataModelProperty("value", GetValue, SetValue));
    }

    [NativeBindingFact]
    public void Rml_variant_conversion_covers_none_and_string_values()
    {
        using var native = new Variant();
        using var none = new RedBolt.Core.Variant();
        Assert.True(RmlUI.ToRmlUi(none, native));
        Assert.Equal(VariantType.None, native.GetVariantType());

        using var value = new RedBolt.Core.Variant("hello");
        Assert.True(RmlUI.ToRmlUi(value, native));
        Assert.Equal(VariantType.String, native.GetVariantType());

        Assert.True(RmlUI.FromRmlUi(native, out var roundTrip));
        using (roundTrip)
            Assert.Equal("hello", roundTrip.GetString());
    }

    private static void GetValue(ref RedBolt.Core.Variant value)
    {
        value = new RedBolt.Core.Variant(1);
    }

    private static void SetValue(in RedBolt.Core.Variant value)
    {
    }
}
