// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.Sdl;

public sealed class SdlOwnedStringTests
{
    [Fact]
    public void Base_path_is_returned_as_a_copied_string_and_freed_natively()
    {
        var basePath = Sdl.SdlGetBasePath();

        Assert.False(string.IsNullOrEmpty(basePath));
    }
}
