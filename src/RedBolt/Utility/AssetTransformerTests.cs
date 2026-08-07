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

    [Fact]
    public void Output_clone_and_base64_round_trip_preserve_independent_collections()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var output = new AssetTransformerOutput
            {
                SourceModified = true,
                OutputResourceNames = ["Models/Box.mdl", "Materials/Box.xml"],
                ModifiedResourceNames = new HashSet<string>(["Models/Source.glb"], StringComparer.Ordinal),
                AppliedTransformers = new HashSet<string>(["ModelImporter"], StringComparer.Ordinal),
                DependencyModificationTimes = new Dictionary<string, uint>(StringComparer.Ordinal)
                {
                    ["Textures/Box.png"] = 4321,
                },
            };

            var clone = output.Clone();
            output.OutputResourceNames.Add("Changed");
            output.ModifiedResourceNames.Add("Changed");
            output.AppliedTransformers.Add("Changed");
            output.DependencyModificationTimes["Changed"] = 1;

            Assert.Equal(["Models/Box.mdl", "Materials/Box.xml"], clone.OutputResourceNames);
            Assert.Equal(["Models/Source.glb"], clone.ModifiedResourceNames);
            Assert.Equal(["ModelImporter"], clone.AppliedTransformers);
            Assert.Equal(4321u, clone.DependencyModificationTimes["Textures/Box.png"]);
            Assert.False(clone.DependencyModificationTimes.ContainsKey("Changed"));

            var roundTrip = AssetTransformerOutput.FromBase64(clone.ToBase64());

            Assert.True(roundTrip.SourceModified);
            Assert.Equal(clone.OutputResourceNames, roundTrip.OutputResourceNames);
            Assert.Equal(clone.ModifiedResourceNames, roundTrip.ModifiedResourceNames);
            Assert.Equal(clone.AppliedTransformers, roundTrip.AppliedTransformers);
            Assert.Equal(clone.DependencyModificationTimes, roundTrip.DependencyModificationTimes);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
