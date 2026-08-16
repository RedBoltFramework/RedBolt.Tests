// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests.MathTests;

using System.Reflection;
using System.Runtime.InteropServices;

using RedBolt;
using RedBolt.Core;
using RedBolt.Math;

public sealed class MathRemainingCoverageTests
{
    [Fact]
    public void Wrapped_scalar_covers_empty_reverse_and_small_range_boundaries()
    {
        var empty = new WrappedScalarRange<float>(4.0f, 4.0f, 0.0f, 10.0f, 0);
        Assert.True(empty.IsEmpty());
        Assert.Equal(0.0f, empty.Min());
        Assert.Equal(10.0f, empty.Max());
        Assert.False(empty.ContainsInclusive(-1.0f));
        Assert.False(empty.ContainsExclusive(4.0f));

        var reverse = new WrappedScalarRange<float>(2.0f, 8.0f, 0.0f, 10.0f, -1);
        Assert.True(reverse.ContainsInclusive(0.0f));
        Assert.True(reverse.ContainsInclusive(10.0f));
        Assert.False(reverse.ContainsExcludingBegin(2.0f));
        Assert.False(reverse.ContainsExcludingEnd(8.0f));

        var scalar = new WrappedScalar<float>(5.0f, 0.0f, 0.0f);
        Assert.Equal(0.0f, scalar.UpdateWrapped(3.0f).Begin());
        scalar = new WrappedScalar<float>(5.0f, 0.0f, 10.0f);
        Assert.Equal(5.0f, scalar.UpdateWrapped(-0.0f).End());
        Assert.Equal(8.0f, scalar.UpdateClamped(3.0f).End());
        Assert.NotEqual(scalar, new WrappedScalar<float>(6.0f, 0.0f, 10.0f));
        Assert.False(scalar.Equals((object?)new WrappedScalar<int>(6, 0, 10)));
    }

