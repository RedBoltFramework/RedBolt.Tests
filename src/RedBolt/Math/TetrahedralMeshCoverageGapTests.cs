// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests.MathTests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.Math;

public sealed class TetrahedralMeshCoverageGapTests
{
    [Fact]
    public void Cubic_helpers_cover_boundary_clamps_and_root_selection()
    {
        var solveCubicEquation = PrivateStatic(
            "SolveCubicEquation",
            typeof(double[]), typeof(double), typeof(double), typeof(double), typeof(double));
        var roots = new double[3];

        Assert.Equal(3, (int)solveCubicEquation.Invoke(
            null, [roots, 0.0, -3.0, 2.0000005, MathDefs.Epsilon])!);
        Assert.Equal(3, (int)solveCubicEquation.Invoke(
            null, [roots, 0.0, -3.0, -2.0000005, MathDefs.Epsilon])!);

        var q = 1000.0;
        var r = global::System.Math.Sqrt(q * q * q + 2.0 * MathDefs.Epsilon);
        Assert.Equal(2, (int)solveCubicEquation.Invoke(
            null, [roots, 0.0, -3.0 * q, 2.0 * r, MathDefs.Epsilon])!);

        var getSmallestPositiveRoot = PrivateStatic(
            "GetSmallestPositiveRoot", typeof(double[]), typeof(int));
        Assert.Equal(2.0, (double)getSmallestPositiveRoot.Invoke(
            null, [new[] { -MathDefs.LargeValue * 2.0, 2.0, 3.0 }, 3])!);
        Assert.Equal(1.0, (double)getSmallestPositiveRoot.Invoke(
            null, [new[] { 1.0, 2.0 }, 2])!);
    }

    [Fact]
    public void Connectivity_and_rebuild_helpers_cover_empty_removed_and_linked_paths()
    {
        var empty = new TetrahedralMesh();
        var emptyRemoved = new List<bool>();
        Assert.True((bool)PrivateInstance("IsAdjacencyValid", typeof(bool)).Invoke(empty, [false])!);
        PrivateInstance("EnsureMeshConnectivity", typeof(List<bool>)).Invoke(empty, [emptyRemoved]);

        var valid = new TetrahedralMesh();
        valid.Tetrahedrons.Add(new Tetrahedron());
        Assert.True((bool)PrivateInstance("IsAdjacencyValid", typeof(bool)).Invoke(valid, [false])!);
        Assert.False((bool)PrivateInstance("IsAdjacencyValid", typeof(bool)).Invoke(valid, [true])!);

        var mismatch = new TetrahedralMesh();
        var first = new Tetrahedron();
        first.Neighbors[0] = 1;
        mismatch.Tetrahedrons.Add(first);
        mismatch.Tetrahedrons.Add(new Tetrahedron());
        Assert.False((bool)PrivateInstance("IsAdjacencyValid", typeof(bool)).Invoke(mismatch, [false])!);

        var connectivity = new TetrahedralMesh();
        var connectedRoot = new Tetrahedron();
        connectedRoot.Neighbors[0] = 1;
        connectedRoot.Neighbors[1] = 0;
        connectedRoot.Neighbors[2] = 3;
        connectivity.Tetrahedrons.Add(connectedRoot);
        connectivity.Tetrahedrons.Add(new Tetrahedron());
        connectivity.Tetrahedrons.Add(new Tetrahedron());
        connectivity.Tetrahedrons.Add(new Tetrahedron());
        var removed = new List<bool> { false, true, false, false };

        PrivateInstance("EnsureMeshConnectivity", typeof(List<bool>)).Invoke(connectivity, [removed]);

        Assert.True(removed[2]);
        Assert.False(removed[3]);

        var rebuild = new TetrahedralMesh();
        rebuild.Tetrahedrons.Add(new Tetrahedron());
        var linkedFirst = new Tetrahedron();
        linkedFirst.Neighbors[0] = 2;
        rebuild.Tetrahedrons.Add(linkedFirst);
        var linkedSecond = new Tetrahedron();
        linkedSecond.Neighbors[0] = 1;
        rebuild.Tetrahedrons.Add(linkedSecond);

        PrivateInstance("RemoveMarkedTetrahedrons", typeof(IReadOnlyList<bool>)).Invoke(
            rebuild, [new List<bool> { true, false, false }]);

        Assert.Equal(2, rebuild.Tetrahedrons.Count);
        Assert.Equal(1u, rebuild.Tetrahedrons[0].Neighbors[0]);
        Assert.Equal(0u, rebuild.Tetrahedrons[1].Neighbors[0]);
    }

