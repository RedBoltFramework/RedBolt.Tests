// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;
using System.Text.Json.Nodes;

using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GlowAssetPipelineCompatibilityTests
{
    [Fact]
    public void Legacy_assetpipeline_fields_are_preserved_on_the_companion_transformer()
    {
        var directory = CreateTempDirectory();
        try
        {
            var fileName = Path.Combine(directory, "Models.assetpipeline");
            File.WriteAllText(fileName, """
                {
                  "Transformers": [
                    {
                      "_Class": "ModelImporter",
                      "Repair Looping": true,
                      "LightMap UV: Generate": true,
                      "LightMap UV: Texels per Unit": 24.5,
                      "LightMap UV: Channel": 2
                    }
                  ]
                }
                """);

            Assert.True(LegacyGlowAssetPipelineMigrator.TryMigrateFile(fileName));

            var transformers = Assert.IsType<JsonArray>(JsonNode.Parse(File.ReadAllText(fileName))!["Transformers"]);
            Assert.Equal(2, transformers.Count);
            var modelImporter = Assert.IsType<JsonObject>(transformers[0]);
            Assert.Equal("ModelImporter", modelImporter["_Class"]!.GetValue<string>());
            Assert.True(modelImporter["Repair Looping"]!.GetValue<bool>());
            Assert.False(modelImporter.ContainsKey(LegacyGlowAssetPipelineMigrator.GenerateProperty));
            Assert.False(modelImporter.ContainsKey(LegacyGlowAssetPipelineMigrator.TexelsPerUnitProperty));
            Assert.False(modelImporter.ContainsKey(LegacyGlowAssetPipelineMigrator.UVChannelProperty));

            var glow = Assert.IsType<JsonObject>(transformers[1]);
            Assert.Equal("GenerateLightmapUVTransformer", glow["_Class"]!.GetValue<string>());
            Assert.True(glow[LegacyGlowAssetPipelineMigrator.GenerateProperty]!.GetValue<bool>());
            Assert.Equal(24.5f, glow[LegacyGlowAssetPipelineMigrator.TexelsPerUnitProperty]!.GetValue<float>());
            Assert.Equal(2u, glow[LegacyGlowAssetPipelineMigrator.UVChannelProperty]!.GetValue<uint>());
            Assert.Equal(4, glow.Count);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Legacy_assetpipeline_without_explicit_defaults_still_supports_ModelImporter_json()
    {
        var directory = CreateTempDirectory();
        try
        {
            var fileName = Path.Combine(directory, "Models.AssetPipeline.json");
            File.WriteAllText(fileName, """
                { "Transformers": [ { "_Class": "ModelImporter" } ] }
                """);

            Assert.Equal(1, LegacyGlowAssetPipelineMigrator.MigrateDirectory(directory));

            var transformers = Assert.IsType<JsonArray>(JsonNode.Parse(File.ReadAllText(fileName))!["Transformers"]);
            Assert.Equal("ModelImporter", transformers[0]!["_Class"]!.GetValue<string>());
            Assert.Equal("GenerateLightmapUVTransformer", transformers[1]!["_Class"]!.GetValue<string>());
            Assert.False(LegacyGlowAssetPipelineMigrator.TryMigrateFile(fileName));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Existing_ModelImporter_json_fields_override_assetpipeline_defaults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var modelFileName = Path.Combine(directory, "Model.glb");
            var parametersDirectory = modelFileName + ".d";
            Directory.CreateDirectory(parametersDirectory);
            File.WriteAllText(Path.Combine(parametersDirectory, "ModelImporter.json"), """
                {
                  "lightmapUVGenerate": true,
                  "lightmapUVTexelsPerUnit": 31.25,
                  "lightmapUVChannel": 3
                }
                """);

            using var transformer = new GenerateLightmapUVTransformer();
            var input = new AssetTransformerInput
            {
                OriginalResourceName = "Model.glb",
                OriginalInputFileName = modelFileName,
                ResourceName = "Model.mdl",
                InputFileName = Path.Combine(directory, "Model.mdl"),
            };
            var method = typeof(GenerateLightmapUVTransformer).GetMethod(
                "LoadParameters", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var parameters = method.Invoke(transformer, [input]);
            Assert.NotNull(parameters);
            var parametersType = parameters.GetType();

            Assert.True((bool)parametersType.GetField("Generate")!.GetValue(parameters)!);
            Assert.Equal(31.25f, parametersType.GetField("TexelsPerUnit")!.GetValue(parameters));
            Assert.Equal(3u, parametersType.GetField("UVChannel")!.GetValue(parameters));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RedBoltGlowCompatibility-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
