namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Utility;

public sealed class AssetTransformerTests
{
    [Fact]
    public void Input_copy_constructor_and_base64_round_trip_preserve_independent_values()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var flavor = new ApplicationFlavor("platform=linux,desktop;quality=high");
            var original = new AssetTransformerInput(
                true, flavor, "Models/Box.glb", "/project/Data/Models/Box.glb", 1234)
            {
                TempPath = "/old-temp/",
                OutputFileName = "/old-output/Box.glb",
                OutputResourceName = "Old/Box.glb",
            };
            var nested = new AssetTransformerInput(
                original, "/temp/", "/project/Cache/Box.glb", "Models/Box.glb");

            original.Flavor.Components["quality"].Add("ultra");
            original.ResourceName = "Changed.glb";

            Assert.True(nested.IsPostTransform);
            Assert.Equal("Models/Box.glb", nested.OriginalResourceName);
            Assert.Equal("Models/Box.glb", nested.ResourceName);
            Assert.Equal("/project/Data/Models/Box.glb", nested.InputFileName);
            Assert.Equal(1234u, nested.InputFileTime);
            Assert.Equal("/temp/", nested.TempPath);
            Assert.Equal("/project/Cache/Box.glb", nested.OutputFileName);
            Assert.Equal("Models/Box.glb", nested.OutputResourceName);
            Assert.DoesNotContain("ultra", nested.Flavor.Components["quality"]);

            var roundTrip = AssetTransformerInput.FromBase64(nested.ToBase64());

            Assert.Equal(nested.IsPostTransform, roundTrip.IsPostTransform);
            Assert.Equal(nested.Flavor.ToString(), roundTrip.Flavor.ToString());
            Assert.Equal(nested.OriginalResourceName, roundTrip.OriginalResourceName);
            Assert.Equal(nested.OriginalInputFileName, roundTrip.OriginalInputFileName);
            Assert.Equal(nested.ResourceName, roundTrip.ResourceName);
            Assert.Equal(nested.InputFileName, roundTrip.InputFileName);
            Assert.Equal(nested.InputFileTime, roundTrip.InputFileTime);
            Assert.Equal(nested.TempPath, roundTrip.TempPath);
            Assert.Equal(nested.OutputFileName, roundTrip.OutputFileName);
            Assert.Equal(nested.OutputResourceName, roundTrip.OutputResourceName);

            var clone = nested.Clone();
            nested.Flavor.Components["quality"].Add("medium");
            Assert.DoesNotContain("medium", clone.Flavor.Components["quality"]);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