    [Fact]
    public void Vertex_cleanup_helpers_cover_highlight_edges_extra_normals_and_ignored_vertices()
    {
        var mesh = new TetrahedralMesh();
        for (var i = 0; i < 10; ++i)
            mesh.Vertices.Add(new Vector3f(i, i + 1, i + 2));
        mesh.DebugHighlightEdges.Add((0, 8));
        mesh.DebugHighlightEdges.Add((8, 1));

        PrivateInstance("RemoveSuperMeshVertices").Invoke(mesh, null);

        Assert.Equal((2u, 0u), mesh.DebugHighlightEdges[0]);
        Assert.Equal((0u, 3u), mesh.DebugHighlightEdges[1]);

        var ignored = new TetrahedralMesh();
        for (var i = 0; i < 10; ++i)
            ignored.Vertices.Add(new Vector3f(i, 0.0f, 0.0f));
        var tetrahedron = new Tetrahedron();
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 0;
        tetrahedron.Indices[2] = 0;
        tetrahedron.Indices[3] = 0;
        ignored.Tetrahedrons.Add(tetrahedron);

        PrivateInstance("UpdateIgnoredVertices").Invoke(ignored, null);

        Assert.Equal([1u], ignored.IgnoredVertices);

        var normals = new TetrahedralMesh();
        normals.Vertices.AddRange([Vector3f.Zero, Vector3f.Right]);
        normals.HullNormals.AddRange([Vector3f.Up, Vector3f.Forward, Vector3f.One]);
        PrivateInstance("CalculateHullNormals", typeof(TetrahedralMeshSurface).MakeByRefType()).Invoke(
            normals, [new TetrahedralMeshSurface()]);
        Assert.Equal(2, normals.HullNormals.Count);

        var outer = new TetrahedralMesh();
        outer.Tetrahedrons.Add(new Tetrahedron());
        PrivateInstance("BuildOuterTetrahedrons", typeof(TetrahedralMeshSurface).MakeByRefType()).Invoke(
            outer, [new TetrahedralMeshSurface()]);
        Assert.Empty(outer.Tetrahedrons);
    }

