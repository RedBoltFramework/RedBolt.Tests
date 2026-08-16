// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Text;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Utility;

public sealed class AssetPipelineTests
{
    [Theory]
    [InlineData("Default.assetpipeline", true)]
    [InlineData("Default.ASSETPIPELINE", true)]
    [InlineData("Default.AssetPipeline.json", true)]
    [InlineData("Default.assetpipeline.JSON", true)]
    [InlineData("Default.json", false)]
    public void CheckExtension_matches_pipeline_extensions(string fileName, bool expected)
    {
        Assert.Equal(expected, AssetPipeline.CheckExtension(fileName));
    }

    [Fact]
    public void Pipeline_loads_mutates_saves_and_clears_archive_values()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            context.RegisterAssembly(typeof(AssetPipeline).Assembly);
            using var reflectedPipeline = Assert.IsType<AssetPipeline>(
                context.CreateObject(new StringHash(nameof(AssetPipeline))));
            using var pipeline = new AssetPipeline();
            using var source = new MemoryBuffer(
                """
                {
                    "Transformers": [
                        { "_Class": "UnknownTransformer" },
                        { "_Class": "AssetTransformer" }
                    ],
                    "Dependencies": [
                        { "Class": "AssetTransformerB", "DependsOn": "AssetTransformerA" }
                    ]
                }
                """);

            Assert.True(pipeline.Load(source));
            var loadedTransformer = Assert.IsType<AssetTransformer>(Assert.Single(pipeline.GetTransformers()));
            var dependency = Assert.Single(pipeline.GetDependencies());
            Assert.Equal("AssetTransformerB", dependency.Class);
            Assert.Equal("AssetTransformerA", dependency.DependsOn);

            using var addedA = new AssetTransformer();
            using var addedB = new AssetTransformer();
            pipeline.AddTransformer(addedA);
            pipeline.AddTransformer(addedB);
            pipeline.ReorderTransformer(addedB, 0);
            Assert.Same(addedB, pipeline.GetTransformers()[0]);
            Assert.Same(loadedTransformer, pipeline.GetTransformers()[1]);
            Assert.Same(addedA, pipeline.GetTransformers()[2]);

            pipeline.RemoveTransformer(addedA);
            pipeline.RemoveTransformer(addedA);
            pipeline.ReorderTransformer(loadedTransformer, pipeline.GetTransformers().Count);
            Assert.Equal(2, pipeline.GetTransformers().Count);
            Assert.Same(addedB, pipeline.GetTransformers()[0]);
            Assert.Same(loadedTransformer, pipeline.GetTransformers()[1]);

            using var destination = new VectorBuffer();
            Assert.True(pipeline.Save(destination));
            var json = Encoding.UTF8.GetString(destination.Buffer);
            Assert.Contains("\"Transformers\"", json, StringComparison.Ordinal);
            Assert.Contains("\"_Class\": \"AssetTransformer\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Dependencies\"", json, StringComparison.Ordinal);

            using var restored = new AssetPipeline();
            using var savedSource = new MemoryBuffer(destination.Buffer.ToArray());
            Assert.True(restored.Load(savedSource));
            Assert.Equal(2, restored.GetTransformers().Count);
            Assert.All(restored.GetTransformers(), item => Assert.IsType<AssetTransformer>(item));
            var restoredDependency = Assert.Single(restored.GetDependencies());
            Assert.Equal(dependency.Class, restoredDependency.Class);
            Assert.Equal(dependency.DependsOn, restoredDependency.DependsOn);

            restored.AddTransformer(null);
            using var empty = new MemoryBuffer("{}");
            Assert.True(restored.Load(empty));
            Assert.Empty(restored.GetTransformers());
            Assert.Empty(restored.GetDependencies());

            using var emptyDestination = new VectorBuffer();
            Assert.True(restored.Save(emptyDestination));
            var emptyJson = Encoding.UTF8.GetString(emptyDestination.Buffer);
            Assert.DoesNotContain("Transformers", emptyJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Dependencies", emptyJson, StringComparison.Ordinal);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
