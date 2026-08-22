// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Utility;

public class IconTests
{
    [Fact]
    public void Font_awesome_constants_are_available_from_core()
    {
        Assert.Same(typeof(Context).Assembly, typeof(Icons).Assembly);
        Assert.Equal("fa-regular-400.ttf", Icons.FontIconFileNameFAR);
        Assert.Equal("fa-solid-900.ttf", Icons.FontIconFileNameFAS);
        Assert.Equal("\uf07b", Icons.Folder);
        Assert.Equal(0xe005, Icons.IconMin);
        Assert.Equal(0xf8ff, Icons.IconMax16);
    }
}
