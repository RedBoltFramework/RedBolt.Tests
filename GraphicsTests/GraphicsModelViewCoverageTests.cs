namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

public sealed class GraphicsModelViewCoverageTests
{
    [Fact]
    public void Model_vertex_format_covers_elements_merge_hash_and_clone()
    {
        var format = new ModelVertexFormat();
        Assert.Equal(VertexElementType.MaxVertexElementTypes, format.Position);
        Assert.Empty(format.ToVertexElements());

        format.Position = VertexElementType.Vector3;
        format.Normal = VertexElementType.Vector3;
        format.Tangent = VertexElementType.Vector4;
        format.Binormal = VertexElementType.Vector3;
        format.BlendIndices = VertexElementType.Vector4;
        format.BlendWeights = VertexElementType.Vector4;
        format.Uv[0] = VertexElementType.Vector2;
        format.Color[1] = VertexElementType.Vector4;

        var elements = format.ToVertexElements();
        Assert.Equal(8, elements.Count);
        Assert.Contains(elements, element => element.Semantic == VertexElementSemantic.Position);
        Assert.Contains(elements, element => element.Semantic == VertexElementSemantic.TexCoord);
        Assert.Contains(elements, element => element.Semantic == VertexElementSemantic.Color && element.Index == 1);

        var clone = format.Clone();
        Assert.Equal(format, clone);
        clone.Color[1] = VertexElementType.Vector3;
        Assert.NotEqual(format, clone);

        var merge = new ModelVertexFormat();
        merge.MergeFrom(format);
        Assert.Equal(format, merge);
        var replacement = new ModelVertexFormat { Position = VertexElementType.Vector4 };
        replacement.Uv[3] = VertexElementType.Vector2;
        merge.MergeFrom(replacement);
        Assert.Equal(VertexElementType.Vector4, merge.Position);
        Assert.Equal(VertexElementType.Vector2, merge.Uv[3]);
        Assert.NotEqual(0u, format.ToHash());
        Assert.Equal(format.GetHashCode(), unchecked((int)format.ToHash()));
    }

    [Fact]
    public void Model_vertex_covers_replacement_repair_pruning_and_weights()
    {
        var source = new ModelVertex();
        source.Position = new Vector4f(1, 2, 3, 1);
        source.Normal = new Vector4f(0, 0, 1, 0);
        source.Tangent = new Vector4f(1, 0, 0, 1);
        source.Binormal = new Vector4f(0, 1, 0, 0);
        source.BlendIndices = new Vector4f(1, 2, 3, 4);
        source.BlendWeights = new Vector4f(0.1f, 0.2f, 0.3f, 0.4f);
        source.SetColor(2, new Color(0.1f, 0.2f, 0.3f, 1));

        var vertex = new ModelVertex();
        vertex.SetPosition(new Vector3f(4, 5, 6));
        vertex.SetNormal(new Vector3f(0, 1, 0));
        vertex.SetColor(0, new Color(1, 0, 0, 1));
        Assert.Equal(new Vector3f(4, 5, 6), vertex.GetPosition());
        Assert.Equal(new Vector3f(0, 1, 0), vertex.GetNormal());
        Assert.Equal(new Color(1, 0, 0, 1), vertex.GetColor());
        Assert.False(vertex.HasTangent());
        Assert.False(vertex.HasBinormal());
        Assert.False(vertex.HasTangentBinormalCombined());

        foreach (var semantic in new[]
        {
            VertexElementSemantic.Position,
            VertexElementSemantic.Normal,
            VertexElementSemantic.Binormal,
            VertexElementSemantic.Tangent,
            VertexElementSemantic.BlendWeights,
            VertexElementSemantic.BlendIndices
        })
            Assert.True(vertex.ReplaceElement(source, new VertexElement(VertexElementType.Vector4, semantic)));
        Assert.True(vertex.ReplaceElement(source,
            new VertexElement(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 0)));
        Assert.True(vertex.ReplaceElement(source,
            new VertexElement(VertexElementType.Vector4, VertexElementSemantic.Color, 2)));
        Assert.False(vertex.ReplaceElement(source,
            new VertexElement(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 4)));
        Assert.False(vertex.ReplaceElement(source,
            new VertexElement(VertexElementType.Vector4, VertexElementSemantic.Color, 4)));
        Assert.False(vertex.ReplaceElement(source,
            new VertexElement(VertexElementType.Vector4, (VertexElementSemantic)unchecked((byte)999))));

        vertex.BlendIndices = new Vector4f(0, -1, 16777216, 16777218);
        vertex.BlendWeights = new Vector4f(0.25f, 0.25f, 0.25f, 0.25f);
        var weights = vertex.GetBlendIndicesAndWeights();
        Assert.Equal(new KeyValuePair<uint, float>(0, 0.25f), weights[0]);
        Assert.Equal(new KeyValuePair<uint, float>(0, 0), weights[1]);
        Assert.Equal(new KeyValuePair<uint, float>(16777216, 0.25f), weights[2]);
        Assert.Equal(new KeyValuePair<uint, float>(0, 0), weights[3]);

        var noNormal = new ModelVertex();
        noNormal.Tangent = new Vector4f(1, 0, 0, 1);
        noNormal.Binormal = new Vector4f(0, 1, 0, 0);
        noNormal.Repair();
        Assert.Equal(Vector4f.Zero, noNormal.Tangent);
        Assert.Equal(Vector4f.Zero, noNormal.Binormal);

        var generatedBinormal = new ModelVertex
        {
            Normal = new Vector4f(0, 0, 1, 0),
            Tangent = new Vector4f(1, 0, 0, 1)
        };
        generatedBinormal.Repair();
        Assert.NotEqual(Vector4f.Zero, generatedBinormal.Binormal);

        var correctedHandedness = new ModelVertex
        {
            Normal = new Vector4f(0, 0, 1, 0),
            Tangent = new Vector4f(1, 0, 0, 0),
            Binormal = new Vector4f(0, 1, 0, 0)
        };
        correctedHandedness.Repair();
        Assert.Equal(-1.0f, correctedHandedness.Tangent.W);

        var pruned = source.Clone();
        pruned.PruneElements(new ModelVertexFormat { Position = VertexElementType.Vector3 });
        Assert.NotEqual(Vector4f.Zero, pruned.Position);
        Assert.Equal(Vector4f.Zero, pruned.Normal);
        Assert.Equal(Vector4f.Zero, pruned.Color[2]);
        Assert.NotEqual(source, pruned);
        Assert.NotEqual(source.Clone(), new ModelVertex());
    }

