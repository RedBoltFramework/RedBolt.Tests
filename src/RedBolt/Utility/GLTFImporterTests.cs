namespace RedBolt.Tests;

using System.Buffers.Binary;
using System.Text;

using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Utility;

public sealed class GLTFImporterTests
{
    private const string TriangleBuffer = "AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA";

    [Fact]
    public void Importer_loads_and_processes_indexed_static_mesh()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_loads_and_processes_indexed_static_mesh));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("triangle.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings { AssetName = "Triangle" });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var model = Assert.IsType<ModelView>(importer.ImportedModel);
        Assert.Equal("Triangle", model.GetName());
        var geometry = Assert.Single(model.GetGeometries());
        var lod = Assert.Single(geometry.Lods);
        Assert.Equal(PrimitiveType.TriangleList, lod.PrimitiveType);
        Assert.Equal([0u, 1u, 2u], lod.Indices);
        Assert.Equal(new Vector3f(1, 0, 0), lod.Vertices[1].GetPosition());
        Assert.Equal(new Vector3f(0, 1, 0), lod.Vertices[2].GetPosition());
    }

    [Fact]
    public void Importer_reads_normalized_attributes_and_tangents()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reads_normalized_attributes_and_tangents));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 3); AppendSingle(data, 4);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        data.AddRange([127, 0, 0, 0, 127, 0, 0, 0, 127]);
        foreach (var tangent in new[] { 1f, 0f, 0f, -1f, 0f, 1f, 0f, -1f, 0f, 0f, 1f, -1f })
            AppendSingle(data, tangent);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 9 },
                { "buffer": 0, "byteOffset": 45, "byteLength": 48 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5120, "normalized": true, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC4" }
              ],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0, "NORMAL": 1, "TANGENT": 2 } }] }]
            }
            """;
        var documentPath = artifacts.Write("attributes.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var lod = Assert.Single(Assert.Single(importer.ImportedModel!.GetGeometries()).Lods);
        Assert.Equal(Vector3f.Zero, lod.Vertices[0].GetPosition());
        Assert.Equal(new Vector3f(2, 3, 4), lod.Vertices[1].GetPosition());
        Assert.Equal(new Vector3f(0, 1, 0), lod.Vertices[1].GetNormal());
        Assert.Equal(new Vector4f(1, 0, 0, -1), lod.Vertices[0].Tangent);
    }

    [Fact]
    public void Importer_rejects_processing_without_a_primary_document()
    {
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("Primary source model is not loaded.", importer.LastError);
    }

    [Fact]
    public void Importer_loads_binary_glb_from_memory()
    {
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFileBinary(CreateEmptyGlb()), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Empty(importer.ImportedModel!.GetGeometries());
    }

    [Fact]
    public void Importer_copies_settings_and_renames_nodes_when_loading()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_copies_settings_and_renames_nodes_when_loading));
        var settings = new GLTFImporterSettings { NodeRenames = new Dictionary<string, string> { ["Node"] = "Renamed" } };
        using var importer = new GLTFImporter(settings);
        settings.NodeRenames["Node"] = "Mutated";
        var documentPath = artifacts.Write("nodes.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "nodes": [{ "name": "Node" }] }
            """));

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        using var nodes = importer.SourceModel!.Nodes;
        using var node = nodes[0];
        using var name = node.Name;
        Assert.Equal("Renamed", name.ToString());
    }

    [Fact]
    public void Importer_merges_secondary_animation_with_requested_name()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_merges_secondary_animation_with_requested_name));
        var primary = artifacts.Write("primary.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "nodes": [{ "name": "Node" }] }
            """));
        var secondary = artifacts.Write("secondary.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "animations": [{ "name": "Original" }] }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(primary), importer.LastError);
        Assert.True(importer.MergeFile(secondary, "Merged"), importer.LastError);
        using var animations = importer.SourceModel!.Animations;
        using var animation = Assert.Single(animations);
        using var name = animation.Name;
        Assert.Equal("Merged", name.ToString());
    }

    [Fact]
    public void Importer_settings_roundtrip_node_renames_and_engine_epsilon()
    {
        var source = new GLTFImporterSettings { NodeRenames = new Dictionary<string, string> { ["Before"] = "After" } };
        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("settings"))
            source.SerializeInBlock(output);
        using var input = new TestJSONInputArchive(output.ToJsonString());
        var result = new GLTFImporterSettings();
        using (input.OpenUnorderedBlock("settings"))
            result.SerializeInBlock(input);

        Assert.Equal(MathDefs.Epsilon, result.KeyFrameTimeError);
        Assert.Equal("After", result.NodeRenames["Before"]);
    }

    [Fact]
    public void Importer_converts_processed_transforms_with_inline_scale_and_mirror()
    {
        using var importer = new GLTFImporter(new GLTFImporterSettings
        {
            MirrorX = true,
            Scale = 2.0f,
            Rotation = new Quaternion(45.0f, Vector3f.Up)
        });
        Assert.True(importer.LoadFileBinary(CreateEmptyGlb()), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var result = importer.ConvertTransform(new Transform(new Vector3f(1, 2, 3), new Quaternion(1, 2, 3, 4), Vector3f.One));

        Assert.Equal(new Vector3f(-2, 4, 6), result.Position);
        Assert.Equal(new Quaternion(1, 2, -3, -4), result.Rotation);
        Assert.Equal(Vector3f.One, result.Scale);
    }

    private static byte[] CreateEmptyGlb()
    {
        var json = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}}");
        var paddedLength = (json.Length + 3) & ~3;
        var result = new byte[12 + 8 + paddedLength];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), checked((uint)result.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), checked((uint)paddedLength));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), 0x4E4F534A);
        json.CopyTo(result, 20);
        Array.Fill(result, (byte)' ', 20 + json.Length, paddedLength - json.Length);
        return result;
    }

    private static void AppendSingle(List<byte> bytes, float value) => bytes.AddRange(BitConverter.GetBytes(value));
}
