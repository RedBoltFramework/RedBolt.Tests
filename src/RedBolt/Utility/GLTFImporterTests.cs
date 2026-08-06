namespace RedBolt.Tests;

using System.Buffers.Binary;
using System.Text;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(GLTFImporterCollection))]
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
        Assert.Equal(new Vector3f(0, 0, 1), lod.Vertices[0].GetNormal());
    }

    [Fact]
    public void Importer_converts_line_loop_to_closed_line_strip()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_converts_line_loop_to_closed_line_strip));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "primitives": [{ "mode": 2, "attributes": { "POSITION": 0 }, "indices": 1 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("line-loop.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        var lod = Assert.Single(Assert.Single(importer.ImportedModel!.GetGeometries()).Lods);
        Assert.Equal(PrimitiveType.LineStrip, lod.PrimitiveType);
        Assert.Equal([0u, 1u, 2u, 0u], lod.Indices);
    }

    [Fact]
    public void Importer_repairs_skinned_vertex_weights()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_repairs_skinned_vertex_weights));
        var data = new List<byte>();
        data.AddRange(Convert.FromBase64String(TriangleBuffer));
        data.AddRange([0, 4, 0, 0, 0, 4, 0, 0, 0, 4, 0, 0, 0, 0]);
        for (var vertexIndex = 0; vertexIndex < 3; ++vertexIndex)
        {
            AppendSingle(data, 0.25f); AppendSingle(data, 0.5f);
            AppendSingle(data, 0); AppendSingle(data, 0);
        }
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }, { "buffer": 0, "byteOffset": 42, "byteLength": 12 }, { "buffer": 0, "byteOffset": 56, "byteLength": 48 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }, { "bufferView": 2, "componentType": 5121, "count": 3, "type": "VEC4" }, { "bufferView": 3, "componentType": 5126, "count": 3, "type": "VEC4" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0, "JOINTS_0": 2, "WEIGHTS_0": 3 }, "indices": 1 }] }],
              "nodes": [{ "name": "Root", "children": [1, 2] }, { "name": "Joint" }, { "name": "Mesh", "mesh": 0, "skin": 0 }],
              "skins": [{ "skeleton": 0, "joints": [1] }]
            }
            """;
        var documentPath = artifacts.Write("repaired-weights.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        var vertex = Assert.Single(Assert.Single(importer.ImportedModel!.GetGeometries()).Lods).Vertices[0];
        Assert.Equal(Vector4f.Zero, vertex.BlendIndices);
        Assert.Equal(new Vector4f(1, 0, 0, 0), vertex.BlendWeights);
    }

    [Fact]
    public void Importer_rejects_invalid_primitive_material()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_invalid_primitive_material));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("invalid-material.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("glTF primitive references an invalid material.", importer.LastError);
    }

    [Fact]
    public void Importer_rejects_malformed_node_translation()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_malformed_node_translation));
        var documentPath = artifacts.Write("invalid-transform.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "nodes": [{ "translation": [1, 2] }],
              "scenes": [{ "nodes": [0] }]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("glTF node translation does not contain 3 values.", importer.LastError);
    }

    [Fact]
    public void Importer_rejects_mismatched_mesh_morph_weights()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_mismatched_mesh_morph_weights));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "weights": [0.25], "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "targets": [{}, {}] }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("invalid-morph-weights.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("glTF mesh morph weight count does not match its targets.", importer.LastError);
    }

    [Fact]
    public void Importer_reads_morph_target_deltas_and_default_weight()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reads_morph_target_deltas_and_default_weight));
        var data = new byte[78];
        Convert.FromBase64String(TriangleBuffer).CopyTo(data, 0);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(42), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(46), 2.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(50), 3.0f);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 78 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 42, "byteLength": 36 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" }],
              "meshes": [{ "weights": [0.25], "primitives": [{ "attributes": { "POSITION": 0 }, "targets": [{ "POSITION": 1 }] }] }],
              "nodes": [{ "name": "Morph", "mesh": 0, "weights": [0.5] }],
              "scenes": [{ "nodes": [0] }]
            }
            """.Replace("$BUFFER$", Convert.ToBase64String(data), StringComparison.Ordinal);
        var documentPath = artifacts.Write("morph.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);

        var model = Assert.IsType<ModelView>(importer.ImportedModel);
        Assert.Equal(0.25f, Assert.Single(model.GetMorphs()).InitialWeight);
        var morph = Assert.Single(Assert.Single(Assert.Single(model.GetGeometries()).Lods).Morphs[0]);
        Assert.Equal(0u, morph.Index);
        Assert.Equal(new Vector3f(1, 2, 3), morph.PositionDelta);
        Assert.True(importer.SaveResources(), importer.LastError);
        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        Assert.Equal(nameof(AnimatedModel), Assert.Single(prefab.GetNodePrefab().Components).TypeName);
    }

    [Fact]
    public void Importer_cooks_morph_weight_animation_and_controller()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_morph_weight_animation_and_controller));
        var data = new byte[88];
        Convert.FromBase64String(TriangleBuffer).CopyTo(data, 0);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(42), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(46), 2.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(50), 3.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(72), 0.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(76), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(80), 0.25f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(84), 0.75f);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 88 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 42, "byteLength": 36 }, { "buffer": 0, "byteOffset": 72, "byteLength": 8 }, { "buffer": 0, "byteOffset": 80, "byteLength": 8 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 2, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 3, "componentType": 5126, "count": 2, "type": "SCALAR" }],
              "meshes": [{ "weights": [0.0], "primitives": [{ "attributes": { "POSITION": 0 }, "targets": [{ "POSITION": 1 }] }] }],
              "nodes": [{ "name": "Morph", "mesh": 0 }],
              "animations": [{ "samplers": [{ "input": 2, "output": 3, "interpolation": "LINEAR" }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "weights" } }] }],
              "scenes": [{ "nodes": [0] }]
            }
            """.Replace("$BUFFER$", Convert.ToBase64String(data), StringComparison.Ordinal);
        var documentPath = artifacts.Write("morph-animation.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        var animation = Assert.Single(importer.ImportedAnimations);
        var track = Assert.Single(animation.VariantTracks.Values);
        Assert.Equal("Morph/@AnimatedModel/Morphs/0", track.Name);
        Assert.Equal([0.25f, 0.75f], track.KeyFrames.Select(keyFrame => keyFrame.Value.GetFloat()));
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        Assert.Contains(prefab.GetNodePrefab().Components, component => component.TypeName == nameof(AnimationController));
    }

    [Fact]
    public void Importer_reads_sparse_accessors()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reads_sparse_accessors));
        var sparseData = new byte[49];
        sparseData[36] = 1;
        BinaryPrimitives.WriteSingleLittleEndian(sparseData.AsSpan(37), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(sparseData.AsSpan(41), 2.0f);
        BinaryPrimitives.WriteSingleLittleEndian(sparseData.AsSpan(45), 3.0f);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 49 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 1 }, { "buffer": 0, "byteOffset": 37, "byteLength": 12 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "sparse": { "count": 1, "indices": { "bufferView": 1, "componentType": 5121 }, "values": { "bufferView": 2 } } }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 } }] }]
            }
            """.Replace("$BUFFER$", Convert.ToBase64String(sparseData), StringComparison.Ordinal);
        var documentPath = artifacts.Write("sparse.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var vertices = Assert.Single(Assert.Single(importer.ImportedModel!.GetGeometries()).Lods).Vertices;
        Assert.Equal(Vector3f.Zero, vertices[0].GetPosition());
        Assert.Equal(new Vector3f(1, 2, 3), vertices[1].GetPosition());
        Assert.Equal(Vector3f.Zero, vertices[2].GetPosition());
    }

    [Fact]
    public void Importer_creates_artificial_skin_from_callback()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_creates_artificial_skin_from_callback));
        var document = """
            {
              "asset": { "version": "2.0" },
              "nodes": [{ "name": "Rig", "children": [1] }, { "name": "Joint" }],
              "scenes": [{ "nodes": [0] }]
            }
            """;
        var documentPath = artifacts.Write("artificial-skin.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty, new ArtificialSkinCallback()), importer.LastError);

        Assert.Equal(2, importer.ImportedModel!.GetBones().Count);
    }

    [Fact]
    public void Importer_cooks_and_saves_model_resource()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_and_saves_model_resource));
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
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings { AssetName = "Triangle Asset" });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        var saved = Assert.Single(importer.SavedResources);
        Assert.Equal("Imported/Models/Triangle_Asset.mdl", saved.Key);
        Assert.Equal(Path.Combine(outputPath, "Models/Triangle_Asset.mdl"), saved.Value);
        Assert.True(importer.SaveResources(), importer.LastError);

        Assert.Equal("UMD3", Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(saved.Value), 0, 4));
    }

    [Fact]
    public void Importer_cooks_static_model_prefab_component()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_static_model_prefab_component));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "materials": [{ "name": "Paint" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }] }],
              "nodes": [{ "name": "Mesh", "mesh": 0 }],
              "scenes": [{ "nodes": [0] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("scene.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings { AssetName = "Triangle" });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var mesh = prefab.GetNodePrefab();
        Assert.Equal("Triangle", mesh.GetNodeName());
        var staticModel = Assert.Single(mesh.Components);
        Assert.Equal(nameof(StaticModel), staticModel.TypeName);
        var model = Assert.Single(staticModel.Attributes.Where(attribute => attribute.Name == "Model")).Value.GetResourceRef();
        var materials = Assert.Single(staticModel.Attributes.Where(attribute => attribute.Name == "Material")).Value.GetResourceRefList();
        Assert.Equal("Imported/Models/Triangle.mdl", model.Name);
        Assert.Equal(["Imported/Materials/Paint.xml"], materials.Names);
    }

    [Fact]
    public void Importer_cooks_skin_and_animated_prefab_component()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_skin_and_animated_prefab_component));
        var skinBuffer = new byte[170];
        Convert.FromBase64String(TriangleBuffer).CopyTo(skinBuffer, 0);
        for (var index = 0; index < 4; ++index)
            BinaryPrimitives.WriteSingleLittleEndian(skinBuffer.AsSpan(42 + index * 20), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(skinBuffer.AsSpan(90), 9.0f);
        BinaryPrimitives.WriteSingleLittleEndian(skinBuffer.AsSpan(94), 8.0f);
        BinaryPrimitives.WriteSingleLittleEndian(skinBuffer.AsSpan(98), 7.0f);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 170 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }, { "buffer": 0, "byteOffset": 42, "byteLength": 128 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }, { "bufferView": 2, "componentType": 5126, "count": 2, "type": "MAT4" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
              "nodes": [
                { "name": "Root", "translation": [1, 0, 0], "children": [1, 3] },
                { "name": "Pivot", "translation": [0, 2, 0], "children": [2, 4] },
                { "name": "Joint", "translation": [0, 0, 3], "rotation": [0, 0.5, 0, 0.8660254] },
                { "name": "SkinnedMesh", "mesh": 0, "skin": 0 },
                { "name": "Attachment", "children": [5] },
                { "name": "End" }
              ],
              "skins": [{ "inverseBindMatrices": 2, "skeleton": 0, "joints": [2] }],
              "scenes": [{ "nodes": [0] }]
            }
            """.Replace("$BUFFER$", Convert.ToBase64String(skinBuffer), StringComparison.Ordinal);
        var documentPath = artifacts.Write("skin.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings
        {
            AssetName = "Skinned", MirrorX = true, AddEmptyNodesToSkeleton = true
        });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);
        var bones = importer.ImportedModel!.GetBones();
        Assert.Equal(["Joint", "Root", "Pivot", "Attachment", "End"], bones.Select(bone => bone.Name));
        Assert.Equal(2, bones[0].ParentIndex);
        Assert.Equal(-1, bones[1].ParentIndex);
        Assert.Equal(1, bones[2].ParentIndex);
        Assert.Equal(2, bones[3].ParentIndex);
        Assert.Equal(3, bones[4].ParentIndex);
        Assert.Equal(new Vector3f(0, 0, 3), bones[0].Position);
        Assert.Equal(new Vector3f(-1, 0, 0), bones[1].Position);
        Assert.Equal(new Vector3f(0, 2, 0), bones[2].Position);
        Assert.True(bones[0].Rotation.ApproximatelyEquivalent(new Quaternion(0.8660254f, 0, -0.5f, 0), 0.0001f));
        Assert.Equal(new Vector3f(-9, 8, 7), bones[0].OffsetMatrix.Translation);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var skinnedMesh = prefab.GetNodePrefab().Children.Single(node => node.GetNodeName() == "SkinnedMesh");
        Assert.Equal(nameof(AnimatedModel), Assert.Single(skinnedMesh.Components).TypeName);
    }

    [Fact]
    public void Importer_keeps_empty_skinned_models_animating_when_invisible()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_keeps_empty_skinned_models_animating_when_invisible));
        var document = """
            {
              "asset": { "version": "2.0" },
              "meshes": [{ "primitives": [] }],
              "nodes": [
                { "name": "Root", "children": [1, 2] },
                { "name": "Joint" },
                { "name": "EmptySkinnedMesh", "mesh": 0, "skin": 0 }
              ],
              "skins": [{ "skeleton": 0, "joints": [1] }],
              "scenes": [{ "nodes": [0] }]
            }
            """;
        var documentPath = artifacts.Write("empty-skin.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var emptySkinnedMesh = prefab.GetNodePrefab().Children.Single(node => node.GetNodeName() == "EmptySkinnedMesh");
        var animatedModel = Assert.Single(emptySkinnedMesh.Components);
        Assert.Equal(nameof(AnimatedModel), animatedModel.TypeName);
        Assert.Equal(0.0f, animatedModel.Attributes.Single(attribute => attribute.Name == "Animation LOD Bias").Value.GetFloat());
        Assert.True(animatedModel.Attributes.Single(attribute => attribute.Name == "Update Invisible").Value.GetBool());
    }

    [Fact]
    public void Importer_warns_when_declared_skeleton_is_not_a_joint_ancestor()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_warns_when_declared_skeleton_is_not_a_joint_ancestor));
        var documentPath = artifacts.Write("unrelated-skeleton.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "meshes": [{ "primitives": [] }],
              "nodes": [
                { "name": "DeclaredSkeleton" },
                { "name": "JointRoot", "children": [2] },
                { "name": "Joint" },
                { "name": "SkinnedMesh", "mesh": 0, "skin": 0 }
              ],
              "skins": [{ "skeleton": 0, "joints": [2] }],
              "scenes": [{ "nodes": [0, 1, 3] }]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Contains("Skeleton node #0 is not a parent of joint node #2.", importer.LastWarning, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_rejects_invalid_declared_skeleton()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_invalid_declared_skeleton));
        var documentPath = artifacts.Write("invalid-skeleton.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "meshes": [{ "primitives": [] }],
              "nodes": [{ "name": "Joint" }, { "name": "SkinnedMesh", "mesh": 0, "skin": 0 }],
              "skins": [{ "skeleton": 2, "joints": [0] }]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("glTF skin references an invalid skeleton node.", importer.LastError);
    }

    [Fact]
    public void Importer_reuses_equivalent_skins_and_cleans_bone_names()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reuses_equivalent_skins_and_cleans_bone_names));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
              "nodes": [
                { "name": "FirstRoot", "children": [1, 3] },
                { "name": "Rig_Hip", "children": [2] },
                { "name": "Rig_Spine" },
                { "name": "FirstMesh", "mesh": 0, "skin": 0 },
                { "name": "SecondRoot", "children": [5, 7] },
                { "name": "Rig_Hip", "translation": [1, 0, 0], "children": [6] },
                { "name": "Rig_Spine" },
                { "name": "SecondMesh", "mesh": 0, "skin": 1 }
              ],
              "skins": [{ "joints": [1, 2] }, { "joints": [5, 6] }],
              "scenes": [{ "nodes": [0, 4] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("equivalent-skins.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings { CleanupBoneNames = true });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Equal(["Hip", "Spine"], importer.ImportedModel!.GetBones().Select(bone => bone.Name));
        Assert.Single(importer.SavedResources.Keys.Where(name => name.StartsWith("Models/", StringComparison.Ordinal)));
    }

    [Fact]
    public void Importer_cooks_separate_models_for_incompatible_skins()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_separate_models_for_incompatible_skins));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
              "nodes": [
                { "name": "FirstRoot", "children": [1, 3] },
                { "name": "Rig_Hip", "children": [2] },
                { "name": "Rig_Spine" },
                { "name": "FirstMesh", "mesh": 0, "skin": 0 },
                { "name": "SecondRoot", "children": [5, 7] },
                { "name": "Other_Hip", "children": [6] },
                { "name": "Other_Spine" },
                { "name": "SecondMesh", "mesh": 0, "skin": 1 }
              ],
              "skins": [{ "joints": [1, 2] }, { "joints": [5] }],
              "scenes": [{ "nodes": [0, 4] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("incompatible-skins.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Equal(2, importer.SavedResources.Keys.Count(name => name.StartsWith("Models/", StringComparison.Ordinal)));
    }

    [Fact]
    public void Importer_applies_matrix_transforms_and_skips_tagged_prefab_nodes()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_applies_matrix_transforms_and_skips_tagged_prefab_nodes));
        var document = """
            {
              "asset": { "version": "2.0" },
              "nodes": [
                { "name": "Wrapper", "children": [1, 2] },
                { "name": "Kept", "matrix": [2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 5, 6, 7, 1] },
                { "name": "Skip_me", "children": [3] },
                { "name": "SkippedChild" }
              ],
              "scenes": [{ "nodes": [0] }]
            }
            """;
        var documentPath = artifacts.Write("hierarchy.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings
        {
            AssetName = "Imported",
            CleanupRootNodes = false,
            MirrorX = true,
            Scale = 2.0f,
            SkipTags = ["Skip_"]
        });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var wrapper = Assert.Single(prefab.GetNodePrefab().Children);
        Assert.Equal("Wrapper", wrapper.GetNodeName());
        var kept = Assert.Single(wrapper.Children);
        Assert.Equal("Kept", kept.GetNodeName());
        Assert.Equal(new Vector3f(-10, 12, 14), Assert.Single(kept.Node.Attributes.Where(attribute => attribute.Name == "Position")).Value.GetVector3());
        Assert.Equal(new Vector3f(2, 3, 4), Assert.Single(kept.Node.Attributes.Where(attribute => attribute.Name == "Scale")).Value.GetVector3());
    }

    [Fact]
    public void Importer_cooks_pbr_material_resources()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_pbr_material_resources));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "materials": [
                { "name": "Paint", "alphaMode": "MASK", "alphaCutoff": 0.25, "extensions": { "KHR_materials_unlit": {} }, "pbrMetallicRoughness": { "baseColorFactor": [0.25, 0.5, 0.75, 1.0], "metallicFactor": 0.2, "roughnessFactor": 0.8 } },
                { "name": "Glass", "alphaMode": "BLEND" }
              ],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("material.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings { FadeTransparency = true });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.Equal("Imported/Materials/Paint.xml", Assert.Single(importer.ImportedModel!.GetGeometries()).Material);
        Assert.Contains("Imported/Materials/Paint.xml", importer.SavedResources.Keys);
        Assert.True(importer.SaveResources(), importer.LastError);
        Assert.True(System.IO.File.Exists(Path.Combine(outputPath, "Materials/Paint.xml")));
        var paint = System.Xml.Linq.XDocument.Load(Path.Combine(outputPath, "Materials/Paint.xml"));
        var glass = System.Xml.Linq.XDocument.Load(Path.Combine(outputPath, "Materials/Glass.xml"));
        Assert.Equal("Techniques/UnlitOpaque.xml", paint.Root!.Element("technique")!.Attribute("name")!.Value);
        Assert.Equal("ALPHAMASK", paint.Root.Element("shader")!.Attribute("vsdefines")!.Value.Trim());
        Assert.Equal("Techniques/LitTransparentFade.xml", glass.Root!.Element("technique")!.Attribute("name")!.Value);
        Assert.Contains("Material 'Paint' has non-standard alpha cutoff.", importer.LastWarning, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_cooks_material_texture_resources()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_material_texture_resources));
        var diffuseBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8aQAAAABJRU5ErkJggg==");
        artifacts.Write("Diffuse.png", diffuseBytes);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "images": [{ "uri": "Diffuse.png" }],
              "samplers": [{ "magFilter": 9728, "minFilter": 9728, "wrapS": 33071, "wrapT": 33648 }],
              "textures": [{ "source": 0, "sampler": 0 }],
              "materials": [{ "pbrMetallicRoughness": { "baseColorTexture": { "index": 0, "texCoord": 1 }, "metallicRoughnessTexture": { "index": 0, "texCoord": 1 } }, "occlusionTexture": { "index": 0, "texCoord": 1, "strength": 0.25 }, "normalTexture": { "index": 0, "texCoord": 1 }, "emissiveTexture": { "index": 0, "texCoord": 1 } }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("texture.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.Contains("Imported/Textures/Diffuse.png", importer.SavedResources.Keys);
        Assert.Contains("Imported/Textures/Diffuse.xml", importer.SavedResources.Keys);
        Assert.Contains("Imported/Textures/Diffuse_Properties.png", importer.SavedResources.Keys);
        Assert.True(importer.SaveResources(), importer.LastError);

        var textureFile = Path.Combine(outputPath, "Textures/Diffuse.png");
        Assert.Equal([137, 80, 78, 71], System.IO.File.ReadAllBytes(textureFile)[..4]);
        Assert.Equal([137, 80, 78, 71], System.IO.File.ReadAllBytes(
            Path.Combine(outputPath, "Textures/Diffuse_Properties.png"))[..4]);
        using var sourceBuffer = new MemoryBuffer(diffuseBytes);
        using var sourceImage = new RedBolt.Resource.Image();
        using var propertiesImage = new RedBolt.Resource.Image();
        using var propertiesFile = new RedBolt.IO.File(Path.Combine(outputPath, "Textures/Diffuse_Properties.png"), FileMode.Read);
        Assert.True(sourceImage.Load(sourceBuffer));
        Assert.True(propertiesImage.Load(propertiesFile));
        var sourceColor = sourceImage.GetPixelInt(0, 0);
        Assert.Equal(((sourceColor >> 8) & 0xffffu) | ((sourceColor & 0xffu) << 24), propertiesImage.GetPixelInt(0, 0));
        var parameters = System.Xml.Linq.XDocument.Load(Path.Combine(outputPath, "Textures/Diffuse.xml"));
        Assert.Equal("clamp", parameters.Root!.Elements("address").Single(element => (string?)element.Attribute("coord") == "u").Attribute("mode")!.Value);
        Assert.Equal("mirror", parameters.Root.Elements("address").Single(element => (string?)element.Attribute("coord") == "v").Attribute("mode")!.Value);
        Assert.Equal("nearest", parameters.Root.Element("filter")!.Attribute("mode")!.Value);
        Assert.Equal("false", parameters.Root.Element("mipmap")!.Attribute("enable")!.Value);
        var material = System.Xml.Linq.XDocument.Load(Path.Combine(outputPath, "Materials/Material_0.xml"));
        Assert.Equal("0.25", material.Root!.Elements("parameter")
            .Single(element => (string?)element.Attribute("name") == "OcclusionStrength").Attribute("value")!.Value);
        Assert.Contains("non-standard UV for diffuse texture #0", importer.LastWarning, StringComparison.Ordinal);
        Assert.Contains("non-standard UV for metallic-roughness texture #0", importer.LastWarning, StringComparison.Ordinal);
        Assert.Contains("non-standard UV for occlusion texture #0", importer.LastWarning, StringComparison.Ordinal);
        Assert.Contains("non-default occlusion strength for occlusion texture #0", importer.LastWarning, StringComparison.Ordinal);
        Assert.Contains("non-standard UV for normal texture #0", importer.LastWarning, StringComparison.Ordinal);
        Assert.Contains("non-standard UV for emissive texture #0", importer.LastWarning, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_ignores_occlusion_texture_with_different_uvs()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_ignores_occlusion_texture_with_different_uvs));
        var diffuseBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8aQAAAABJRU5ErkJggg==");
        artifacts.Write("Diffuse.png", diffuseBytes);
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "images": [{ "uri": "Diffuse.png" }],
              "textures": [{ "source": 0 }],
              "materials": [{ "name": "Paint", "pbrMetallicRoughness": { "metallicRoughnessTexture": { "index": 0, "texCoord": 0 } }, "occlusionTexture": { "index": 0, "texCoord": 1, "strength": 0.25 } }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }] }]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("mismatched-occlusion-uv.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.Contains("different UVs", importer.LastWarning, StringComparison.Ordinal);
        Assert.True(importer.SaveResources(), importer.LastError);
        var material = System.Xml.Linq.XDocument.Load(Path.Combine(outputPath, "Materials/Paint.xml"));
        Assert.DoesNotContain(material.Root!.Elements("parameter"), element =>
            (string?)element.Attribute("name") == "OcclusionStrength");
    }

    [Fact]
    public void Importer_cooks_scene_prefab_hierarchy()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_scene_prefab_hierarchy));
        var documentPath = artifacts.Write("scene.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "scenes": [{ "nodes": [0] }],
              "nodes": [
                { "name": "Root", "translation": [1, 2, 3], "children": [1] },
                { "name": "Child", "scale": [2, 3, 4] }
              ]
            }
            """));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings { AssetName = "Asset", Scale = 2.0f });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.Contains("Imported/Prefab.prefab", importer.SavedResources.Keys);
        Assert.True(importer.SaveResources(), importer.LastError);
        Assert.True(System.IO.File.Exists(Path.Combine(outputPath, "Prefab.prefab")));
    }

    [Fact]
    public void Importer_uses_prefab_names_for_unnamed_multiple_scenes()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_uses_prefab_names_for_unnamed_multiple_scenes));
        var documentPath = artifacts.Write("scenes.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "scenes": [{}, {}]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, "Imported/"), importer.LastError);
        Assert.Contains("Imported/Prefabs/Prefab.prefab", importer.SavedResources.Keys);
        Assert.Contains("Imported/Prefabs/Prefab_1.prefab", importer.SavedResources.Keys);
    }

    [Fact]
    public void Importer_applies_preview_scene_settings()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_applies_preview_scene_settings));
        var documentPath = artifacts.Write("scene.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "scenes": [{ "nodes": [0] }],
              "nodes": [{ "name": "Asset" }]
            }
            """));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings
        {
            Preview = new GLTFImporterSettings.PreviewOptions
            {
                AddLights = true,
                AddSkybox = false,
                AddReflectionProbe = false,
                HighRenderQuality = true
            }
        });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var scene = prefab.GetScenePrefab();
        Assert.Contains(scene.Components, component => component.TypeName == nameof(Octree));
        Assert.Contains(scene.Components, component => component.TypeName == nameof(RenderPipeline));
        var light = Assert.Single(scene.Children.Where(node => node.GetNodeName() == "Default Light"));
        Assert.Equal(nameof(Light), Assert.Single(light.Components).TypeName);
    }

    [Fact]
    public void Importer_warns_when_preview_resources_are_missing()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_warns_when_preview_resources_are_missing));
        var documentPath = artifacts.Write("scene.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "scenes": [{}] }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings
        {
            Preview = new GLTFImporterSettings.PreviewOptions
            {
                AddLights = false,
                SkyboxMaterial = "Materials/Missing.xml",
                ReflectionProbeCubemap = "Textures/Missing.xml"
            }
        });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Contains("Cannot add default skybox with material 'Materials/Missing.xml'.", importer.LastWarning,
            StringComparison.Ordinal);
        Assert.Contains("Cannot add default reflection probe with material 'Textures/Missing.xml'.", importer.LastWarning,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_combines_named_mesh_lods()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_combines_named_mesh_lods));
        var document = """
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,$BUFFER$", "byteLength": 42 }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }],
              "meshes": [
                { "name": "Body_LOD10", "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] },
                { "name": "Body_LOD20", "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }
              ]
            }
            """.Replace("$BUFFER$", TriangleBuffer, StringComparison.Ordinal);
        var documentPath = artifacts.Write("lods.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var geometry = Assert.Single(importer.ImportedModel!.GetGeometries());
        Assert.Equal([10.0f, 20.0f], geometry.Lods.Select(lod => lod.LodDistance));
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
    public void Importer_reads_color_uv_and_skin_attributes()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reads_color_uv_and_skin_attributes));
        var data = new List<byte>();
        foreach (var value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) AppendSingle(data, value);
        foreach (var value in new[] { 0f, 0f, 1f, 0f, 0f, 1f }) AppendSingle(data, value);
        foreach (var value in new[] { .5f, .5f, .75f, .5f, .5f, .75f }) AppendSingle(data, value);
        foreach (var value in new[] { 1f, 0f, 0f, 1f, 0f, 1f, 0f, 1f, 0f, 0f, 1f, 1f }) AppendSingle(data, value);
        data.AddRange([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        data.AddRange([255, 0, 0, 0, 0, 255, 0, 0, 0, 0, 255, 0]);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 60, "byteLength": 24 }, { "buffer": 0, "byteOffset": 84, "byteLength": 48 },
                { "buffer": 0, "byteOffset": 132, "byteLength": 12 }, { "buffer": 0, "byteOffset": 144, "byteLength": 12 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" }, { "bufferView": 3, "componentType": 5126, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5121, "count": 3, "type": "VEC4" }, { "bufferView": 5, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" }
              ],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0, "TEXCOORD_0": 1, "TEXCOORD_1": 2, "COLOR_0": 3, "JOINTS_0": 4, "WEIGHTS_0": 5 } }] }]
            }
            """;
        var documentPath = artifacts.Write("skinned-attributes.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);

        var lod = Assert.Single(Assert.Single(importer.ImportedModel!.GetGeometries()).Lods);
        Assert.Equal(new Vector4f(1, 0, 0, 1), lod.Vertices[0].Color[0]);
        Assert.Equal(new Vector4f(.75f, .5f, 0, 0), lod.Vertices[1].Uv[1]);
        Assert.Equal(new Vector4f(5, 6, 7, 8), lod.Vertices[1].BlendIndices);
        Assert.Equal(new Vector4f(0, 1, 0, 0), lod.Vertices[1].BlendWeights);
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
    public void Importer_loads_mesh_without_primitives()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_loads_mesh_without_primitives));
        var documentPath = artifacts.Write("empty-mesh.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "meshes": [{ "primitives": [] }] }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Empty(importer.ImportedModel!.GetGeometries());
        Assert.Empty(importer.ImportedModel.GetMorphs());
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
    public void Importer_warns_about_unsupported_used_extensions()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_warns_about_unsupported_used_extensions));
        var documentPath = artifacts.Write("extensions.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "extensionsUsed": ["EXT_example"] }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.Contains("Unsupported extension used: 'EXT_example'.", importer.LastWarning, StringComparison.Ordinal);
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
    public void Importer_warns_about_ignored_merged_animation_nodes()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_warns_about_ignored_merged_animation_nodes));
        var primary = artifacts.Write("primary.gltf", Encoding.UTF8.GetBytes("""
            { "asset": { "version": "2.0" }, "nodes": [{ "name": "Target" }] }
            """));
        var secondary = artifacts.Write("secondary.gltf", Encoding.UTF8.GetBytes("""
            {
              "asset": { "version": "2.0" },
              "nodes": [{ "name": "Ignored" }],
              "animations": [{ "channels": [{ "sampler": 0, "target": { "node": 0, "path": "translation" } }] }]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(primary), importer.LastError);
        Assert.True(importer.MergeFile(secondary, "Merged"), importer.LastError);
        Assert.Contains("Ignored nodes in animation: Ignored", importer.LastWarning, StringComparison.Ordinal);
        using var animation = Assert.Single(importer.SourceModel!.Animations);
        using var channels = animation.Channels;
        Assert.Empty(channels);
    }

    [Fact]
    public void Importer_cooks_linear_translation_animation()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_cooks_linear_translation_animation));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 0); AppendSingle(data, 0);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{ "name": "Node" }],
              "animations": [{ "name": "Move", "samplers": [{ "input": 0, "output": 1, "interpolation": "LINEAR" }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "translation" } }] }]
            }
            """;
        var documentPath = artifacts.Write("animation.gltf", Encoding.UTF8.GetBytes(document));
        var callback = new AnimationCallback();
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(artifacts.DirectoryPath, "Imported/", callback), importer.LastError);

        var animation = Assert.Single(importer.ImportedAnimations);
        Assert.Same(animation, callback.Animation);
        Assert.Equal("Imported/Animations/Move.ani", animation.Name);
        var track = Assert.Single(animation.Tracks.Values);
        Assert.Equal(RedBolt.Graphics.AnimationChannel.Position, track.ChannelMask);
        Assert.Equal(1, animation.Length);
        Assert.Equal(new Vector3f(2, 0, 0), track.KeyFrames[1].Position);
        Assert.Contains("Imported/Animations/Move.ani", importer.SavedResources.Keys);
        Assert.True(importer.SaveResources(), importer.LastError);
        Assert.Equal("UAN2", Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(
            Path.Combine(artifacts.DirectoryPath, "Animations/Move.ani")), 0, 4));
    }

    [Fact]
    public void Importer_uses_hierarchy_path_for_scene_animation_tracks()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_uses_hierarchy_path_for_scene_animation_tracks));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 0); AppendSingle(data, 0);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{ "name": "Root", "children": [1, 2] }, { "name": "Child" }, { "name": "Child" }],
              "animations": [{ "samplers": [{ "input": 0, "output": 1 }], "channels": [{ "sampler": 0, "target": { "node": 2, "path": "translation" } }] }]
            }
            """;
        var documentPath = artifacts.Write("nested-animation.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Equal("Root/#1", Assert.Single(Assert.Single(importer.ImportedAnimations).Tracks.Values).Name);
    }

    [Fact]
    public void Importer_rejects_unknown_animation_channels()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_unknown_animation_channels));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 1); AppendSingle(data, 0); AppendSingle(data, 0);
        var documentPath = artifacts.Write("unknown-animation-channel.gltf", Encoding.UTF8.GetBytes($$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{}],
              "animations": [{ "samplers": [{ "input": 0, "output": 1 }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "visibility" } }] }]
            }
            """));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.False(importer.Process(string.Empty, string.Empty));
        Assert.Equal("Unknown animation channel 'visibility'.", importer.LastError);
    }

    [Fact]
    public void Importer_preserves_omitted_scene_root_slots_for_scene_animations()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_preserves_omitted_scene_root_slots_for_scene_animations));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 0); AppendSingle(data, 0);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{ "name": "Omitted" }, { "name": "Included" }],
              "scenes": [{ "nodes": [1] }],
              "animations": [{ "samplers": [{ "input": 0, "output": 1 }], "channels": [{ "sampler": 0, "target": { "node": 1, "path": "translation" } }] }]
            }
            """;
        var documentPath = artifacts.Write("omitted-root-animation.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.True(importer.SaveResources(), importer.LastError);

        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var root = prefab.GetNodePrefab();
        Assert.Contains(root.Components, component => component.TypeName == nameof(AnimationController));
        Assert.Equal(["Included"], root.Children.Select(node => node.GetNodeName()));
        Assert.Contains(prefab.GetScenePrefab().Children, node => node.GetNodeName() == "Disabled Node Placeholder");
    }

    [Fact]
    public void Importer_uses_cleaned_bone_names_for_animation_tracks()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_uses_cleaned_bone_names_for_animation_tracks));
        var data = new List<byte>();
        data.AddRange(Convert.FromBase64String(TriangleBuffer));
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 0); AppendSingle(data, 0);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }, { "buffer": 0, "byteOffset": 42, "byteLength": 8 }, { "buffer": 0, "byteOffset": 50, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" }, { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }, { "bufferView": 2, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 3, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
              "nodes": [{ "name": "Rig_Root", "children": [1, 2] }, { "name": "Rig_Joint" }, { "name": "Mesh", "mesh": 0, "skin": 0 }],
              "skins": [{ "skeleton": 0, "joints": [1] }],
              "animations": [{ "samplers": [{ "input": 2, "output": 3 }], "channels": [{ "sampler": 0, "target": { "node": 1, "path": "translation" } }] }],
              "scenes": [{ "nodes": [0] }]
            }
            """;
        var documentPath = artifacts.Write("bone-animation.gltf", Encoding.UTF8.GetBytes(document));
        var outputPath = Path.Combine(artifacts.DirectoryPath, "output");
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(outputPath, "Imported/"), importer.LastError);
        Assert.Equal("Joint", Assert.Single(Assert.Single(importer.ImportedAnimations).Tracks.Values).Name);
        Assert.True(importer.SaveResources(), importer.LastError);
        using var source = new MemoryBuffer(System.IO.File.ReadAllBytes(Path.Combine(outputPath, "Prefab.prefab")));
        using var prefab = new PrefabResource();
        Assert.True(prefab.BeginLoad(source));
        var mesh = prefab.GetNodePrefab().FindChild("Rig_Root/Mesh");
        Assert.NotNull(mesh);
        Assert.Contains(mesh.Components, component => component.TypeName == nameof(AnimationController));
    }

    [Fact]
    public void Importer_merges_nearby_animation_key_times()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_merges_nearby_animation_key_times));
        var data = new List<byte>();
        foreach (var value in new[] { 0f, 1f, 0.0005f, 1.0005f, 0f, 0f, 0f, 2f, 0f, 0f, 1f, 1f, 1f, 2f, 2f, 2f })
            AppendSingle(data, value);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 8 }, { "buffer": 0, "byteOffset": 16, "byteLength": 24 }, { "buffer": 0, "byteOffset": 40, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 2, "componentType": 5126, "count": 2, "type": "VEC3" }, { "bufferView": 3, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{ "name": "Node" }],
              "animations": [{ "samplers": [{ "input": 0, "output": 2 }, { "input": 1, "output": 3 }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "translation" } }, { "sampler": 1, "target": { "node": 0, "path": "scale" } }] }]
            }
            """;
        var documentPath = artifacts.Write("merged-times.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings { KeyFrameTimeError = 0.001f });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        var keys = Assert.Single(Assert.Single(importer.ImportedAnimations).Tracks.Values).KeyFrames;
        Assert.Equal([0f, 1f], keys.Select(keyFrame => keyFrame.Time));
    }

    [Fact]
    public void Importer_skips_tagged_animations()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_skips_tagged_animations));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        AppendSingle(data, 0); AppendSingle(data, 0); AppendSingle(data, 0);
        AppendSingle(data, 2); AppendSingle(data, 0); AppendSingle(data, 0);
        var document = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 24 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 2, "type": "VEC3" }],
              "nodes": [{ "name": "Node" }],
              "animations": [{ "name": "Skip_Move", "samplers": [{ "input": 0, "output": 1 }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "translation" } }] }]
            }
            """;
        var documentPath = artifacts.Write("skip-animation.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings { SkipTags = ["Skip_"] });

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        Assert.Empty(importer.ImportedAnimations);
        Assert.DoesNotContain(importer.SavedResources.Keys, name => name.StartsWith("Animations/", StringComparison.Ordinal));
    }

    [Fact]
    public void Importer_rejects_unknown_source_extensions()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_unknown_source_extensions));
        var documentPath = artifacts.Write("not-gltf.json", Encoding.UTF8.GetBytes("{ \"asset\": { \"version\": \"2.0\" } }"));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.False(importer.LoadFile(documentPath));
        Assert.Contains("Unknown glTF file extension '.json'.", importer.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_rejects_unknown_merge_source_extensions()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_rejects_unknown_merge_source_extensions));
        var primaryPath = artifacts.Write("primary.gltf", Encoding.UTF8.GetBytes("{ \"asset\": { \"version\": \"2.0\" } }"));
        var secondaryPath = artifacts.Write("secondary.json", Encoding.UTF8.GetBytes("{ \"asset\": { \"version\": \"2.0\" } }"));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(primaryPath), importer.LastError);
        Assert.False(importer.MergeFile(secondaryPath, "Merged"));
        Assert.Contains("Unknown glTF file extension '.json'.", importer.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_reads_cubic_spline_bone_animation_values()
    {
        using var artifacts = new TestArtifactScope(nameof(Importer_reads_cubic_spline_bone_animation_values));
        var data = new List<byte>();
        AppendSingle(data, 0); AppendSingle(data, 1);
        foreach (var value in new[] { -1f, 0f, 0f, 0f, 0f, 0f, 2f, 0f, 0f, -1f, 0f, 0f, 3f, 0f, 0f, 1f, 0f, 0f })
            AppendSingle(data, value);
        var document = $$"""
            { "asset": { "version": "2.0" },
              "buffers": [{ "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([.. data])}}", "byteLength": {{data.Count}} }],
              "bufferViews": [{ "buffer": 0, "byteLength": 8 }, { "buffer": 0, "byteOffset": 8, "byteLength": 72 }],
              "accessors": [{ "bufferView": 0, "componentType": 5126, "count": 2, "type": "SCALAR" }, { "bufferView": 1, "componentType": 5126, "count": 6, "type": "VEC3" }],
              "nodes": [{ "name": "Node" }],
              "animations": [{ "samplers": [{ "input": 0, "output": 1, "interpolation": "CUBICSPLINE" }], "channels": [{ "sampler": 0, "target": { "node": 0, "path": "translation" } }] }] }
            """;
        var documentPath = artifacts.Write("cubic.gltf", Encoding.UTF8.GetBytes(document));
        using var importer = new GLTFImporter(new GLTFImporterSettings());

        Assert.True(importer.LoadFile(documentPath), importer.LastError);
        Assert.True(importer.Process(string.Empty, string.Empty), importer.LastError);
        var keys = Assert.Single(Assert.Single(importer.ImportedAnimations).Tracks.Values).KeyFrames;
        Assert.Equal(new Vector3f(0, 0, 0), keys[0].Position);
        Assert.Equal(new Vector3f(3, 0, 0), keys[1].Position);
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

    private sealed class AnimationCallback : GLTFImporterCallback
    {
        public RedBolt.Graphics.Animation? Animation { get; private set; }

        public override void OnAnimationLoaded(RedBolt.Graphics.Animation animation) => Animation = animation;
    }

    private sealed class ArtificialSkinCallback : GLTFImporterCallback
    {
        public override List<string> GetArtificialSkinNodes() => ["Rig"];
    }

    private static void AppendSingle(List<byte> bytes, float value) => bytes.AddRange(BitConverter.GetBytes(value));
}

public sealed class GLTFImporterFixture : IDisposable
{
    private readonly Context _previousContext = Context.Instance;
    private readonly Context _context = new();
    private readonly ResourceCache _resources;

    public GLTFImporterFixture()
    {
        Context.SetInstance(_context);
        _resources = new ResourceCache();
        _context.RegisterSubsystem(_resources);
        _resources.RegisterDefaultResources();
        _resources.AddResourceDir(RbfxTestData.CoreData);
    }

    public void Dispose()
    {
        _resources.Dispose();
        Context.SetInstance(_previousContext);
        _context.Dispose();
    }
}

[CollectionDefinition(nameof(GLTFImporterCollection), DisableParallelization = true)]
public sealed class GLTFImporterCollection : ICollectionFixture<GLTFImporterFixture> { }
