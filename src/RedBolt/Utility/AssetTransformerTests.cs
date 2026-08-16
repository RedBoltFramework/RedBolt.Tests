// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.IO;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
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

    [Fact]
    public void Dependency_clone_and_archive_round_trip_preserve_values()
    {
        var source = new AssetTransformerDependency
        {
            Class = "TextureTransformer",
            DependsOn = "ImageTransformer",
        };
        var clone = source.Clone();

        source.Class = "Changed";
        source.DependsOn = "Changed";

        Assert.NotSame(source, clone);
        Assert.Equal("TextureTransformer", clone.Class);
        Assert.Equal("ImageTransformer", clone.DependsOn);

        using var output = new Base64OutputArchive();
        using (output.OpenUnorderedBlock("dependency"))
            clone.SerializeInBlock(output);

        using var input = new Base64InputArchive(output.GetBase64());
        var roundTrip = new AssetTransformerDependency();
        using (input.OpenUnorderedBlock("dependency"))
            roundTrip.SerializeInBlock(input);

        Assert.Equal(clone.Class, roundTrip.Class);
        Assert.Equal(clone.DependsOn, roundTrip.DependsOn);
    }

    [Fact]
    public void Transformer_filters_executes_and_stops_in_order()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            context.RegisterAssembly(typeof(AssetTransformer).Assembly);
            using var transformer = new AssetTransformer();
            using var reflectedTransformer = Assert.IsType<AssetTransformer>(
                context.CreateObject(new StringHash(nameof(AssetTransformer))));
            Assert.False(transformer.IsApplicable(new AssetTransformerInput()));
            Assert.False(transformer.Execute(
                new AssetTransformerInput(), new AssetTransformerOutput(), [transformer]));
            Assert.True(transformer.IsSingleInstanced());
            Assert.False(transformer.IsExecutedOnOutput());
            Assert.False(transformer.IsPostTransform());

            var flavor = new ApplicationFlavorPattern("platform=desktop,linux");
            transformer.SetFlavor(flavor);
            flavor.Components["platform"].Add("changed");
            Assert.DoesNotContain("changed", transformer.GetFlavor().Components["platform"]);

            using var notApplicable = new TrackingTransformerA();
            using var applicable = new TrackingTransformerB { Applicable = true };
            Assert.True(AssetTransformer.IsApplicable(
                new AssetTransformerInput(), [notApplicable, applicable]));
            Assert.Equal(1, notApplicable.ApplicableCalls);
            Assert.Equal(1, applicable.ApplicableCalls);

            using var skipped = new TrackingTransformerA { Applicable = true };
            using var applied = new TrackingTransformerB
            {
                Applicable = true,
                ExecuteOnOutput = true,
            };
            using var failed = new TrackingTransformerC
            {
                Applicable = true,
                ExecuteOnOutput = true,
                ExecuteResult = false,
            };
            var output = new AssetTransformerOutput();

            Assert.False(AssetTransformer.ExecuteTransformers(
                new AssetTransformerInput(), output, [skipped, applied, failed], true));
            Assert.Equal(0, skipped.ApplicableCalls);
            Assert.Equal(0, skipped.ExecuteCalls);
            Assert.Equal(1, applied.ExecuteCalls);
            Assert.Equal(1, failed.ExecuteCalls);
            Assert.Contains(nameof(TrackingTransformerB), output.AppliedTransformers);
            Assert.DoesNotContain(nameof(TrackingTransformerC), output.AppliedTransformers);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Transformer_executes_copies_outputs_and_removes_temporary_directory()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var rootPath = Path.Combine(Path.GetTempPath(), $"RedBolt.AssetTransformer.{Guid.NewGuid():N}");

        try
        {
            var temporaryPath = Path.Combine(rootPath, "temporary");
            var outputPath = Path.Combine(rootPath, "cache") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(Path.Combine(outputPath, "Models"));
            System.IO.File.WriteAllText(Path.Combine(outputPath, "Models", "Box.mdl"), "old");
            var input = new AssetTransformerInput
            {
                TempPath = temporaryPath,
            };
            var output = new AssetTransformerOutput();
            using var transformer = new FileWritingTransformer();

            Assert.True(AssetTransformer.ExecuteTransformersAndStore(
                input, outputPath, output, [transformer]));

            Assert.Equal("model", System.IO.File.ReadAllText(Path.Combine(outputPath, "Models", "Box.mdl")));
            Assert.False(System.IO.File.Exists(Path.Combine(outputPath, ".hidden")));
            Assert.Equal(["Models/Box.mdl"], output.OutputResourceNames);
            Assert.Contains(nameof(FileWritingTransformer), output.AppliedTransformers);
            Assert.False(Directory.Exists(temporaryPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Transformer_adds_dependencies_relative_to_original_data_folder()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var rootPath = Path.Combine(Path.GetTempPath(), $"RedBolt.AssetDependency.{Guid.NewGuid():N}");

        try
        {
            var dataPath = Path.Combine(rootPath, "Data");
            var inputFileName = Path.Combine(dataPath, "Models", "Box.glb");
            var dependencyFileName = Path.Combine(dataPath, "Textures", "Box.png");
            Directory.CreateDirectory(Path.GetDirectoryName(dependencyFileName)!);
            System.IO.File.WriteAllText(dependencyFileName, "texture");
            var expectedTime = DateTimeOffset.UtcNow.AddMinutes(-1).UtcDateTime;
            System.IO.File.SetLastWriteTimeUtc(dependencyFileName, expectedTime);

            var input = new AssetTransformerInput
            {
                OriginalResourceName = Path.Combine("Models", "Box.glb"),
                OriginalInputFileName = inputFileName,
            };
            var output = new AssetTransformerOutput();
            using var transformer = new ExposedDependencyTransformer();

            transformer.Add(input, output, dependencyFileName);
            transformer.Add(input, output, Path.Combine(rootPath, "Outside.png"));

            var actualTime = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(dependencyFileName))
                .ToUnixTimeSeconds();
            Assert.Equal(unchecked((uint)actualTime),
                output.DependencyModificationTimes[Path.Combine("Textures", "Box.png")]);
            Assert.Single(output.DependencyModificationTimes);
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
            Context.SetInstance(previousContext);
        }
    }

    private class TrackingTransformer : AssetTransformer
    {
        public bool Applicable;
        public bool ExecuteOnOutput;
        public bool ExecuteResult = true;
        public int ApplicableCalls;
        public int ExecuteCalls;

        public override bool IsApplicable(AssetTransformerInput input)
        {
            ++ApplicableCalls;
            return Applicable;
        }

        public override bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
            IReadOnlyList<AssetTransformer> transformers)
        {
            ++ExecuteCalls;
            return ExecuteResult;
        }

        public override bool IsExecutedOnOutput() => ExecuteOnOutput;
    }

    private sealed class TrackingTransformerA : TrackingTransformer;

    private sealed class TrackingTransformerB : TrackingTransformer;

    private sealed class TrackingTransformerC : TrackingTransformer;

    private sealed class FileWritingTransformer : AssetTransformer
    {
        public override bool IsApplicable(AssetTransformerInput input) => true;

        public override bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
            IReadOnlyList<AssetTransformer> transformers)
        {
            var modelPath = Path.Combine(input.TempPath, "Models", "Box.mdl");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            System.IO.File.WriteAllText(modelPath, "model");
            System.IO.File.WriteAllText(Path.Combine(input.TempPath, ".hidden"), "hidden");
            return true;
        }
    }

    private sealed class ExposedDependencyTransformer : AssetTransformer
    {
        public void Add(AssetTransformerInput input, AssetTransformerOutput output, string fileName)
        {
            AddDependency(input, output, fileName);
        }
    }
}
