// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Text;

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
        Assert.Equal(Icons.Folder, char.ConvertFromUtf32(0xf07b));
        Assert.Equal(Icons.ArrowsLeftRightToLine, char.ConvertFromUtf32(0xe4ba));
        Assert.Equal(Icons.House, char.ConvertFromUtf32(0xf015));
        Assert.Equal(0xe005, Icons.IconMin);
        Assert.Equal(0xf8ff, Icons.IconMax16);
    }

    [Fact]
    public void Font_awesome_constants_have_generated_utf8_aliases()
    {
        Assert.Equal(Encoding.UTF8.GetBytes(Icons.Folder), Icons.FolderU8.ToArray());
        Assert.Equal(
            Encoding.UTF8.GetBytes(Icons.ArrowsLeftRightToLine),
            Icons.ArrowsLeftRightToLineU8.ToArray());
        Assert.Equal(
            Encoding.UTF8.GetBytes(Icons.FontIconFileNameFAS),
            Icons.FontIconFileNameFASU8.ToArray());
    }
}
