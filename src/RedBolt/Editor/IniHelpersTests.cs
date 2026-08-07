namespace RedBolt.Tests;

using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class IniHelpersTests
{
    [NativeBindingFact]
    public void String_and_integer_values_round_trip_through_ini_lines()
    {
        using var output = new ImGuiTextBuffer();

        IniHelpers.WriteStringToIni(output, "Name", "Žaibas");
        IniHelpers.WriteIntToIni(output, "Count", -42);

        Assert.Equal("Name=Žaibas\nCount=-42\n", Encoding.UTF8.GetString(output.CStr()));
        Assert.Equal("Žaibas", IniHelpers.ReadStringFromIni("Name=Žaibas", "Name"));
        Assert.Equal(-42, IniHelpers.ReadIntFromIni("Count=-42", "Count"));
    }

    [Fact]
    public void Missing_or_malformed_ini_values_are_reported_like_rbfx()
    {
        Assert.Null(IniHelpers.ReadStringFromIni("Other=value", "Name"));
        Assert.Null(IniHelpers.ReadStringFromIni("Name", "Name"));
        Assert.Null(IniHelpers.ReadIntFromIni("Other=12", "Count"));
        Assert.Equal(0, IniHelpers.ReadIntFromIni("Count=invalid", "Count"));
    }
}