    [Fact]
    public void Spline_remaining_mutation_and_interpolation_branches_are_deterministic()
    {
        using var spline = new Spline(InterpolationMode.LinearCurve);
        spline.AddKnot(new Variant(1.0f));
        spline.AddKnot(new Variant(3.0f));
        spline.SetKnot(new Variant(2.0f), 0);
        spline.SetKnot(new Variant(new Vector3f(4.0f, 5.0f, 6.0f)), 1);
        Assert.Equal(2.0f, spline.GetKnot(0).GetFloat());

        Assert.False(spline.Equals((object?)null));
        Assert.False(spline.Equals(new object()));

        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Float,
            new Variant(0.0f), new Variant(1.0f), new Variant(2.0f));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector2,
            new Variant(Vector2f.Zero), new Variant(Vector2f.One), new Variant(new Vector2f(2.0f, 2.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector3,
            new Variant(Vector3f.Zero), new Variant(Vector3f.One), new Variant(new Vector3f(2.0f, 2.0f, 2.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector4,
            new Variant(Vector4f.Zero), new Variant(Vector4f.One), new Variant(new Vector4f(2.0f, 2.0f, 2.0f, 2.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Color,
            new Variant(Color.Black), new Variant(Color.Gray), new Variant(Color.White));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Double,
            new Variant(0.0), new Variant(1.0), new Variant(2.0));

        using var shortCatmull = new Spline(
            new VariantVector { new(0.0f), new(1.0f), new(2.0f) },
            InterpolationMode.CatmullRomCurve);
        using var noCatmullPoint = shortCatmull.GetPoint(0.5f);
        Assert.Equal(VariantType.None, noCatmullPoint.Type);

        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Float,
            new Variant(0.0f), new Variant(1.0f), new Variant(2.0f), new Variant(3.0f));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector2,
            new Variant(Vector2f.Zero), new Variant(Vector2f.One),
            new Variant(new Vector2f(2.0f, 2.0f)), new Variant(new Vector2f(3.0f, 3.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector3,
            new Variant(Vector3f.Zero), new Variant(Vector3f.One),
            new Variant(new Vector3f(2.0f, 2.0f, 2.0f)), new Variant(new Vector3f(3.0f, 3.0f, 3.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector4,
            new Variant(Vector4f.Zero), new Variant(Vector4f.One),
            new Variant(new Vector4f(2.0f, 2.0f, 2.0f, 2.0f)),
            new Variant(new Vector4f(3.0f, 3.0f, 3.0f, 3.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Color,
            new Variant(Color.Black), new Variant(Color.Gray), new Variant(Color.White),
            new Variant(new Color(0.25f, 0.25f, 0.25f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Double,
            new Variant(0.0), new Variant(1.0), new Variant(2.0), new Variant(3.0));

        var method = typeof(Spline).GetMethod(
            "LinearInterpolation",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(VariantVector), typeof(float) },
            modifiers: null)!;
        using var emptyKnots = new VariantVector();
        using var emptyResult = (Variant)method.Invoke(spline, new object?[] { emptyKnots, 0.5f })!;
        Assert.Equal(VariantType.None, emptyResult.Type);
    }

    [Fact]
    public void Sphere_remaining_merge_equality_and_corner_branches_are_deterministic()
    {
        var equalCenterDifferentRadius = new Sphere(Vector3f.Zero, 1.0f);
        var differentRadius = new Sphere(Vector3f.Zero, 2.0f);
        Assert.False(equalCenterDifferentRadius == differentRadius);
        Assert.True(equalCenterDifferentRadius != differentRadius);
        Assert.False(equalCenterDifferentRadius.Equals((object?)null));

        var polyhedron = new Polyhedron();
        polyhedron.Faces.Add(new List<Vector3f>());
        polyhedron.AddFace(Vector3f.Zero, Vector3f.Right, Vector3f.Up);
        var merged = new Sphere();
        merged.Merge(polyhedron);
        Assert.True(merged.Defined);

        var box = new BoundingBox(-1.0f, 1.0f);
        foreach (var center in new[]
        {
            new Vector3f(-2.0f, 0.0f, 0.0f),
            new Vector3f(2.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, -2.0f, 0.0f),
            new Vector3f(0.0f, 2.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, -2.0f),
            new Vector3f(0.0f, 0.0f, 2.0f)
        })
        {
            var outside = new Sphere(center, 0.5f);
            Assert.Equal(Intersection.Outside, outside.IsInside(box));
            Assert.Equal(Intersection.Outside, outside.IsInsideFast(box));
        }

        Assert.Equal(Intersection.Outside, new Sphere(new Vector3f(2.0f, 0.0f, 0.0f), 1.0f).IsInsideFast(box));

        // Each radius is chosen to pass the preceding box corners and stop at
        // the next one in Sphere.IsInside's explicit corner walk.
        foreach (var (center, radius) in new[]
        {
            (new Vector3f(0.0f, 0.0f, 0.0f), 1.5f),
            (new Vector3f(-0.9f, -0.9f, -0.9f), 1.0f),
            (new Vector3f(-0.9f, -0.9f, 0.0f), 2.5f),
            (new Vector3f(0.9f, -0.9f, 0.0f), 2.5f),
            (new Vector3f(0.0f, 0.0f, -0.9f), 2.2f),
            (new Vector3f(0.0f, 0.9f, -0.9f), 2.5f),
            (new Vector3f(-0.9f, 0.0f, -0.9f), 2.5f),
            (new Vector3f(-0.9f, -0.9f, -0.9f), 3.0f)
        })
            Assert.Equal(Intersection.Intersects, new Sphere(center, radius).IsInside(box));
    }

    [Fact]
    public void Ray_remaining_box_face_and_geometry_branches_are_deterministic()
    {
        var box = new BoundingBox(-1.0f, 1.0f);

        Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f),
            new Ray(new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Left).HitDistanceAndNormal(box).Normal);
        foreach (var origin in new[]
        {
            new Vector3f(2.0f, -2.0f, 0.0f),
            new Vector3f(2.0f, 2.0f, 0.0f),
            new Vector3f(2.0f, 0.0f, -2.0f),
            new Vector3f(2.0f, 0.0f, 2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Left).HitDistance(box)));

        Assert.Equal(new Vector3f(0.0f, 1.0f, 0.0f),
            new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down).HitDistanceAndNormal(box).Normal);
        foreach (var origin in new[]
        {
            new Vector3f(-2.0f, 2.0f, 0.0f),
            new Vector3f(2.0f, 2.0f, 0.0f),
            new Vector3f(0.0f, 2.0f, -2.0f),
            new Vector3f(0.0f, 2.0f, 2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Down).HitDistance(box)));

        Assert.Equal(new Vector3f(0.0f, 0.0f, 1.0f),
            new Ray(new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Back).HitDistanceAndNormal(box).Normal);
        foreach (var origin in new[]
        {
            new Vector3f(-2.0f, 0.0f, 2.0f),
            new Vector3f(2.0f, 0.0f, 2.0f),
            new Vector3f(0.0f, -2.0f, 2.0f),
            new Vector3f(0.0f, 2.0f, 2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Back).HitDistance(box)));

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 4.0f));
        Assert.True(new Ray(new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward).HitDistance(frustum) > 0.0f);
        Assert.True(float.IsPositiveInfinity(
            new Ray(new Vector3f(5.0f, 0.0f, 2.0f), Vector3f.Right).HitDistance(frustum)));

        const int stride = 20;
        Span<byte> vertices = stackalloc byte[stride * 3];
        Write(vertices, 0, new Vector3f(0.0f, 0.0f, 0.0f));
        Write(vertices, 12, Vector2f.Zero);
        Write(vertices, stride, new Vector3f(0.0f, 1.0f, 0.0f));
        Write(vertices, stride + 12, Vector2f.Up);
        Write(vertices, stride * 2, new Vector3f(1.0f, 0.0f, 0.0f));
        Write(vertices, stride * 2 + 12, Vector2f.Right);

        var frontRay = new Ray(new Vector3f(0.25f, 0.25f, -1.0f), Vector3f.Forward);
        var backRay = new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Back);
        Assert.False(frontRay.InsideGeometry(vertices, stride, 0u, 3u));
        Assert.True(backRay.InsideGeometry(vertices, stride, 0u, 3u));
        Assert.False(frontRay.InsideGeometry(vertices, stride, 0u, 0u));

        Span<byte> indices16 = stackalloc byte[sizeof(ushort) * 3];
        Write(indices16, 0, (ushort)0);
        Write(indices16, sizeof(ushort), (ushort)1);
        Write(indices16, sizeof(ushort) * 2, (ushort)2);
        Assert.False(frontRay.InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 3u));
        Assert.True(backRay.InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 3u));
        Assert.False(frontRay.InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 0u));

