// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.Utility;

public sealed class BaseAssetPostTransformerTests
{
    [Fact]
    public void Post_transformer_applies_loads_parameters_and_expands_resource_patterns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltBaseAssetPostTransformer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "Animations"));
        System.IO.File.WriteAllText(Path.Combine(directory, "Animations", "Walk.ani"), string.Empty);
        System.IO.File.WriteAllText(Path.Combine(directory, "Animations", "Run.ANI"), string.Empty);
        System.IO.File.WriteAllText(Path.Combine(directory, "Animations", "Ignore.txt"), string.Empty);
        System.IO.File.WriteAllText(Path.Combine(directory, "parameters.json"), """
            {
                "Value": 42
            }
            """);

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountDir(directory);
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(new ResourceCache());

            using var transformer = new TestPostTransformer();
            var input = new AssetTransformerInput
            {
                ResourceName = "Assets/Process.PARAMS.JSON",
            };

            Assert.True(transformer.IsApplicable(input));
            Assert.True(transformer.IsPostTransform());
            Assert.Equal(42, transformer.LoadTestParameters("parameters.json").Value);

            var matches = transformer.GetMatches("Animations", "*.ani");
            Assert.Equal(["Run.ANI", "Walk.ani"], matches.Select(static match => match.FileName).Order());
            Assert.Equal(["Run", "Walk"], matches.Select(static match => match.Match).Order());
            var runMatch = Assert.Single(matches, static match => match.Match == "Run");
            Assert.Equal("Output/Run.processed.ani", transformer.Expand("Output/{}.processed.ani", runMatch));

            input.ResourceName = "Assets/Process.json";
            Assert.False(transformer.IsApplicable(input));
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }

    private sealed class TestPostTransformer : BaseAssetPostTransformer
    {
        public override string GetParametersFileName() => ".params.json";

        public TestParameters LoadTestParameters(string fileName) => LoadParameters<TestParameters>(fileName);

        public IReadOnlyList<TestPatternMatch> GetMatches(string baseResourceName, string pattern)
        {
            return GetResourcesByPattern(baseResourceName, pattern)
                .Select(static match => new TestPatternMatch(match.FileName, match.Match))
                .ToArray();
        }

        public string Expand(string template, TestPatternMatch match)
        {
            var value = new PatternMatch
            {
                FileName = match.FileName,
                Match = match.Match,
            };
            return GetMatchFileName(template, value);
        }
    }

    public sealed class TestParameters : IArchiveSerializable
    {
        public int Value;

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeValue(archive, "Value", ref Value);
        }
    }

    private readonly record struct TestPatternMatch(string FileName, string Match);
}