    [Fact]
    public void Geometry_lod_covers_primitive_counts_iteration_normalization_and_normals()
    {
        foreach (var (type, indices, expected) in new[]
        {
            (PrimitiveType.TriangleList, new uint[] { 0, 1, 2, 2, 3, 0 }, 2u),
            (PrimitiveType.LineList, new uint[] { 0, 1, 2, 3 }, 2u),
            (PrimitiveType.PointList, new uint[] { 0, 1, 2 }, 3u),
            (PrimitiveType.TriangleStrip, new uint[] { 0, 1, 2, 3 }, 2u),
            (PrimitiveType.TriangleFan, new uint[] { 0, 1, 2, 3 }, 2u),
            (PrimitiveType.LineStrip, new uint[] { 0, 1, 2 }, 2u),
            ((PrimitiveType)999, new uint[] { 0, 1, 2 }, 0u)
        })
        {
            var lod = new GeometryLODView { PrimitiveType = type, Indices = [.. indices] };
            Assert.Equal(expected, lod.GetNumPrimitives());
            Assert.Equal(type is PrimitiveType.TriangleList or PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan,
                lod.IsTriangleGeometry());
            Assert.Equal(type is PrimitiveType.LineList or PrimitiveType.LineStrip, lod.IsLineGeometry());
            Assert.Equal(type == PrimitiveType.PointList, lod.IsPointGeometry());
        }

        var stripShort = new GeometryLODView { PrimitiveType = PrimitiveType.TriangleStrip, Indices = [0, 1] };
        Assert.Equal(0u, stripShort.GetNumPrimitives());
        var lineShort = new GeometryLODView { PrimitiveType = PrimitiveType.LineStrip, Indices = [0] };
        Assert.Equal(0u, lineShort.GetNumPrimitives());

        var triangle = new GeometryLODView
        {
            PrimitiveType = PrimitiveType.TriangleList,
            Vertices =
            [
                Vertex(new Vector3f(0, 0, 0)),
                Vertex(new Vector3f(1, 0, 0)),
                Vertex(new Vector3f(0, 1, 0))
            ],
            Indices = [0, 1, 2],
            VertexFormat = new ModelVertexFormat { Position = VertexElementType.Vector3 },
            Morphs = new Dictionary<uint, List<ModelVertexMorph>>
            {
                [2] = [new ModelVertexMorph
                {
                    Index = 1,
                    PositionDelta = new Vector3f(1, 0, 0)
                }]
            }
        };
        Assert.Equal(new Vector3f(1.0f / 3, 1.0f / 3, 0), triangle.CalculateCenter());
        Assert.Equal(3u, triangle.CalculateNumMorphs());
        var callbacks = new List<(uint, uint, uint)>();
        triangle.ForEachTriangle((a, b, c) => callbacks.Add((a, b, c)));
        Assert.Equal([(0u, 1u, 2u)], callbacks);
        Assert.Throws<ArgumentNullException>(() => triangle.ForEachTriangle(null!));

        var strip = triangle;
        strip.PrimitiveType = PrimitiveType.TriangleStrip;
        strip.Indices = [0, 1, 2, 3];
        callbacks.Clear();
        strip.ForEachTriangle((a, b, c) => callbacks.Add((a, b, c)));
        Assert.Equal([(0u, 1u, 2u), (1u, 3u, 2u)], callbacks);
        var fan = strip;
        fan.PrimitiveType = PrimitiveType.TriangleFan;
        fan.ForEachTriangle((a, b, c) => callbacks.Add((a, b, c)));
        Assert.Equal(4, callbacks.Count);

        var normalized = triangle;
        normalized.Indices.Clear();
        normalized.Morphs[2].Add(new ModelVertexMorph
        {
            Index = 1,
            NormalDelta = new Vector3f(0, 1, 0)
        });
        normalized.Normalize();
        Assert.Equal([0u, 1u, 2u], normalized.Indices);
        Assert.Single(normalized.Morphs[2]);
        var clone = normalized.Clone();
        var cloneVertex = clone.Vertices[0];
        cloneVertex.Position.X = 99;
        clone.Vertices[0] = cloneVertex;
        Assert.NotEqual(clone.Vertices[0], normalized.Vertices[0]);

        normalized.RecalculateFlatNormals();
        Assert.All(normalized.Vertices, vertex => Assert.NotEqual(Vector4f.Zero, vertex.Normal));
        normalized.RecalculateSmoothNormals();
        normalized.InvalidateNormalsAndTangents();
        Assert.All(normalized.Vertices, vertex =>
        {
            Assert.Equal(Vector4f.Zero, vertex.Normal);
            Assert.Equal(Vector4f.Zero, vertex.Tangent);
        });
    }