        Span<byte> indices32 = stackalloc byte[sizeof(uint) * 3];
        Write(indices32, 0, 0u);
        Write(indices32, sizeof(uint), 1u);
        Write(indices32, sizeof(uint) * 2, 2u);
        Assert.False(frontRay.InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 3u));
        Assert.True(backRay.InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 3u));
        Assert.False(frontRay.InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 0u));
    }

    [Fact]
    public void Color_remaining_channel_bounds_and_clip_branches_are_deterministic()
    {
        Assert.True(new Color(0.2f, 0.4f, 0.6f, 0.8f) != new Color(0.2f, 0.5f, 0.6f, 0.8f));
        Assert.True(new Color(0.2f, 0.4f, 0.6f, 0.8f) != new Color(0.2f, 0.4f, 0.7f, 0.8f));
        Assert.True(new Color(0.2f, 0.4f, 0.6f, 0.8f) != new Color(0.2f, 0.4f, 0.6f, 0.9f));
        Assert.False(new Color(0.2f, 0.4f, 0.6f, 0.8f).Equals((object?)null));

        foreach (var color in new[]
        {
            new Color(0.8f, 0.2f, 0.4f),
            new Color(0.4f, 0.2f, 0.8f),
            new Color(0.2f, 0.8f, 0.4f),
            new Color(0.4f, 0.8f, 0.2f)
        })
        {
            color.Bounds(out var min, out var max);
            Assert.Equal(0.2f, min);
            Assert.Equal(0.8f, max);
            _ = color.MaxRGB();
            _ = color.MinRGB();
        }

        var clippedBounds = new Color(-2.0f, 2.0f, 0.5f);
        clippedBounds.Bounds(out var clippedMin, out var clippedMax, clipped: true);
        Assert.Equal(0.0f, clippedMin);
        Assert.Equal(1.0f, clippedMax);

        var clipped = new Color(-1.0f, 2.0f, 0.5f, -2.0f);
        clipped.Clip(clipAlpha: true);
        Assert.Equal(new Color(0.0f, 1.0f, 0.5f, 0.0f), clipped);
        clipped = new Color(0.5f, 0.5f, 0.5f, 2.0f);
        clipped.Clip(clipAlpha: true);
        Assert.Equal(1.0f, clipped.A);

        var zeroGreenMask = new Color.ChannelMask(0xff, 0, 0xff00, 0);
        var zeroGreen = new Color(0xffffffffu, zeroGreenMask);
        Assert.Equal(0.0f, zeroGreen.G);
        var nonzeroGreenMask = new Color.ChannelMask(0, 0xff, 0, 0);
        Assert.Equal(1.0f, new Color(0xffffffffu, nonzeroGreenMask).G);
    }

    [Fact]
    public void IntRect_remaining_merge_object_and_iterator_branches_are_deterministic()
    {
        var merged = new IntRect(1, 1, 3, 3);
        merged.Merge(new IntRect(0, 0, 4, 4));
        Assert.Equal(new IntRect(0, 0, 4, 4), merged);

        var emptyWidth = new IntRect(0, 0, 0, 2);
        emptyWidth.Merge(new IntRect(1, 2, 3, 4));
        Assert.Equal(new IntRect(1, 2, 3, 4), emptyWidth);
        var emptyHeight = new IntRect(0, 0, 2, 0);
        emptyHeight.Merge(new IntRect(1, 2, 3, 4));
        Assert.Equal(new IntRect(1, 2, 3, 4), emptyHeight);

        var valid = new IntRect(1, 1, 4, 4);
        valid.Merge(new IntRect(2, 2, 3, 3));
        Assert.Equal(new IntRect(1, 1, 4, 4), valid);
        valid.Merge(new IntRect(0, 0, 5, 5));
        Assert.Equal(new IntRect(0, 0, 5, 5), valid);

        Assert.False(valid.Equals((object?)null));
        var iterator = valid.Begin();
        Assert.True(iterator.Equals((object)iterator));
        Assert.False(iterator.Equals((object?)null));
    }

    [Fact]
    public void AreaAllocator_remaining_growth_and_zero_size_branches_are_deterministic()
    {
        var allocator = new AreaAllocator(2, 2, 4, 4);
        var x = 0;
        var y = 0;
        Assert.True(allocator.Allocate(1, 1, ref x, ref y));
        Assert.True(allocator.Allocate(2, 1, ref x, ref y));
        Assert.Equal(2, allocator.Width);
        Assert.True(allocator.Allocate(4, 1, ref x, ref y));
        Assert.Equal(4, allocator.Height);

        var negative = new AreaAllocator(2, 2);
        Assert.True(negative.Allocate(-1, -1, ref x, ref y));

        var precise = new AreaAllocator(4, 4, fastMode: false);
        Assert.True(precise.Allocate(1, 1, ref x, ref y));
        Assert.True(precise.Allocate(2, 2, ref x, ref y));
        Assert.True(precise.Allocate(1, 1, ref x, ref y));
    }

    [Fact]
    public void MathDefs_remaining_scalar_encoding_and_vector_branches_are_deterministic()
    {
        Assert.Equal(-1.0f, MathDefs.Sign(-1.0f));
        Assert.Equal(0.0f, MathDefs.Sign(0.0f));
        Assert.Equal(1.0f, MathDefs.Sign(1.0f));
        Assert.Equal(10.0f, MathDefs.Smooth(0.0f, 10.0f, 0.0f, 1.0f));
        Assert.Equal(9.0f, MathDefs.AbsMod(-1.0f, 10.0f));

        Assert.Equal(0.0f, MathDefs.Average(Array.Empty<float>()));
        Assert.Equal(2.0f, MathDefs.Average(new[] { 1.0f, 3.0f }));
        Assert.Equal(0.0, MathDefs.Average(Array.Empty<double>()));
        Assert.Equal(2.0, MathDefs.Average(new[] { 1.0, 3.0 }));

        Assert.Equal(4.0f, MathDefs.RoundToNearestMultiple(4.0f, 4.0f));
        Assert.Equal(8.0f, MathDefs.RoundToNearestMultiple(6.0f, 4.0f));
        Assert.Equal(-8.0f, MathDefs.RoundToNearestMultiple(-6.0f, 4.0f));

        Span<byte> encoded32 = stackalloc byte[5];
        Assert.Equal(1u, MathDefs.EncodeVariableLength(0u, encoded32));
        Assert.Equal(5u, MathDefs.EncodeVariableLength(uint.MaxValue, encoded32));
        Span<byte> encoded64 = stackalloc byte[10];
        Assert.Equal(1u, MathDefs.EncodeVariableLength(0ul, encoded64));
        Assert.Equal(10u, MathDefs.EncodeVariableLength(ulong.MaxValue, encoded64));

        var min = MathDefs.VectorMin(
            new Vector4f(1.0f, 3.0f, 5.0f, 7.0f),
            new Vector4f(2.0f, 2.0f, 6.0f, 6.0f));
        var max = MathDefs.VectorMax(
            new Vector4f(1.0f, 3.0f, 5.0f, 7.0f),
            new Vector4f(2.0f, 2.0f, 6.0f, 6.0f));
        Assert.Equal(new Vector4f(1.0f, 2.0f, 5.0f, 6.0f), min);
        Assert.Equal(new Vector4f(2.0f, 3.0f, 6.0f, 7.0f), max);
    }

    [Fact]
    public void BoundingBox_remaining_projection_sphere_and_object_branches_are_deterministic()
    {
        var box = new BoundingBox(-1.0f, 1.0f);
        var sameMinDifferentMax = new BoundingBox(box.Min, new Vector3f(2.0f, 1.0f, 1.0f));
        Assert.True(box != sameMinDifferentMax);
        Assert.False(box.Equals((object?)null));
        Assert.False(box.Equals(new object()));

        var negativeDepth = new BoundingBox(
            new Vector3f(-1.0f, -1.0f, -2.0f), new Vector3f(1.0f, 1.0f, -1.0f));
        Assert.True(negativeDepth.Projected(Matrix4.Identity).Defined);

        foreach (var center in new[]
        {
            new Vector3f(-2.0f, -2.0f, -2.0f),
            new Vector3f(2.0f, 2.0f, 2.0f),
            new Vector3f(-2.0f, 2.0f, -2.0f),
            new Vector3f(2.0f, -2.0f, 2.0f)
        })
        {
            var sphere = new Sphere(center, 0.5f);
            Assert.Equal(Intersection.Outside, box.IsInside(sphere));
            Assert.Equal(Intersection.Outside, box.IsInsideFast(sphere));
        }

        Assert.Equal(Intersection.Inside, box.IsInside(new Sphere(Vector3f.Zero, 0.5f)));
        Assert.Equal(Intersection.Intersects, box.IsInside(new Sphere(Vector3f.Zero, 2.0f)));
    }

    [Fact]
    public void Matrix4_remaining_operators_accessors_and_decomposition_are_deterministic()
    {
        var matrix = new Matrix4(
            2, 3, 4, 5,
            6, 7, 8, 9,
            10, 11, 13, 14,
            15, 16, 17, 19);
        var copy = new Matrix4(matrix);
        Assert.Equal(matrix, copy);
        Assert.True(matrix == copy);
        copy.M00 += 0.5f;
        Assert.True(matrix != copy);

        var fromMatrix3 = new Matrix4(Matrix3.Identity);
        Assert.Equal(1.0f, fromMatrix3.M33);
        Assert.True(new Vector3f(14.0f / 67.0f, 30.0f / 67.0f, 48.0f / 67.0f)
            .ApproximatelyEquivalent(matrix * new Vector3f(1, 1, 1), 0.00001f));
        Assert.Equal(
            new Vector4f(40, 80, 127, 174),
            matrix * new Vector4f(1, 2, 3, 4));

        var other = Matrix4.Identity;
        Assert.Equal(matrix + other, new Matrix4(
            3, 3, 4, 5,
            6, 8, 8, 9,
            10, 11, 14, 14,
            15, 16, 17, 20));
        Assert.Equal(matrix - other, new Matrix4(
            1, 3, 4, 5,
            6, 6, 8, 9,
            10, 11, 12, 14,
            15, 16, 17, 18));
        Assert.Equal(matrix * 2.0f, 2.0f * matrix);
        _ = matrix * other;
        _ = matrix * new Matrix3x4(Matrix3.Identity);

        matrix.SetTranslation(new Vector3f(20, 21, 22));
        matrix.SetRotation(Matrix3.Identity);
        matrix.SetScale(new Vector3f(2, 3, 4));
        matrix.SetScale(2.0f);
        Assert.Equal(new Matrix3(2, 0, 0, 0, 2, 0, 0, 0, 2), matrix.Matrix3);
        Assert.Equal(new Vector3f(20, 21, 22), matrix.Translation);
        Assert.Equal(new Vector3f(2, 2, 2), matrix.Scale);
        Assert.Equal(new Vector3f(2, 2, 2), matrix.SignedScale(Matrix3.Identity));
        Assert.Equal(new Quaternion(Matrix3.Identity), matrix.Rotation);
        _ = matrix.RotationMatrix;
        _ = matrix.Transpose;

        Assert.True(matrix.ApproximatelyEquivalent(new Matrix4(matrix)));
        var approximate = new Matrix4(matrix);
        approximate.M00 += MathDefs.Epsilon * 2.0f;
        Assert.False(matrix.ApproximatelyEquivalent(approximate));

        matrix.Decompose(out var translation, out var rotation, out var scale);
        Assert.Equal(new Vector3f(20, 21, 22), translation);
        Assert.Equal(new Vector3f(2, 2, 2), scale);
        _ = rotation;
        var reflected = Matrix4.Identity;
        reflected.SetScale(new Vector3f(-2, 3, 4));
        reflected.Decompose(out _, out _, out var reflectedScale);
        Assert.True(reflectedScale.X < 0.0f);
        _ = matrix.Determinant;
        _ = matrix.Inverse;

        Assert.Equal(0.0f, matrix.Element(2, 0));
        Assert.Equal(matrix.Row(1), new Vector4f(0, 2, 0, 21));
        Assert.Equal(matrix.Column(2), new Vector4f(0, 0, 2, 17));
        Assert.False(matrix.IsNaN);
        Assert.False(matrix.IsInf);
        var nan = matrix;
        nan.M00 = float.NaN;
        Assert.True(nan.IsNaN);
        var inf = matrix;
        inf.M00 = float.PositiveInfinity;
        Assert.True(inf.IsInf);
        Assert.NotEmpty(matrix.ToString());
        Assert.NotEqual(0u, matrix.ToHash());

        var source = Enumerable.Range(1, 32).Select(static value => (float)value).ToArray();
        var destination = new float[source.Length];
        Matrix4.BulkTranspose(destination, source, 2);
        Assert.Equal(source[0], destination[0]);
        Assert.Equal(source[4], destination[1]);
        Assert.Equal(source[16], destination[16]);
    }

    [Fact]
    public void Matrix3x4_remaining_operators_accessors_and_reflection_are_deterministic()
    {
        var matrix = new Matrix3x4(
            2, 3, 4, 5,
            6, 7, 8, 9,
            10, 11, 13, 14);
        Assert.Equal(matrix, new Matrix3x4(matrix));
        Assert.Equal(matrix, new Matrix3x4(matrix.Matrix4));
        Assert.Equal(new Matrix3x4(new Matrix3(matrix.Matrix3)),
            new Matrix3x4(new Matrix3(matrix.Matrix3)));
        Assert.True(matrix == new Matrix3x4(matrix));
        var different = new Matrix3x4(matrix);
        different.M00 += 1;
        Assert.True(matrix != different);

        Assert.Equal(new Vector3f(14, 30, 48), matrix * new Vector3f(1, 1, 1));
        Assert.Equal(new Vector3f(40, 80, 127), matrix * new Vector4f(1, 2, 3, 4));
        _ = matrix + Matrix3x4.Identity;
        _ = matrix - Matrix3x4.Identity;
        _ = matrix * 2.0f;
        _ = 2.0f * matrix;
        _ = matrix * Matrix3x4.Identity;
        _ = matrix * Matrix4.Identity;

        matrix.SetTranslation(new Vector3f(20, 21, 22));
        matrix.SetRotation(Matrix3.Identity);
        matrix.SetScale(new Vector3f(2, 3, 4));
        matrix.SetScale(2.0f);
        Assert.Equal(new Matrix3(2, 0, 0, 0, 2, 0, 0, 0, 2), matrix.Matrix3);
        Assert.Equal(new Vector3f(20, 21, 22), matrix.Translation);
        Assert.Equal(new Vector3f(2, 2, 2), matrix.Scale);
        Assert.Equal(new Vector3f(2, 2, 2), matrix.SignedScale(Matrix3.Identity));
        _ = matrix.RotationMatrix;
        _ = matrix.Rotation;
        _ = matrix.Matrix4;

        Assert.True(matrix.ApproximatelyEquivalent(new Matrix3x4(matrix)));
        var approximate = new Matrix3x4(matrix);
        approximate.M00 += MathDefs.Epsilon * 2.0f;
        Assert.False(matrix.ApproximatelyEquivalent(approximate));
        matrix.Decompose(out var translation, out _, out var scale);
        Assert.Equal(new Vector3f(20, 21, 22), translation);
        Assert.Equal(new Vector3f(2, 2, 2), scale);

        var reflected = Matrix3x4.FromScale(new Vector3f(-2, 3, 4));
        _ = reflected.RotationMatrix;
        Assert.True(reflected.Scale.X < 0.0f);
        _ = matrix.Determinant;
        _ = matrix.Inverse;
        Assert.Equal(0.0f, matrix.Element(2, 0));
        Assert.Equal(matrix.Row(1), new Vector4f(0, 2, 0, 21));
        Assert.Equal(matrix.Column(2), new Vector3f(0, 0, 2));
        Assert.False(matrix.IsNaN);
        Assert.False(matrix.IsInf);
        var nan = matrix;
        nan.M00 = float.NaN;
        Assert.True(nan.IsNaN);
        var inf = matrix;
        inf.M00 = float.PositiveInfinity;
        Assert.True(inf.IsInf);
        Assert.NotEmpty(matrix.ToString());
        Assert.NotEqual(0u, matrix.ToHash());
    }

    [Fact]
    public void Frustum_remaining_definers_transforms_classification_and_projection_are_deterministic()
    {
        var frustum = new Frustum();
        frustum.Define(60.0f, 1.5f, 1.0f, -1.0f, 4.0f);
        frustum.DefineOrtho(4.0f, 1.5f, 1.0f, -1.0f, 4.0f, Matrix3x4.Identity);
        frustum.Define(new Vector3f(2, 2, 1), new Vector3f(4, 4, 4));
        frustum.Define(new BoundingBox(-1.0f, 1.0f), Matrix3x4.Identity);
        frustum.Define(Matrix4.Identity);
        frustum.DefineSplit(Matrix4.Identity, 0.1f, 0.9f);

        var copy = new Frustum(frustum);
        var clone = (Frustum)frustum.Clone();
        var copied = new Frustum();
        copied.CopyFrom(frustum);
        Assert.Equal(frustum.Vertices, copy.Vertices);
        Assert.Equal(frustum.Vertices, clone.Vertices);
        Assert.Equal(frustum.Vertices, copied.Vertices);

        frustum.Transform(Matrix3.Identity);
        frustum.Transform(Matrix3x4.FromTranslation(new Vector3f(1, 2, 3)));
        _ = frustum.Transformed(Matrix3.Identity);
        _ = frustum.Transformed(Matrix3x4.Identity);

        frustum.Define(new Vector3f(2, 2, 1), new Vector3f(4, 4, 4));
        Assert.Equal(Intersection.Inside, frustum.IsInside(new Vector3f(0, 0, 2)));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new Vector3f(100, 100, 100)));
        Assert.Equal(Intersection.Inside, frustum.IsInsideFast(new Sphere(new Vector3f(0, 0, 2), 0.01f)));
        Assert.Equal(Intersection.Outside, frustum.IsInsideFast(new Sphere(new Vector3f(100, 0, 0), 0.1f)));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new Sphere(new Vector3f(100, 0, 0), 0.1f)));
        Assert.Equal(Intersection.Inside, frustum.IsInside(new BoundingBox(
            new Vector3f(-0.01f, -0.01f, 1.99f), new Vector3f(0.01f, 0.01f, 2.01f))));
        Assert.Equal(Intersection.Outside, frustum.IsInside(new BoundingBox(100, 101)));
        Assert.Equal(Intersection.Outside, frustum.IsInsideFast(new BoundingBox(100, 101)));
        _ = frustum.IsInside(new BoundingBox(-10, 10));
        _ = frustum.Distance(new Vector3f(100, 0, 0));
        _ = frustum.Projected(Matrix4.Identity);

        // Exercise both clipped-edge directions and the all-behind early return.
        frustum.Vertices[0] = new Vector3f(-1, -1, -1);
        frustum.Vertices[1] = new Vector3f(1, -1, -1);
        frustum.Vertices[2] = new Vector3f(1, 1, 0.5f);
        frustum.Vertices[3] = new Vector3f(-1, 1, 0.5f);
        frustum.Vertices[4] = new Vector3f(-1, -1, 0.5f);
        frustum.Vertices[5] = new Vector3f(1, -1, -1);
        frustum.Vertices[6] = new Vector3f(1, 1, -1);
        frustum.Vertices[7] = new Vector3f(-1, 1, -1);
        _ = frustum.Projected(Matrix4.Identity);
    }

    [Fact]
    public void BoundingBox_remaining_construction_merge_clip_distance_and_object_branches_are_deterministic()
    {
        var rect = new Rect(-2, -1, 3, 4);
        var box = new BoundingBox(rect);
        box.Define(new BoundingBox(-1, 1));
        box.Define(rect);
        box.Define(2.0f, 5.0f);
        box.Define(new Vector3f(1, 2, 3));
        box.Define(new[] { new Vector3f(-2, -3, -4), new Vector3f(3, 4, 5) });
        box.Define(Array.Empty<Vector3f>());

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1, 1, 1), new Vector3f(2, 2, 2));
        var polyhedron = new Polyhedron();
        polyhedron.Faces.Add([]);
        polyhedron.AddFace(Vector3f.Zero, Vector3f.Right, Vector3f.Up);
        box.Define(frustum);
        box.Define(polyhedron);
        box.Define(new Sphere(Vector3f.Zero, 2.0f));

        box.Merge(new Vector3f(-3, -4, -5));
        box.Merge(new BoundingBox(1, 2));
        box.Merge(new[] { new Vector3f(-6, 0, 0), new Vector3f(6, 0, 0) });
        box.Merge(frustum);
        box.Merge(polyhedron);
        box.Merge(new Sphere(Vector3f.Zero, 3.0f));
        Assert.True(box.Defined);

        var clipped = box.Clipped(new BoundingBox(-1, 1));
        Assert.True(clipped.Defined);
        var empty = box;
        empty.Clip(new BoundingBox(100, 101));
        Assert.False(empty.Defined);
        _ = box.Merged(Vector3f.Zero);
        _ = box.Merged(new BoundingBox(-1, 1));
        _ = box.Merged(new[] { Vector3f.Zero });
        _ = box.Merged(frustum);
        _ = box.Merged(polyhedron);
        _ = box.Merged(new Sphere(Vector3f.Zero, 1));
        _ = box.Padded(new Vector3f(1, 2, 3), new Vector3f(4, 5, 6));
        _ = box.Padded(Vector3f.One);
        _ = new BoundingBox().Padded(Vector3f.One);
        box.Transform(Matrix3.Identity);
        box.Transform(Matrix3x4.FromTranslation(Vector3f.One));
        _ = box.Transformed(Matrix3.Identity);
        _ = box.Transformed(Matrix3x4.Identity);
        _ = box.Projected(Matrix4.Identity);

        _ = box.Center;
        _ = box.Size;
        _ = box.HalfSize;
        _ = box.Volume;
        Assert.True(box.DistanceToPoint(new Vector3f(100, 0, 0)) > 0);
        Assert.True(box.SignedDistanceToPoint(Vector3f.Zero) < 0);
        _ = box.DistanceToBoundingBox(new BoundingBox(100, 101));
        _ = box.SignedDistanceToBoundingBox(new BoundingBox(100, 101));
        _ = box.IsInside(Vector3f.Zero);
        _ = box.IsInside(new Vector3f(100, 100, 100));
        _ = box.IsInside(new BoundingBox(-1, 1));
        _ = box.IsInside(new BoundingBox(100, 101));
        _ = box.IsInsideFast(new BoundingBox(100, 101));
        _ = box.IsInside(new Sphere(Vector3f.Zero, 0.1f));
        _ = box.IsInside(new Sphere(new Vector3f(100, 100, 100), 0.1f));
        _ = box.IsInsideFast(new Sphere(Vector3f.Zero, 0.1f));
        _ = box.IsInsideFast(new Sphere(new Vector3f(100, 100, 100), 0.1f));
        Assert.NotEmpty(box.ToString());
        Assert.True(box.Equals((object)box));
        Assert.NotEqual(0, box.GetHashCode());
    }

    private static void AssertSplinePointType(InterpolationMode mode, VariantType expectedType, params Variant[] values)
    {
        using var knots = new VariantVector();
        foreach (var value in values)
        {
            knots.Add(value);
            value.Dispose();
        }

        using var spline = new Spline(knots, mode);
        using var point = spline.GetPoint(0.5f);
        Assert.Equal(expectedType, point.Type);
    }

    private static void Write<T>(Span<byte> destination, int offset, T value) where T : struct =>
        MemoryMarshal.Write(destination[offset..], in value);
}
