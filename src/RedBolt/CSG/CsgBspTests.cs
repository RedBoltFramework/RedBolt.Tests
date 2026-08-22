// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.CSG;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CsgBspTests
{
    private readonly HeadlessEngineFixture _fixture;

    public CsgBspTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        Csg.RegisterCsgLibrary(_fixture.Context);
    }

    [Fact]
    public void Polygon_clone_and_flip_preserve_value_semantics()
    {
        var polygon = CreateTriangle(0.0f, Vector3f.Up, 7);
        polygon.Vertices[0].Uv[0] = new Vector4f(1.0f, 2.0f, 3.0f, 4.0f);

        var clone = polygon.Clone();
        clone.Vertices[0].Uv[0] = Vector4f.Zero;
        clone.Flip();

        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), polygon.Vertices[0].Uv[0]);
        Assert.Equal(7u, clone.Shared);
        Assert.Equal(-polygon.Plane.Normal, clone.Plane.Normal);
        Assert.Equal(-Vector3f.Up, clone.Vertices[0].GetNormal());
    }

    [Fact]
    public void Bsp_clips_polygons_to_requested_side()
    {
        var bsp = new CsgBsp();
        bsp.Build([CreateTriangle(0.0f, Vector3f.Forward, 0)]);

        var front = CreateTriangle(1.0f, Vector3f.Forward, 1);
        var back = CreateTriangle(-1.0f, Vector3f.Forward, 2);
        var polygons = new List<CsgPolygon> { front, back };

        var outside = bsp.Clip(polygons, CsgClipMode.ClipToOutside);
        var inside = bsp.Clip(polygons, CsgClipMode.ClipToInside);

        Assert.Single(outside);
        Assert.Equal(1u, outside[0].Shared);
        Assert.Single(inside);
        Assert.Equal(2u, inside[0].Shared);
    }

    [Fact]
    public void Triangulation_handles_concave_polygon()
    {
        var polygon = new CsgPolygon(
        [
            CreateVertex(new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward),
            CreateVertex(new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Forward),
            CreateVertex(new Vector3f(2.0f, 2.0f, 0.0f), Vector3f.Forward),
            CreateVertex(new Vector3f(1.0f, 1.0f, 0.0f), Vector3f.Forward),
            CreateVertex(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Forward)
        ], 3);
        var indices = new List<uint>();

        Assert.True(Csg.TriangulatePolygon(polygon, indices));
        Assert.Equal(9, indices.Count);
        Assert.All(indices, index => Assert.InRange(index, 0u, 4u));
    }

    [Fact]
    public void Simplification_merges_coplanar_triangles_with_shared_edge()
    {
        var polygons = new List<CsgPolygon>
        {
            new(
            [
                CreateVertex(new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward),
                CreateVertex(new Vector3f(1.0f, 0.0f, 0.0f), Vector3f.Forward),
                CreateVertex(new Vector3f(1.0f, 1.0f, 0.0f), Vector3f.Forward)
            ], 4),
            new(
            [
                CreateVertex(new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward),
                CreateVertex(new Vector3f(1.0f, 1.0f, 0.0f), Vector3f.Forward),
                CreateVertex(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Forward)
            ], 4)
        };

        Assert.True(Csg.SimplifyPolygons(polygons, CsgCommon.DefaultEpsilon));
        Assert.Single(polygons);
        Assert.Equal(4, polygons[0].Vertices.Count);
    }

    [Fact]
    public void Model_conversion_preserves_transform_layout_and_material_mapping()
    {
        var elements = new VertexElement[]
        {
            new(VertexElementType.Vector3, VertexElementSemantic.Position),
            new(VertexElementType.Vector3, VertexElementSemantic.Normal),
            new(VertexElementType.Vector2, VertexElementSemantic.TexCoord),
        };
        using var vertexBuffer = new VertexBuffer(DeviceObjectFlag.Headless);
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(3, elements));
        vertexBuffer.SetUnpackedData(
        [
            new Vector4f(0.0f, 0.0f, 0.0f, 1.0f), Vector3f.Forward.ToVector4(), Vector4f.Zero,
            new Vector4f(1.0f, 0.0f, 0.0f, 1.0f), Vector3f.Forward.ToVector4(), new Vector4f(1, 0, 0, 0),
            new Vector4f(0.0f, 1.0f, 0.0f, 1.0f), Vector3f.Forward.ToVector4(), new Vector4f(0, 1, 0, 0),
        ]);

        using var indexBuffer = new IndexBuffer(DeviceObjectFlag.Headless);
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(3));
        indexBuffer.SetUnpackedData([0, 1, 2]);

        using var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3, 0, 3));

        using var sourceModel = new Model();
        Assert.True(sourceModel.SetVertexBuffers([vertexBuffer]));
        Assert.True(sourceModel.SetIndexBuffers([indexBuffer]));
        sourceModel.SetNumGeometries(1);
        Assert.True(sourceModel.SetGeometry(0, 0, geometry));

        var polygons = Csg.BuildPolygonsFromModel(sourceModel,
            Matrix3x4.FromTranslation(new Vector3f(2.0f, 3.0f, 4.0f)), 2);

        var polygon = Assert.Single(polygons);
        Assert.Equal(2u << 16, polygon.Shared);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), polygon.Vertices[0].GetPosition());
        Assert.Equal(Vector3f.Forward, polygon.Vertices[0].GetNormal());

        var triangulated = Csg.TriangulatePolygons(polygons);
        ResourceRefList? outputMaterials = new ResourceRefList();
        using var result = Csg.BuildModel(triangulated,
            new Model?[] { null, null, sourceModel },
            new ResourceRefList?[] { null, null, new ResourceRefList(nameof(Material), ["Materials/Test.xml"]) },
            ref outputMaterials);

        var resultGeometry = Assert.IsType<Geometry>(result.GetGeometry(0));
        Assert.Equal(elements, resultGeometry.GetVertexBuffers()[0]!.GetElements());
        Assert.Equal([0u, 1u, 2u], resultGeometry.IndexBuffer!.GetUnpackedData());
        Assert.Equal(ObjectReflection<Material>.TypeId, outputMaterials!.Value.Type);
        Assert.Equal(["Materials/Test.xml"], outputMaterials.Value.Names);
    }

    [Fact]
    public void Model_conversion_uses_geometry_draw_range_for_positions_and_uvs()
    {
        var elements = new VertexElement[]
        {
            new(VertexElementType.Vector3, VertexElementSemantic.Position),
            new(VertexElementType.Vector2, VertexElementSemantic.TexCoord),
        };
        using var vertexBuffer = new VertexBuffer(DeviceObjectFlag.Headless);
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(6, elements));
        vertexBuffer.SetUnpackedData(
        [
            new Vector4f(10, 0, 0, 1), new Vector4f(10, 0, 0, 0),
            new Vector4f(11, 0, 0, 1), new Vector4f(11, 0, 0, 0),
            new Vector4f(10, 1, 0, 1), new Vector4f(10, 1, 0, 0),
            new Vector4f(0, 0, 0, 1), new Vector4f(0, 0, 0, 0),
            new Vector4f(1, 0, 0, 1), new Vector4f(1, 0, 0, 0),
            new Vector4f(0, 1, 0, 1), new Vector4f(0, 1, 0, 0),
        ]);

        using var indexBuffer = new IndexBuffer(DeviceObjectFlag.Headless);
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(6));
        indexBuffer.SetUnpackedData([0, 1, 2, 3, 4, 5]);

        using var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 3, 3, 3, 3));

        using var sourceModel = new Model();
        Assert.True(sourceModel.SetVertexBuffers([vertexBuffer]));
        Assert.True(sourceModel.SetIndexBuffers([indexBuffer]));
        sourceModel.SetNumGeometries(1);
        Assert.True(sourceModel.SetGeometry(0, 0, geometry));

        var polygon = Assert.Single(Csg.BuildPolygonsFromModel(sourceModel, Matrix3x4.Identity));

        Assert.Equal(Vector3f.Zero, polygon.Vertices[0].GetPosition());
        Assert.Equal(new Vector4f(1, 0, 0, 0), polygon.Vertices[1].Uv[0]);
        Assert.Equal(new Vector4f(0, 1, 0, 0), polygon.Vertices[2].Uv[0]);
    }

    [Fact]
    public void Difference_between_overlapping_boxes_keeps_cut_surface()
    {
        var box = Assert.IsType<Model>(_fixture.Resources.Get<Model>("Models/Box.mdl"));
        var bspA = Csg.BuildBspFromModel(box, Matrix3x4.Identity, 0);
        var bspB = Csg.BuildBspFromModel(box,
            Matrix3x4.FromTranslation(new Vector3f(0.5f, 0.5f, 0.5f)), 1);

        var result = Csg.BooleanOperation(bspA, bspB, CsgOperation.DifferenceAB);

        Assert.NotEmpty(result);
        Assert.Contains(result, polygon => polygon.Shared >> 16 == 1);
        var triangulated = Csg.TriangulatePolygons(result);
        Assert.Contains(triangulated.Geometries, geometry => geometry.Shared >> 16 == 1);
    }

    [Fact]
    public void Generated_registration_and_brush_state_match_component_workflow()
    {
        var componentReflection = _fixture.Context.GetReflection<CsgComponent>();
        var brushReflection = _fixture.Context.GetReflection<CsgBrush>();
        var rootReflection = _fixture.Context.GetReflection<CsgRoot>();

        Assert.NotNull(componentReflection);
        Assert.NotNull(brushReflection);
        Assert.NotNull(rootReflection);
        var operationAttribute = Assert.IsType<AttributeInfo>(brushReflection.GetAttribute("Operation"));
        Assert.Equal(["Union", "DifferenceAB", "Intersection"], operationAttribute.EnumNames);
        Assert.NotNull(brushReflection.GetAttribute("Model"));
        Assert.NotNull(rootReflection.GetAttribute("Deferred Delay Sec"));
        Assert.NotNull(rootReflection.GetAttribute("Epsilon"));
        Assert.NotNull(rootReflection.GetAttribute("Materials"));

        using var scene = new RedBolt.Scene.Scene();
        var root = scene.CreateChild("Root").CreateComponent<CsgRoot>();
        var brush = root.Node.CreateChild("Brush").CreateComponent<CsgBrush>();

        brush.Operation = CsgOperation.DifferenceAB;
        Assert.True(brush.IsDirty);
        brush.ClearDirty();
        Assert.False(brush.IsDirty);
        root.DeferredDelaySec = -1.0f;
        Assert.Equal(0.0f, root.DeferredDelaySec);
        root.Epsilon = -1.0f;
        Assert.Equal(0.0f, root.Epsilon);
    }

    private static CsgPolygon CreateTriangle(float z, Vector3f normal, uint shared)
    {
        return new CsgPolygon(
        [
            CreateVertex(new Vector3f(0.0f, 0.0f, z), normal),
            CreateVertex(new Vector3f(1.0f, 0.0f, z), normal),
            CreateVertex(new Vector3f(0.0f, 1.0f, z), normal)
        ], shared);
    }

    private static ModelVertex CreateVertex(Vector3f position, Vector3f normal)
    {
        var vertex = new ModelVertex();
        vertex.SetPosition(position);
        vertex.SetNormal(normal);
        return vertex;
    }
}