    [Fact]
    public void Geometry_and_bone_views_cover_transform_helpers_and_model_view_management()
    {
        var geometry = new GeometryView
        {
            Material = "Materials/Test",
            Lods = [new GeometryLODView { Morphs = new Dictionary<uint, List<ModelVertexMorph>> { [3] = [] } }]
        };
        Assert.Equal(4u, geometry.CalculateNumMorphs());
        var geometryClone = geometry.Clone();
        geometryClone.Material = "Other";
        Assert.NotEqual(geometry, geometryClone);

        var bone = new BoneView { Name = "Root" };
        bone.SetInitialTransform(new Vector3f(1, 2, 3), Quaternion.IDENTITY, Vector3f.One);
        bone.RecalculateOffsetMatrix();
        bone.SetLocalBoundingSphere(2);
        Assert.Equal(BoneCollisionShape.Sphere, bone.ShapeFlags);
        bone.SetLocalBoundingBox(new BoundingBox(new Vector3f(-1, -1, -1), new Vector3f(1, 1, 1)));
        Assert.Equal(BoneCollisionShape.Box, bone.ShapeFlags);
        bone.ResetBoundingVolume();
        Assert.Equal(BoneCollisionShape.None, bone.ShapeFlags);
        Assert.Equal(bone, bone);

        using var view = new ModelView();
        view.SetName("CoverageModel");
        view.SetGeometries([new GeometryView
        {
            Material = "Materials/Test",
            Lods = [new GeometryLODView
            {
                PrimitiveType = PrimitiveType.TriangleList,
                Vertices = [Vertex(new Vector3f(-1, -2, -3)), Vertex(new Vector3f(2, 3, 4)), Vertex(new Vector3f(0, 1, 2))],
                Indices = [0, 1, 2],
                VertexFormat = new ModelVertexFormat { Position = VertexElementType.Vector3 }
            }]
        }]);
        view.SetBones([bone]);
        view.SetMorph(2, new ModelMorphView { Name = "Morph", InitialWeight = 0.5f });
        using var metadata = new Variant("value");
        view.AddMetadata("key", metadata);
        using var replacementMetadata = new Variant(42);
        view.AddMetadata("key", replacementMetadata);

        Assert.Equal("CoverageModel", view.GetName());
        Assert.Single(view.GetGeometries());
        Assert.Single(view.GetBones());
        Assert.Equal(3, view.GetMorphs().Count);
        Assert.Equal(VariantType.Int, view.GetMetadata("key").Type);
        Assert.Equal(VariantType.None, view.GetMetadata("missing").Type);
        Assert.Equal("Materials/Test", view.ExportMaterialList().Names[0]);

        var box = view.CalculateBoundingBox();
        Assert.True(box.Defined);
        view.MirrorGeometriesX();
        view.ScaleGeometries(2);
        Assert.Equal(-4.0f, view.GetGeometries()[0].Lods[0].Vertices[1].Position.X);
        view.Normalize();
        view.CalculateMissingNormals(flatNormals: false);
        view.RecalculateBoneBoundingBoxes(exportedOnly: false);
        view.RepairBoneWeights();
        view.Clear();
        Assert.Empty(view.GetGeometries());
        Assert.Empty(view.GetBones());
    }

    private static ModelVertex Vertex(Vector3f position)
    {
        var vertex = new ModelVertex { Position = position.ToVector4(1) };
        return vertex;
    }
}
