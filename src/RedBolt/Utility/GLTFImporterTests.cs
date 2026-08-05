namespace RedBolt.Tests;

using System.Buffers.Binary;
using System.Text;

using RedBolt.Graphics;
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
}