    [Fact]
    public void Filter_and_rebuild_surface_paths_remove_overconnected_tetrahedrons()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        ]);

        for (var i = 0; i < 3; ++i)
        {
            var tetrahedron = new Tetrahedron();
            tetrahedron.Indices[0] = 0;
            tetrahedron.Indices[1] = 1;
            tetrahedron.Indices[2] = 2;
            tetrahedron.Indices[3] = 3;
            mesh.Tetrahedrons.Add(tetrahedron);
        }

        var forcedSkip = new List<bool> { false, false, false };
        PrivateInstance("FilterMeshSurface", typeof(List<bool>), typeof(bool)).Invoke(mesh, [forcedSkip, true]);
        Assert.DoesNotContain(true, forcedSkip);

        var removed = new List<bool> { false, false, false };
        PrivateInstance("FilterMeshSurface", typeof(List<bool>)).Invoke(mesh, [removed]);

        Assert.Contains(true, removed);
        Assert.Equal(3, mesh.Tetrahedrons.Count);
    }

    [Fact]
    public void Filter_mesh_surface_skips_tetrahedrons_removed_by_an_earlier_edge()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(10.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 1.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 1.0f),
            new Vector3f(0.0f, 0.0f, 2.0f),
            new Vector3f(0.0f, 0.0f, 3.0f),
            new Vector3f(5.0f, 8.66f, 0.0f),
            new Vector3f(5.0f, 0.0f, 8.66f),
            new Vector3f(5.0f, -8.66f, 0.0f),
            new Vector3f(5.0f, 0.0f, -8.66f)
        ]);

        foreach (var indices in new uint[][]
        {
            [0, 1, 2, 3],
            [0, 1, 2, 4],
            [0, 1, 2, 5],
            [0, 1, 6, 7],
            [0, 1, 8, 9]
        })
        {
            var tetrahedron = new Tetrahedron();
            for (var index = 0; index < 4; ++index)
                tetrahedron.Indices[index] = indices[index];
            mesh.Tetrahedrons.Add(tetrahedron);
        }

        var removed = new List<bool> { false, false, false, false, false };
        PrivateInstance("FilterMeshSurface", typeof(List<bool>)).Invoke(mesh, [removed]);

        Assert.True(removed.Count(static value => value) >= 3);
    }

    [Fact]
    public void Tetrahedral_mesh_public_barycentric_fallbacks_are_reached()
    {
        var outer = new TetrahedralMesh();
        outer.Vertices.AddRange([Vector3f.Zero, Vector3f.Right, Vector3f.Up]);
        var outerTetrahedron = new Tetrahedron();
        outerTetrahedron.Indices[0] = 0;
        outerTetrahedron.Indices[1] = 1;
        outerTetrahedron.Indices[2] = 2;
        outer.Tetrahedrons.Add(outerTetrahedron);

        Assert.Equal(new Vector4f(0.0f, 0.0f, 0.0f, -1.0f),
            outer.GetOuterBarycentricCoords(0, Vector3f.Back));

        var inner = new TetrahedralMesh();
        inner.Vertices.AddRange([Vector3f.Zero, Vector3f.Right, Vector3f.Up, Vector3f.Forward]);
        var innerTetrahedron = new Tetrahedron();
        innerTetrahedron.Indices[0] = 0;
        innerTetrahedron.Indices[1] = 1;
        innerTetrahedron.Indices[2] = 2;
        innerTetrahedron.Indices[3] = 3;
        innerTetrahedron.CalculateInnerMatrix(inner.Vertices);
        for (var i = 0; i < 4; ++i)
            innerTetrahedron.Neighbors[i] = 0;
        inner.Tetrahedrons.Add(innerTetrahedron);
        inner.NumInnerTetrahedrons = 1;

        uint hint = 0;
        var result = inner.GetInterpolationFactors(new Vector3f(2.0f, 2.0f, 2.0f), ref hint);
        Assert.Equal(0u, hint);
        Assert.True(result.X < 0.0f);
    }

    [Fact]
    public void Define_retries_degenerate_positions_and_finishes_with_ignored_vertices()
    {
        var mesh = new TetrahedralMesh();
        mesh.Define([Vector3f.Zero, Vector3f.Zero, Vector3f.Zero, Vector3f.Zero]);

        Assert.Equal(4, mesh.IgnoredVertices.Count);
    }

    [Fact]
    public void BuildTetrahedrons_requeues_a_position_that_cannot_be_located()
    {
        var mesh = new TetrahedralMesh();
        PrivateInstance("InitializeSuperMesh", typeof(BoundingBox)).Invoke(
            mesh, [new BoundingBox(Vector3f.Zero, Vector3f.One)]);

        PrivateInstance("BuildTetrahedrons", typeof(IReadOnlyList<Vector3f>)).Invoke(
            mesh, [new List<Vector3f> { new(float.NaN, 0.0f, 0.0f) }]);

        Assert.NotEmpty(mesh.IgnoredVertices);
    }

    [Fact]
    public void Intersected_hole_paths_cover_failure_reversion_and_success()
    {
        var mesh = CreateSharedFaceMesh();
        var findAndRemove = PrivateInstance(
            "FindAndRemoveIntersected",
            DelaunayContext.MakeByRefType(),
            typeof(Vector3f),
            typeof(TetrahedralMeshSurface).MakeByRefType(),
            typeof(List<uint>),
            typeof(bool));

        var noTetraContext = CreateDelaunayContext(
            [new HighPrecisionSphere { Radius = 1.0 }], [true, true]);
        var noTetraSurface = new TetrahedralMeshSurface();
        var noTetraRemoved = new List<uint>();
        Assert.False((bool)findAndRemove.Invoke(mesh, [
            noTetraContext, new Vector3f(0.25f, 0.25f, 0.0f), noTetraSurface, noTetraRemoved, false])!);

        var outsideContext = CreateDelaunayContext(
            [new HighPrecisionSphere { Radius = 0.0 }, new HighPrecisionSphere { Radius = 0.0 }],
            [false, false]);
        var outsideSurface = new TetrahedralMeshSurface();
        var outsideRemoved = new List<uint>();
        Assert.False((bool)findAndRemove.Invoke(mesh, [
            outsideContext, new Vector3f(0.25f, 0.25f, 0.0f), outsideSurface, outsideRemoved, false])!);

        var coplanarContext = CreateDelaunayContext(
            [new HighPrecisionSphere { Radius = 1.0 }, new HighPrecisionSphere { Radius = 0.0 }],
            [false, false]);
        var coplanarSurface = new TetrahedralMeshSurface();
        var coplanarRemoved = new List<uint>();
        Assert.False((bool)findAndRemove.Invoke(mesh, [
            coplanarContext, new Vector3f(0.25f, 0.25f, 0.0f), coplanarSurface, coplanarRemoved, false])!);
        Assert.Empty(coplanarSurface.Faces);
        Assert.Empty(coplanarRemoved);

        var dumpContext = CreateDelaunayContext(
            [new HighPrecisionSphere { Radius = 1.0 }, new HighPrecisionSphere { Radius = 0.0 }],
            [false, false]);
        var dumpSurface = new TetrahedralMeshSurface();
        var dumpRemoved = new List<uint>();
        Assert.False((bool)findAndRemove.Invoke(mesh, [
            dumpContext, new Vector3f(0.25f, 0.25f, 0.0f), dumpSurface, dumpRemoved, true])!);
        Assert.NotEmpty(mesh.DebugHighlightEdges);

        var successContext = CreateDelaunayContext(
            [new HighPrecisionSphere { Radius = 1.0 }, new HighPrecisionSphere { Radius = 0.0 }],
            [false, false]);
        var successSurface = new TetrahedralMeshSurface();
        var successRemoved = new List<uint>();
        Assert.True((bool)findAndRemove.Invoke(mesh, [
            successContext, new Vector3f(0.1f, 0.1f, 0.1f), successSurface, successRemoved, false])!);
        Assert.Single(successRemoved);
        Assert.Equal(4, successSurface.Faces.Count);
        Assert.True(successSurface.IsClosedSurface());

        var invalidAdjacencyMesh = new TetrahedralMesh();
        invalidAdjacencyMesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        ]);
        invalidAdjacencyMesh.Vertices.AddRange(
        [
            new Vector3f(2.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 2.0f, 0.0f)
        ]);
        for (var index = 0; index < 3; ++index)
        {
            var tetrahedron = new Tetrahedron();
            tetrahedron.Indices[0] = 0;
            tetrahedron.Indices[1] = 1;
            tetrahedron.Indices[2] = 2;
            tetrahedron.Indices[3] = (uint)(3 + index);
            tetrahedron.CalculateInnerMatrix(invalidAdjacencyMesh.Vertices);
            invalidAdjacencyMesh.Tetrahedrons.Add(tetrahedron);
        }

        var first = invalidAdjacencyMesh.Tetrahedrons[0];
        first.Neighbors[0] = 1;
        invalidAdjacencyMesh.Tetrahedrons[0] = first;
        var second = invalidAdjacencyMesh.Tetrahedrons[1];
        second.Neighbors[0] = 0;
        second.Neighbors[1] = 2;
        invalidAdjacencyMesh.Tetrahedrons[1] = second;
        var third = invalidAdjacencyMesh.Tetrahedrons[2];
        third.Neighbors[1] = 1;
        invalidAdjacencyMesh.Tetrahedrons[2] = third;

        var invalidAdjacencyContext = CreateDelaunayContext(
            [
                new HighPrecisionSphere { Radius = 100.0 },
                new HighPrecisionSphere { Radius = 100.0 },
                new HighPrecisionSphere { Radius = 100.0 }
            ], [false, false, false]);
        var invalidAdjacencySurface = new TetrahedralMeshSurface();
        var invalidAdjacencyRemoved = new List<uint>();
        Assert.False((bool)findAndRemove.Invoke(invalidAdjacencyMesh, [
            invalidAdjacencyContext, new Vector3f(0.1f, 0.1f, 0.1f),
            invalidAdjacencySurface, invalidAdjacencyRemoved, false])!);
    }

    [Fact]
    public void CString_null_equality_and_interpolated_branches_are_exercised()
    {
        using var nullText = new CString((string)null!);
        Assert.Equal(string.Empty, nullText.ConvertToString());

        using var left = new CString("same");
        using var right = new CString("diff");
        Assert.False(left.Equals(right));

        using var emptyArgument = new CString();
        using var formatted = new CString();
        formatted.FormatInPlace($"before{emptyArgument}after");
        Assert.Equal("beforeafter", formatted.ConvertToString());
    }

    [Fact]
    public void CString_retry_span_formatting_is_exercised()
    {
        using var formatted = new CString();
        formatted.FormatInPlace("{0}", new RetryFormattingValue());
        Assert.Equal("retry", formatted.ConvertToString());
    }

    [Fact]
    public void CString_malformed_format_is_exercised()
    {
        using var formatted = new CString();
        Assert.Throws<FormatException>(() => formatted.FormatInPlace("{0x}", "value"));
    }

    [Fact]
    public void CString_heap_growth_and_zero_capacity_are_exercised()
    {
        using var heapText = new CString(new string('a', 20));
        heapText.FormatInPlace(new string('b', 1000));
        Assert.Equal(1000, heapText.Length);

        using var zeroCapacityText = new CString();
        typeof(CString).GetField("_capacity", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(zeroCapacityText, 0);
        zeroCapacityText.FormatInPlace("x");
        Assert.Equal("x", zeroCapacityText.ConvertToString());
    }

    private static TetrahedralMesh CreateSharedFaceMesh()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward,
            Vector3f.Back
        ]);

        var first = new Tetrahedron();
        first.Indices[0] = 0;
        first.Indices[1] = 1;
        first.Indices[2] = 2;
        first.Indices[3] = 3;
        first.Neighbors[3] = 1;
        first.CalculateInnerMatrix(mesh.Vertices);
        mesh.Tetrahedrons.Add(first);

        var second = new Tetrahedron();
        second.Indices[0] = 0;
        second.Indices[1] = 2;
        second.Indices[2] = 1;
        second.Indices[3] = 4;
        second.Neighbors[3] = 0;
        second.CalculateInnerMatrix(mesh.Vertices);
        mesh.Tetrahedrons.Add(second);
        return mesh;
    }

    private static object CreateDelaunayContext(
        IEnumerable<HighPrecisionSphere> circumspheres, IEnumerable<bool> removed)
    {
        var contextType = typeof(TetrahedralMesh).GetNestedType("DelaunayContext", BindingFlags.NonPublic)!;
        var context = Activator.CreateInstance(contextType)!;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        contextType.GetField("Circumspheres", fields)!.SetValue(context, circumspheres.ToList());
        contextType.GetField("Removed", fields)!.SetValue(context, removed.ToList());
        return context;
    }

    private static MethodInfo PrivateStatic(string name, params Type[] parameterTypes) =>
        typeof(TetrahedralMesh).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, parameterTypes)!;

    private static MethodInfo PrivateInstance(string name, params Type[] parameterTypes) =>
        typeof(TetrahedralMesh).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, parameterTypes)!;

    private readonly struct RetryFormattingValue : ISpanFormattable
    {
        public override string ToString() => "retry";

        public string ToString(string? format, IFormatProvider? formatProvider) => "retry";

        public bool TryFormat(
            Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
        {
            const string value = "retry";
            if (destination.Length < 300)
            {
                charsWritten = 0;
                return false;
            }

            value.AsSpan().CopyTo(destination);
            charsWritten = value.Length;
            return true;
        }
    }

    private static Type DelaunayContext =>
        typeof(TetrahedralMesh).GetNestedType("DelaunayContext", BindingFlags.NonPublic)!;
}
