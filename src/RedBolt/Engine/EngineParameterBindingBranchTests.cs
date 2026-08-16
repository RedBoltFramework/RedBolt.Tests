// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Engine;
using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineParameterBindingBranchTests
{
    [Fact]
    public void Parameter_binding_coerces_numeric_enums_and_non_string_booleans()
    {
        using var engine = new Engine();

        engine.SetParameter(EngineParameter.RenderBackend, (int)RenderBackend.Vulkan);
        engine.SetParameter(EngineParameter.Headless, 0);

        Assert.Equal(RenderBackend.Vulkan, engine.GetParameter(EngineParameter.RenderBackend));
        Assert.Equal(false, engine.GetParameter(EngineParameter.Headless));
    }

    [Fact]
    public void Parameter_binding_handles_reference_null_and_null_string_conversion_results()
    {
        using var engine = new Engine();

        engine.SetParameter(EngineParameter.ApplicationName, null);
        Assert.Null(engine.GetParameter(EngineParameter.ApplicationName));

        engine.SetParameter(EngineParameter.ApplicationName, new NullStringValue());
        Assert.Equal(string.Empty, engine.GetParameter(EngineParameter.ApplicationName));
    }

    private sealed class NullStringValue
    {
        public override string ToString() => null!;
    }
}
