// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.SystemUI;

public sealed class EditVariantOptionsTests
{
    [Fact]
    public void Edit_variant_options_preserve_defaults_chaining_and_metadata_aliases()
    {
        var enumValues = new StringVector { "Zero", "One" };
        var resourceTypes = new StringVector { "Texture2D" };
        var elementNames = new StringVector { "X", "Y" };

        var defaultOptions = new Widgets.EditVariantOptions();
        Assert.Equal(0.01, defaultOptions.StepValue);
        Assert.Equal(0.0, defaultOptions.MinimumValue);
        Assert.Equal(0.0, defaultOptions.MaximumValue);
        Assert.False(defaultOptions.IsColor);
        Assert.False(defaultOptions.IsResizeAllowed);
        Assert.False(defaultOptions.IsTypeChangeAllowed);
        Assert.False(defaultOptions.IsBitmask);
        Assert.False(defaultOptions.UsesDynamicMetadata);
        Assert.Null(defaultOptions.IntToString);
        Assert.Null(defaultOptions.ResourceTypeNames);
        Assert.Null(defaultOptions.SizedStructVectorElementNames);

        defaultOptions.AsColor();
        Assert.True(defaultOptions.IsColor);

        var options = new Widgets.EditVariantOptions()
            .AsColor()
            .AsBitmask()
            .Range(-2.0, 3.0)
            .Step(0.5)
            .Enum(enumValues)
            .ResourceTypes(resourceTypes)
            .SizedStructVector(elementNames)
            .AllowResize()
            .AllowTypeChange()
            .DynamicMetadata();

        Assert.Equal(0.5, options.StepValue);
        Assert.Equal(-2.0, options.MinimumValue);
        Assert.Equal(3.0, options.MaximumValue);
        Assert.True(options.IsColor);
        Assert.True(options.IsResizeAllowed);
        Assert.True(options.IsTypeChangeAllowed);
        Assert.True(options.IsBitmask);
        Assert.True(options.UsesDynamicMetadata);
        Assert.Same(enumValues, options.IntToString);
        Assert.Same(resourceTypes, options.ResourceTypeNames);
        Assert.Same(elementNames, options.SizedStructVectorElementNames);
    }
}
