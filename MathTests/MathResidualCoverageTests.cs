namespace RedBolt.Tests.MathTests;

using System.Reflection;

using RedBolt.Math;

public sealed class MathResidualCoverageTests
{
    [Fact]
    public void Scalar_math_covers_remaining_edges_and_integer_codecs()
    {
        Assert.Equal(0.0f, MathDefs.Average(Array.Empty<float>()));
        Assert.Equal(2.0f, MathDefs.Average(new[] { 1.0f, 3.0f }));
        Assert.Equal(0.0, MathDefs.Average(Array.Empty<double>()));
        Assert.Equal(2.0, MathDefs.Average(new[] { 1.0, 3.0 }));

        Assert.Equal(7.0f, MathDefs.Smooth(3.0f, 7.0f, 0.0f, 1.0f));
        Assert.Equal(4.0f, MathDefs.RoundToNearestMultiple(4.1f, 4.0f));
        Assert.Equal(-8.0f, MathDefs.RoundToNearestMultiple(-6.0f, -4.0f));
        Assert.Equal(-4.0f, MathDefs.Wrap(-12.0f, -2.0f, 8.0f));

        Assert.Equal(0xc000, MathDefs.FloatToHalf(-2.0f));
        Assert.Equal(0, MathDefs.FloatToHalf(0.00000001f));
        Assert.Equal(0x7bff, MathDefs.FloatToHalf(float.MaxValue));
        Assert.Equal(-2.0f, MathDefs.HalfToFloat(0xc000));

        uint decoded = 0;
        uint offset = 0;
        Assert.False(MathDefs.DecodeVariableLength(ref decoded, ref offset, 0x80));
        Assert.True(MathDefs.DecodeVariableLength(ref decoded, ref offset, 0x01));
        Assert.Equal(128u, decoded);

        ulong decodedLong = 0;
        uint longOffset = 0;
        Assert.False(MathDefs.DecodeVariableLength(ref decodedLong, ref longOffset, 0x80));
        Assert.True(MathDefs.DecodeVariableLength(ref decodedLong, ref longOffset, 0x01));
        Assert.Equal(128UL, decodedLong);

        foreach (var value in new short[] { -2, -1, 0, 1, 2 })
            Assert.Equal(value, MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value)));
        foreach (var value in new long[] { -2, -1, 0, 1, 2 })
            Assert.Equal(value, MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value)));

        Assert.Equal(new Vector4f(2.0f, 4.0f, 6.0f, 8.0f),
            MathDefs.VectorLerp(Vector4f.Zero, new Vector4f(4, 8, 12, 16), new Vector4f(0.5f, 0.5f, 0.5f, 0.5f)));
        Assert.Equal(new Vector4f(-1, 2, -3, 4),
            MathDefs.VectorMin(new Vector4f(-1, 3, -3, 5), new Vector4f(0, 2, 0, 4)));
        Assert.Equal(new Vector4f(0, 3, 0, 5),
            MathDefs.VectorMax(new Vector4f(-1, 3, -3, 5), new Vector4f(0, 2, 0, 4)));
    }

    [Fact]
    public void Random_engine_covers_seed_normalization_and_small_shuffle_inputs()
    {
        var zeroSeed = new RandomEngine(0);
        Assert.Equal("1", zeroSeed.Save());
        Assert.Equal(0u, zeroSeed.GetUInt(0));
        Assert.Equal(4u, zeroSeed.GetUInt(4, 4));
        Assert.Equal(int.MinValue, zeroSeed.GetInt(int.MinValue, int.MinValue));

        var wrappedSeed = new RandomEngine(uint.MaxValue);
        Assert.InRange(wrappedSeed.GetDouble(), 0.0, 1.0);

        var engine = new RandomEngine(23);
        Span<int> empty = [];
        engine.Shuffle(empty);
        Span<int> one = stackalloc int[] { 9 };
        engine.Shuffle(one);
        Assert.Equal(9, one[0]);

        var pair = engine.GetStandardNormalFloatPair();
        Assert.True(float.IsFinite(pair.First));
        Assert.True(float.IsFinite(pair.Second));
        Assert.InRange(engine.GetDouble(-3.0, 3.0), -3.0, 3.0);
        Assert.InRange(engine.GetFloat(-3.0f, 3.0f), -3.0f, 3.0f);
        Assert.False(engine.GetBool(-1.0f));
    }

    [Fact]
    public void Vector4_and_signed_vector_paths_cover_remaining_vector_apis()
    {
        var vector2 = new Vector2f(1.0f, 0.0f);
        Assert.Equal(-90.0f, vector2.SignedAngle(Vector2f.Up), 4.0f);

        var vector4 = new Vector4f(-4.0f, 6.0f, -8.0f, 10.0f);
        Assert.Equal(new Vector4f(-2, 3, -4, 5), vector4 / 2.0f);
        Assert.Equal(new Vector4f(-2, 2, -2, 2), vector4 / new Vector4f(2, 3, 4, 5));
        Assert.Equal(MathF.Sqrt(216.0f), vector4.Length);
        Assert.Equal(216.0f, vector4.LengthSquared);
        Assert.Equal(4.0f, vector4.DotProduct(new Vector4f(1, 1, 1, 1)));
        Assert.Equal(28.0f, vector4.AbsDotProduct(new Vector4f(1, 1, 1, 1)));
        Assert.Equal(new Vector4f(4, 6, 8, 10), vector4.Abs);
        Assert.Equal(1.0f, new Vector4f(1, 0, 0, 0).ProjectOntoAxis(Vector3f.Right));

        for (var i = 0; i < 4; ++i)
            vector4[i] = i + 1;
        Assert.Equal(new[] { 1.0f, 2.0f, 3.0f, 4.0f }, vector4.Data().ToArray());
        Assert.Throws<IndexOutOfRangeException>(() => _ = vector4[4]);
        Assert.Throws<IndexOutOfRangeException>(() => vector4[-1] = 0);
        Assert.True(new Vector4f(float.NaN, 0, 0, 0).IsNaN);
        Assert.True(new Vector4f(0, float.PositiveInfinity, 0, 0).IsInf);
        Assert.True(vector4.ApproximatelyEquivalent(new Vector4f(1, 2, 3, 4)));
    }

    [Fact]
    public void Quaternion_native_slerp_covers_shortest_and_linear_fallback_paths()
    {
        Assert.False(OperatingSystem.IsBrowser());

        var identity = Quaternion.IDENTITY;
        var opposite = -identity;
        Assert.True(identity.Slerp(opposite, 0.5f).Equivalent(identity));

        var quarter = new Quaternion(90.0f, Vector3f.Up);
        var halfway = identity.Slerp(quarter, 0.5f);
        Assert.True(halfway.Equivalent(new Quaternion(45.0f, Vector3f.Up), 0.0001f));

        var negativeDot = (-new Quaternion(30.0f, Vector3f.Up));
        Assert.True(identity.Slerp(negativeDot, 0.5f).Equivalent(new Quaternion(15.0f, Vector3f.Up), 0.0001f));
        Assert.True(identity.Slerp(identity, 0.25f).Equivalent(identity));
    }

    [Fact]
    public void Sphere_merge_and_classification_cover_all_public_outcomes()
    {
        var undefined = new Sphere();
        undefined.Merge(new Sphere(Vector3f.Right, 2.0f));
        Assert.Equal(new Sphere(Vector3f.Right, 2.0f), undefined);

        var containing = new Sphere(Vector3f.Zero, 5.0f);
        containing.Merge(new Sphere(Vector3f.Right, 1.0f));
        Assert.Equal(new Sphere(Vector3f.Zero, 5.0f), containing);

        var contained = new Sphere(Vector3f.Zero, 1.0f);
        contained.Merge(new Sphere(Vector3f.Right, 5.0f));
        Assert.Equal(new Sphere(Vector3f.Right, 5.0f), contained);

        var overlapping = new Sphere(Vector3f.Zero, 2.0f);
        overlapping.Merge(new Sphere(new Vector3f(3, 0, 0), 2.0f));
        Assert.True(overlapping.Defined);
        Assert.True(overlapping.Radius > 2.0f);

        var outer = new Sphere(Vector3f.Zero, 2.0f);
        var inner = new Sphere(Vector3f.Zero, 1.0f);
        Assert.Equal(Intersection.Inside, outer.IsInside(inner));
        Assert.Equal(Intersection.Outside, outer.IsInside(new Sphere(new Vector3f(5, 0, 0), 1.0f)));
        Assert.Equal(Intersection.Intersects, outer.IsInside(new Sphere(new Vector3f(2, 0, 0), 2.0f)));
        Assert.Equal(Intersection.Inside, outer.IsInsideFast(inner));
        Assert.Equal(Intersection.Outside, outer.IsInsideFast(new Sphere(new Vector3f(5, 0, 0), 1.0f)));

        var emptyFacePolyhedron = new Polyhedron();
        emptyFacePolyhedron.Faces.Add([]);
        outer.Merge(emptyFacePolyhedron);
        Assert.True(outer.Defined);

        var disjoint = outer.Intersect(new Sphere(new Vector3f(10, 0, 0), 1.0f));
        Assert.False(disjoint.IsValid);
        var containedIntersection = outer.Intersect(new Sphere(Vector3f.Zero, 0.5f));
        Assert.False(containedIntersection.IsValid);
    }

    [Fact]
    public void Geometry_helpers_cover_constructor_and_intersection_edges()
    {
        var box = new BoundingBox(Vector3f.Zero, Vector3f.One);
        var polyhedron = new Polyhedron(box);
        Assert.False(polyhedron.Empty());
        Assert.Equal(6, polyhedron.Faces.Count);
        var copy = new Polyhedron(polyhedron);
        Assert.Equal(polyhedron.Faces.Count, copy.Faces.Count);
        Assert.Same(copy, copy.CopyAssign(polyhedron));

        var parallel = new Plane(Vector3f.Up, Vector3f.Zero).Intersect(new Plane(Vector3f.Up, Vector3f.Down));
        Assert.Equal(Vector3f.Zero, parallel.Direction);
        var threeParallel = new Plane(Vector3f.Up, Vector3f.Zero).Intersect(
            new Plane(Vector3f.Up, Vector3f.Down), new Plane(Vector3f.Up, new Vector3f(0, -2, 0)));
        Assert.True(threeParallel == Vector3f.Zero);

        var ray = new Ray(Vector3f.Zero, Vector3f.Right);
        Assert.Equal(ray, ray.Clone());
        Assert.Equal(ray.GetHashCode(), ray.GetHashCode());
    }

    [Fact]
    public void Area_allocator_covers_vertical_growth_and_negative_dimensions()
    {
        var allocator = new AreaAllocator(2, 2, 4, 4);
        var x = 0;
        var y = 0;
        Assert.True(allocator.Allocate(3, 2, ref x, ref y));
        Assert.True(allocator.Allocate(4, 2, ref x, ref y));
        Assert.Equal(4, allocator.Width);
        Assert.Equal(4, allocator.Height);

        var zero = new AreaAllocator(2, 2, false);
        Assert.True(zero.Allocate(-1, -1, ref x, ref y));
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Inverse_kinematics_covers_empty_early_returns_and_previous_transforms()
    {
        var exposed = new ExposedIKChain();
        Assert.Throws<ArgumentOutOfRangeException>(() => exposed.StoreAndRestore());

        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var second = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var third = new IKNode(new Vector3f(2, 0, 0), Quaternion.IDENTITY);
        exposed.AddNode(first);
        exposed.AddNode(second);
        exposed.AddNode(third);
        exposed.UpdateLengths();
        Assert.Null(exposed.FindSegment(new IKNode()));
        exposed.StoreAndRestore();

        var segment = exposed.GetSegments()[0];
        segment.UpdateRotationInNodes(fromPrevious: true, isLastSegment: false);
        segment.UpdateRotationInNodes(fromPrevious: true, isLastSegment: true);
        segment.UpdateRotationInNodes(fromPrevious: false, isLastSegment: false);
        segment.UpdateRotationInNodes(fromPrevious: false, isLastSegment: true);
        segment.Twist(10.0f, false);
        segment.Twist(10.0f, true);
        Assert.True(first.RotationDirty);

        var fabrik = new IKFabrikChain();
        fabrik.Solve(Vector3f.Right, new IKSettings());
        fabrik.AddNode(new IKNode(Vector3f.Zero, Quaternion.IDENTITY));
        fabrik.Solve(Vector3f.Zero, new IKSettings());

        var spine = new IKSpineChain();
        spine.Solve(Vector3f.Right, Vector3f.Right, 90.0f, new IKSettings());
        spine.AddNode(new IKNode(Vector3f.Zero, Quaternion.IDENTITY));
        spine.Twist(20.0f, new IKSettings());
        spine.Solve(Vector3f.Right, Vector3f.Right, 90.0f, new IKSettings());
    }

    [Fact]
    public void Inverse_kinematics_covers_eye_world_transform_spine_twist_and_rotation_modes()
    {
        var root = new IKNode(Vector3f.Zero, new Quaternion(30.0f, Vector3f.Up));
        var eye = new IKEyeChain();
        eye.Initialize(root);
        eye.SetWorldEyeTransform(Vector3f.Right, Vector3f.Forward);
        var lookRotation = eye.SolveLookAt(Vector3f.Up, new IKSettings { MaxIterations = 2 });
        Assert.True(float.IsFinite(lookRotation.W));

        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var middle = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var end = new IKNode(new Vector3f(2, 0, 0), Quaternion.IDENTITY);
        var spine = new IKSpineChain();
        spine.AddNode(first);
        spine.AddNode(middle);
        spine.AddNode(end);
        spine.UpdateLengths();
        spine.Solve(Vector3f.Up, Vector3f.Right, 90.0f, new IKSettings());
        spine.Twist(15.0f, new IKSettings());
        spine.Solve(Vector3f.Up, Vector3f.Right, 90.0f, new IKSettings(), _ => 0.0f);
        Assert.True(first.RotationDirty);

        var trig = new IKTrigonometricChain();
        trig.Initialize(new IKNode(Vector3f.Zero, Quaternion.IDENTITY),
            new IKNode(Vector3f.Right, Quaternion.IDENTITY),
            new IKNode(new Vector3f(2, 0, 0), Quaternion.IDENTITY));
        trig.UpdateLengths();
        trig.Solve(Vector3f.Up, Vector3f.Right, Vector3f.Up, 0.0f, 180.0f);
        Assert.True(trig.GetCurrentChainRotation().LengthSquared > 0.0f);
    }

    [Fact]
    public void Tetrahedral_public_types_cover_default_edges_adjacency_and_faces()
    {
        var triangle = new TetrahedralMeshSurfaceTriangle();
        triangle.Neighbors[1] = 17;
        Assert.True(triangle.HasNeighbor(17));
        Assert.False(triangle.HasNeighbor(18));

        var defaultEdge = new TetrahedralMeshSurfaceEdge();
        var orderedEdge = new TetrahedralMeshSurfaceEdge(2, 3, 0, 0);
        Assert.True(orderedEdge > defaultEdge);
        Assert.False(defaultEdge > orderedEdge);

        var surface = new TetrahedralMeshSurface();
        surface.Faces.Add(triangle);
        Assert.Equal(1u, surface.Size);
        Assert.False(surface.CalculateAdjacency());

        var closed = new TetrahedralMeshSurface();
        foreach (var indices in new[]
        {
            new uint[] { 1, 2, 3 },
            new uint[] { 0, 3, 2 },
            new uint[] { 0, 1, 3 },
            new uint[] { 0, 2, 1 }
        })
        {
            var face = new TetrahedralMeshSurfaceTriangle();
            face.Indices[0] = indices[0];
            face.Indices[1] = indices[1];
            face.Indices[2] = indices[2];
            closed.Faces.Add(face);
        }

        Assert.True(closed.CalculateAdjacency());
        Assert.True(closed.IsClosedSurface());
        Assert.Equal(4u, closed.Size);

        var tetra = new Tetrahedron();
        tetra.Indices[0] = 0;
        tetra.Indices[1] = 1;
        tetra.Indices[2] = 2;
        tetra.Indices[3] = 3;
        tetra.Neighbors[2] = 17;
        Assert.Equal(2u, tetra.GetNeighborFaceIndex(17));
        Assert.True(tetra.HasNeighbor(17));
        Assert.Equal(4u, tetra.GetNeighborFaceIndex(99));
        for (uint i = 0; i < 4; ++i)
        {
            var face = tetra.GetTriangleFace(i, 5, i);
            Assert.Equal(5u, face.TetIndex);
            Assert.Equal(i, face.TetFace);
        }
    }

    [Fact]
    public void Tetrahedral_mesh_public_outer_and_empty_interpolation_paths_are_reached()
    {
        var empty = new TetrahedralMesh();
        uint emptyHint = 23;
        Assert.Equal(Vector4f.Zero, empty.GetInterpolationFactors(Vector3f.Zero, ref emptyHint));

        var mesh = new TetrahedralMesh();
        mesh.Define(
        [
            new Vector3f(0, 0, 0),
            new Vector3f(1, 0, 0),
            new Vector3f(0, 1, 0),
            new Vector3f(0, 0, 1),
            new Vector3f(0.2f, 0.2f, 0.2f)
        ]);

        Assert.True(mesh.Tetrahedrons.Count > 0);
        var outerIndex = mesh.NumInnerTetrahedrons;
        if (outerIndex < mesh.Tetrahedrons.Count)
        {
            var outer = mesh.GetOuterBarycentricCoords(outerIndex, mesh.Vertices[(int)mesh.Tetrahedrons[(int)outerIndex].Indices[0]]);
            Assert.True(float.IsFinite(outer.X));
            var hint = outerIndex + 100;
            _ = mesh.GetInterpolationFactors(Vector3f.Zero, ref hint);
            Assert.True(hint < mesh.Tetrahedrons.Count);
        }
    }

    [Fact]
    public void Scalar_value_types_cover_remaining_operators_and_constructors()
    {
        var color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        Assert.Equal(color, new Color(color));
        Assert.Equal(color, new Color(new[] { color.R, color.G, color.B, color.A }));
        var packed = new Color(0x80402010u);
        packed.FromUInt(0x01020304u);
        Assert.Equal(4.0f / 255.0f, packed.R);
        _ = color.Average();
        _ = color.Luma();

        var vector2 = new Vector2f(2, 3);
        Assert.Equal(1.0f, vector2.CrossProduct(new Vector2f(1, 2)));
        Assert.Equal(new Vector2f(-3, 2), vector2.GetOrthogonalCounterClockwise());
        _ = ((ICloneable)new Vector4f(1, 2, 3, 4)).Clone();

        var range = new NumericRange<int>(1, 3);
        Assert.True(range == new NumericRange<int>(1, 3));
        Assert.True(range != new NumericRange<int>(1, 4));

        var wrapped = new WrappedScalar<float>(1, 0, 2);
        var sameWrapped = new WrappedScalar<float>(1, 0, 2);
        Assert.True(wrapped != new WrappedScalar<float>(2, 0, 2));
        Assert.False(wrapped != sameWrapped);
        Assert.True(new WrappedScalarRange<float>(1, 0, 2) != new WrappedScalarRange<float>(2, 0, 2));
        _ = new WrappedScalar<float>();

        var matrix = Matrix3.Identity;
        Assert.False(matrix != Matrix3.Identity);
        var ray = new Ray(Vector3f.Zero, Vector3f.Right);
        _ = ((ICloneable)ray).Clone();
        _ = new IntRectIterator(new IntRect(0, 0, 2, 2), Vector2i.Zero).GetHashCode();

        var parallelA = new Plane(Vector3f.Up, Vector3f.Zero);
        var parallelB = new Plane(Vector3f.Up, Vector3f.One);
        _ = parallelA.Intersect(parallelB);

        var allocator = new AreaAllocator(2, 2, 2, 4, false);
        typeof(AreaAllocator).GetField("_doubleWidth", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(allocator, false);
        var x = 0;
        var y = 0;
        Assert.True(allocator.Allocate(2, 3, ref x, ref y));
        var split = new AreaAllocator(10, 10, 10, 10, false);
        var freeAreas = typeof(AreaAllocator).GetField("_freeAreas", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var areas = (List<IntRect>)freeAreas.GetValue(split)!;
        areas.Clear();
        areas.Add(new IntRect(0, 0, 10, 10));
        Assert.True((bool)typeof(AreaAllocator).GetMethod("SplitRect", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(split, [0, new IntRect(2, 2, 4, 4)])!);

        var polyhedron = new Polyhedron(new BoundingBox(Vector3f.Zero, Vector3f.One));
        typeof(Polyhedron).GetMethod("SetFace", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(uint), typeof(Vector3f), typeof(Vector3f), typeof(Vector3f)], modifiers: null)!
            .Invoke(polyhedron, [0u, Vector3f.Zero, Vector3f.Right, Vector3f.Up]);
    }

    [Fact]
    public void Inverse_kinematics_and_surface_edge_paths_are_exercised()
    {
        var begin = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var end = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        begin.PreviousPosition = new Vector3f(0, 0, 0);
        end.PreviousPosition = new Vector3f(0, 1, 0);
        var segment = new IKNodeSegment { BeginNode = begin, EndNode = end };
        var continuous = new IKSettings { ContinuousRotations = true };
        var original = new IKSettings { ContinuousRotations = false };
        _ = segment.CalculateRotation(continuous);
        _ = segment.CalculateRotation(original);

        var surface = new TetrahedralMeshSurface();
        foreach (var indices in new[]
        {
            new uint[] { 0, 1, 2 }, new uint[] { 0, 2, 3 }
        })
        {
            var face = new TetrahedralMeshSurfaceTriangle();
            face.Indices[0] = indices[0];
            face.Indices[1] = indices[1];
            face.Indices[2] = indices[2];
            surface.Faces.Add(face);
        }
        var firstFace = surface.Faces[0];
        firstFace.Neighbors[0] = 5;
        surface.Faces[0] = firstFace;
        Assert.False(surface.CalculateAdjacency());
    }

    [Fact]
    public void Scalar_geometry_and_bisection_edges_are_exercised()
    {
        Assert.True(float.IsFinite(EaseMath.ElasticInOut(0.5f, 0.0f)));

        var chain = new IKSpineChain();
        chain.AddNode(new IKNode(Vector3f.Zero, Quaternion.IDENTITY));
        chain.AddNode(new IKNode(Vector3f.Right, Quaternion.IDENTITY));
        chain.Solve(Vector3f.Right, Vector3f.Right, 0.0f, new IKSettings());

        var vector = Vector3f.Lerp(Vector3f.Zero, Vector3f.One, Vector3f.One * 0.5f);
        Assert.Equal(new Vector3f(0.5f, 0.5f, 0.5f), vector);

        var first = new IntRectIterator(new IntRect(0, 0, 1, 1), Vector2i.Zero);
        var second = new IntRectIterator(new IntRect(0, 0, 1, 1), Vector2i.Zero);
        Assert.True(first == second);
        Assert.False(first != second);

        var intersection = new Plane(Vector3f.Right, Vector3f.Zero)
            .Intersect(new Plane(Vector3f.Up, Vector3f.Zero));
        Assert.NotEqual(Vector3f.Zero, intersection.Direction);

        var forcedDeterminant = typeof(Plane).GetMethod(
            "Intersect", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(Plane), typeof(bool)], modifiers: null)!;
        var forced = (Ray)forcedDeterminant.Invoke(
            new Plane(Vector3f.Right, Vector3f.Zero),
            [new Plane(Vector3f.Up, Vector3f.Zero), true])!;
        Assert.Equal(Vector3f.Zero, forced.Origin);
        Assert.NotEqual(Vector3f.Zero, forced.Direction);

        var frustum = new Frustum();
        frustum.Planes[0] = new Plane(-Vector3f.Right, Vector3f.Zero);
        frustum.Planes[1] = new Plane(Vector3f.Up, new Vector3f(0.0f, -1.0f, 0.0f));
        var miss = new Ray(new Vector3f(100.0f, 0.0f, 0.0f), new Vector3f(1.0f, -1.0f, 0.0f))
            .HitDistance(frustum);
        Assert.Equal(float.PositiveInfinity, miss);
    }

    [Fact]
    public void Surface_adjacency_rejects_a_second_link_to_the_same_faces()
    {
        var surface = new TetrahedralMeshSurface();
        foreach (var indices in new[]
        {
            new uint[] { 1, 2, 3 },
            new uint[] { 0, 3, 2 },
            new uint[] { 0, 1, 3 },
            new uint[] { 0, 2, 1 }
        })
        {
            var face = new TetrahedralMeshSurfaceTriangle();
            face.Indices[0] = indices[0];
            face.Indices[1] = indices[1];
            face.Indices[2] = indices[2];
            surface.Faces.Add(face);
        }

        Assert.True(surface.CalculateAdjacency());
        Assert.False(surface.CalculateAdjacency());
    }

    private sealed class ExposedIKChain : IKChain
    {
        public void StoreAndRestore()
        {
            StorePreviousTransforms();
            RestorePreviousTransforms();
        }
    }
}
