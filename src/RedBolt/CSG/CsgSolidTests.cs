// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections.Generic;
using System.Linq;

using RedBolt.CSG;
using RedBolt.Graphics;
using RedBolt.Math;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CsgSolidTests
{
    private const double VolumeTolerance = 1e-3;

    private readonly HeadlessEngineFixture _fixture;

    public CsgSolidTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        Csg.RegisterCsgLibrary(_fixture.Context);
    }

    [Theory]
    [InlineData(CsgOperation.Union)]
    [InlineData(CsgOperation.DifferenceAB)]
    [InlineData(CsgOperation.Intersection)]
    public void Overlapping_spheres_match_bsp_volume_and_stay_closed(CsgOperation operation)
    {
        var a = Polygons("Models/Sphere.mdl", Matrix3x4.Identity);
        var b = Polygons("Models/Sphere.mdl", Matrix3x4.FromTranslation(new Vector3f(0.35f, 0.35f, 0.35f)));

        var result = Csg.BooleanOperation(new CsgSolid(a), new CsgSolid(b), operation);

        Assert.True(result.IsClosed);
        Assert.Equal(Volume(Bsp(a, b, operation)), Volume(result.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Contained_difference_subtracts_inner_volume()
    {
        var outer = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var inner = Polygons("Models/Sphere.mdl", Matrix3x4.FromScale(0.5f));

        var result = Csg.BooleanOperation(new CsgSolid(outer), new CsgSolid(inner), CsgOperation.DifferenceAB);

        Assert.True(result.IsClosed);
        Assert.Equal(2, result.ComponentCount);
        Assert.Equal(Volume(outer) - Volume(inner), Volume(result.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Disjoint_union_keeps_every_face_and_component()
    {
        var a = Polygons("Models/Sphere.mdl", Matrix3x4.Identity);
        var b = Polygons("Models/Box.mdl", Matrix3x4.FromTranslation(new Vector3f(5.0f, 0.0f, 0.0f)));
        var solidA = new CsgSolid(a);
        var solidB = new CsgSolid(b);

        var result = Csg.BooleanOperation(solidA, solidB, CsgOperation.Union);

        Assert.True(result.IsClosed);
        Assert.Equal(solidA.FaceCount + solidB.FaceCount, result.FaceCount);
        Assert.Equal(solidA.ComponentCount + solidB.ComponentCount, result.ComponentCount);
        Assert.Equal(Volume(a) + Volume(b), Volume(result.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Boolean_result_can_be_reused_as_an_operand()
    {
        var box = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var cutter = Polygons("Models/Cylinder.mdl",
            new Matrix3x4(Vector3f.Zero, new Quaternion(90.0f, Vector3f.Right), new Vector3f(0.5f, 2.0f, 0.5f)));
        var sphere = Polygons("Models/Sphere.mdl", Matrix3x4.FromTranslation(new Vector3f(0.4f, 0.4f, 0.0f)));

        var first = Csg.BooleanOperation(new CsgSolid(box), new CsgSolid(cutter), CsgOperation.DifferenceAB);
        var second = Csg.BooleanOperation(first, new CsgSolid(sphere), CsgOperation.Union);

        var reference = Bsp(Bsp(box, cutter, CsgOperation.DifferenceAB), sphere, CsgOperation.Union);
        Assert.Equal(Volume(reference), Volume(second.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Solids_touching_along_an_edge_stay_closed()
    {
        // The second box shares only the edge x = 0.5, y = 0.5 with the first; the union pinches that edge.
        var a = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var b = Polygons("Models/Box.mdl", Matrix3x4.FromTranslation(new Vector3f(1.0f, 1.0f, 0.0f)));

        var result = Csg.BooleanOperation(new CsgSolid(a), new CsgSolid(b), CsgOperation.Union);

        Assert.True(result.IsClosed);
        Assert.Equal(2.0, Volume(result.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Zero_area_needle_faces_do_not_open_a_solid()
    {
        var box = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        // A collinear triangle on the top face, present in both orientations as boolean fragments can leave it.
        var needle = new[]
        {
            new Vector3f(-0.5f, 0.5f, 0.0f), new Vector3f(0.0f, 0.5f, 0.0f), new Vector3f(0.5f, 0.5f, 0.0f)
        };
        box.Add(new CsgPolygon([Vertex(needle[0]), Vertex(needle[1]), Vertex(needle[2])], 0));
        box.Add(new CsgPolygon([Vertex(needle[2]), Vertex(needle[1]), Vertex(needle[0])], 0));

        Assert.True(new CsgSolid(box).IsClosed);
    }

    [Fact]
    public void Contains_point_classifies_inside_outside_and_far_points()
    {
        var solid = new CsgSolid(Polygons("Models/Sphere.mdl", Matrix3x4.Identity));

        Assert.True(solid.ContainsPoint(Vector3f.Zero));
        Assert.True(solid.ContainsPoint(new Vector3f(0.49f, 0.0f, 0.0f)));
        Assert.False(solid.ContainsPoint(new Vector3f(0.51f, 0.0f, 0.0f)));
        Assert.False(solid.ContainsPoint(new Vector3f(100.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Open_mesh_is_not_closed()
    {
        // Operands must be closed; an open mesh is detected rather than silently accepted.
        Assert.False(new CsgSolid(Polygons("Models/Plane.mdl", Matrix3x4.Identity)).IsClosed);
    }

    [Theory]
    [InlineData(CsgOperation.Union, 1.5)]
    [InlineData(CsgOperation.DifferenceAB, 0.5)]
    [InlineData(CsgOperation.Intersection, 0.5)]
    public void Coplanar_faces_produce_closed_results(CsgOperation operation, double volume)
    {
        // Half-overlapping unit boxes share four face planes, so every contact is coplanar.
        var a = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var b = Polygons("Models/Box.mdl", Matrix3x4.FromTranslation(new Vector3f(0.5f, 0.0f, 0.0f)));

        var result = Csg.BooleanOperation(new CsgSolid(a), new CsgSolid(b), operation);

        Assert.True(result.IsClosed);
        Assert.Equal(volume, Volume(result.ToPolygons()), VolumeTolerance);
    }

    [Fact]
    public void Empty_result_is_closed()
    {
        var a = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var b = Polygons("Models/Box.mdl", Matrix3x4.FromTranslation(new Vector3f(3.0f, 0.0f, 0.0f)));

        var result = Csg.BooleanOperation(new CsgSolid(a), new CsgSolid(b), CsgOperation.Intersection);

        Assert.Equal(0, result.FaceCount);
        Assert.True(result.IsClosed);
    }

    [Fact]
    public void Returned_polygons_and_triangulated_vertices_do_not_alias_solid_faces()
    {
        var box = Polygons("Models/Box.mdl", Matrix3x4.Identity);
        var solid = Csg.BooleanOperation(new CsgSolid(box),
            new CsgSolid(Polygons("Models/Box.mdl", Matrix3x4.FromTranslation(new Vector3f(0.5f, 0.5f, 0.5f)))),
            CsgOperation.DifferenceAB);
        var before = solid.ToPolygons();
        var original = before[0].Vertices[0].Uv[0];

        var copies = solid.ToPolygons();
        copies[0].Vertices[0].Uv[0] = new Vector4f(9.0f, 9.0f, 9.0f, 9.0f);
        var triangulated = Csg.TriangulatePolygons(solid.ToPolygons());
        foreach (var vertex in triangulated.Geometries.SelectMany(geometry => geometry.Vertices))
            vertex.Uv[0] = new Vector4f(7.0f, 7.0f, 7.0f, 7.0f);

        Assert.Equal(original, solid.ToPolygons()[0].Vertices[0].Uv[0]);
    }

    private static ModelVertex Vertex(Vector3f position)
    {
        var vertex = new ModelVertex();
        vertex.SetPosition(position);
        vertex.SetNormal(Vector3f.Up);
        return vertex;
    }

    private List<CsgPolygon> Polygons(string model, Matrix3x4 transform) =>
        Csg.BuildPolygonsFromModel(Assert.IsType<Model>(_fixture.Resources.Get<Model>(model)), transform);

    private static List<CsgPolygon> Bsp(List<CsgPolygon> a, List<CsgPolygon> b, CsgOperation operation)
    {
        var bspA = new CsgBsp();
        bspA.Build(a.Select(polygon => polygon.Clone()).ToList());
        var bspB = new CsgBsp();
        bspB.Build(b.Select(polygon => polygon.Clone()).ToList());
        return Csg.BooleanOperation(bspA, bspB, operation);
    }

    private static double Volume(IEnumerable<CsgPolygon> polygons)
    {
        var sixVolumes = 0.0;
        foreach (var polygon in polygons)
        {
            var first = polygon.Vertices[0].GetPosition();
            for (var i = 1; i + 1 < polygon.Vertices.Count; ++i)
                sixVolumes += first.DotProduct(polygon.Vertices[i].GetPosition().CrossProduct(polygon.Vertices[i + 1].GetPosition()));
        }
        return sixVolumes / 6.0;
    }
}
