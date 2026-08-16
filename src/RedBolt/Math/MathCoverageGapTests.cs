// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;

public sealed class MathCoverageGapTests
{
    [Fact]
    public void BoundingBox_boundaries_cover_definition_merging_clipping_and_distance()
    {
        var undefined = new BoundingBox();
        Assert.False(undefined.Defined);
        Assert.False(undefined.Padded(Vector3f.One).Defined);

        IReadOnlyList<Vector3f> empty = Array.Empty<Vector3f>();
        undefined.Define(empty);
        Assert.False(undefined.Defined);

        var points = new[]
        {
            new Vector3f(-2.0f, -1.0f, -3.0f),
            new Vector3f(4.0f, 5.0f, 6.0f),
            Vector3f.Zero
        };
        undefined.Define(points);
        Assert.Equal(new Vector3f(-2.0f, -1.0f, -3.0f), undefined.Min);
        Assert.Equal(new Vector3f(4.0f, 5.0f, 6.0f), undefined.Max);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 1.5f), undefined.Center);
        Assert.Equal(new Vector3f(6.0f, 6.0f, 9.0f), undefined.Size);
        Assert.Equal(new Vector3f(3.0f, 3.0f, 4.5f), undefined.HalfSize);
        Assert.Equal(324.0f, undefined.Volume);

        var fromRect = new BoundingBox(new Rect(-1.0f, -2.0f, 3.0f, 4.0f));
        Assert.Equal(new Vector3f(-1.0f, -2.0f, 0.0f), fromRect.Min);
        Assert.Equal(new Vector3f(3.0f, 4.0f, 0.0f), fromRect.Max);

        var fromSphere = new BoundingBox(new Sphere(new Vector3f(2.0f, 3.0f, 4.0f), 1.5f));
        Assert.Equal(new Vector3f(0.5f, 1.5f, 2.5f), fromSphere.Min);
        Assert.Equal(new Vector3f(3.5f, 4.5f, 5.5f), fromSphere.Max);

        var box = new BoundingBox(-1.0f, 1.0f);
        box.Define(new Vector3f(-2.0f, -3.0f, -4.0f), new Vector3f(2.0f, 3.0f, 4.0f));
        box.Merge(new Vector3f(-5.0f, 0.0f, 0.0f));
        box.Merge(new BoundingBox(new Vector3f(-1.0f, -6.0f, -1.0f), new Vector3f(1.0f, 1.0f, 7.0f)));
        box.Merge(new Sphere(new Vector3f(0.0f, 0.0f, 0.0f), 8.0f));
        Assert.Equal(new Vector3f(-8.0f, -8.0f, -8.0f), box.Min);
        Assert.Equal(new Vector3f(8.0f, 8.0f, 8.0f), box.Max);

        var mergedPoint = box.Merged(new Vector3f(10.0f, 0.0f, 0.0f));
        var mergedBox = box.Merged(new BoundingBox(-2.0f, 2.0f));
        var mergedPoints = box.Merged(new[] { new Vector3f(-9.0f, -9.0f, -9.0f), Vector3f.Zero });
        var mergedSphere = box.Merged(new Sphere(Vector3f.Zero, 20.0f));
        Assert.Equal(8.0f, box.Max.X);
        Assert.Equal(10.0f, mergedPoint.Max.X);
        Assert.Equal(8.0f, mergedBox.Max.X);
        Assert.Equal(-9.0f, mergedPoints.Min.X);
        Assert.Equal(-20.0f, mergedSphere.Min.X);

        var clipped = box.Clipped(new BoundingBox(-4.0f, 4.0f));
        Assert.Equal(new Vector3f(-4.0f, -4.0f, -4.0f), clipped.Min);
        Assert.Equal(new Vector3f(4.0f, 4.0f, 4.0f), clipped.Max);

        var disjoint = box.Clipped(new BoundingBox(20.0f, 21.0f));
        Assert.False(disjoint.Defined);

        var padded = clipped.Padded(new Vector3f(1.0f, 2.0f, 3.0f), new Vector3f(2.0f, 1.0f, 0.5f));
        Assert.Equal(new Vector3f(-5.0f, -6.0f, -7.0f), padded.Min);
        Assert.Equal(new Vector3f(6.0f, 5.0f, 4.5f), padded.Max);

        var translated = clipped.Transformed(Matrix3x4.FromTranslation(new Vector3f(5.0f, 6.0f, 7.0f)));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), translated.Min);
        Assert.Equal(new Vector3f(9.0f, 10.0f, 11.0f), translated.Max);
        Assert.Equal(clipped, clipped.Transformed(Matrix3.Identity));

        var mutable = clipped;
        mutable.Transform(Matrix3.Identity);
        mutable.Transform(Matrix3x4.Identity);
        Assert.Equal(clipped, mutable);
        Assert.True(clipped.Projected(Matrix4.Identity).Defined);

        Assert.Equal(0.0f, clipped.DistanceToPoint(Vector3f.Zero));
        Assert.Equal(1.0f, clipped.DistanceToPoint(new Vector3f(5.0f, 0.0f, 0.0f)));
        Assert.Equal(-4.0f, clipped.SignedDistanceToPoint(Vector3f.Zero));
        Assert.Equal(1.0f, clipped.SignedDistanceToPoint(new Vector3f(5.0f, 0.0f, 0.0f)));

        var separated = new BoundingBox(new Vector3f(6.0f, -1.0f, -1.0f), new Vector3f(7.0f, 1.0f, 1.0f));
        Assert.Equal(2.0f, clipped.DistanceToBoundingBox(separated));
        Assert.Equal(2.0f, clipped.SignedDistanceToBoundingBox(separated));
        Assert.True(clipped.SignedDistanceToBoundingBox(new BoundingBox(-1.0f, 1.0f)) < 0.0f);

        Assert.Equal(Intersection.Inside, clipped.IsInside(Vector3f.Zero));
        Assert.Equal(Intersection.Outside, clipped.IsInside(new Vector3f(5.0f, 0.0f, 0.0f)));
        Assert.Equal(Intersection.Inside, clipped.IsInside(new BoundingBox(-2.0f, 2.0f)));
        Assert.Equal(Intersection.Intersects, clipped.IsInside(new BoundingBox(3.0f, 6.0f)));
        Assert.Equal(Intersection.Outside, clipped.IsInside(new BoundingBox(6.0f, 7.0f)));
        Assert.Equal(Intersection.Inside, clipped.IsInsideFast(new BoundingBox(3.0f, 6.0f)));
        Assert.Equal(Intersection.Outside, clipped.IsInsideFast(new BoundingBox(6.0f, 7.0f)));

        Assert.Equal(Intersection.Inside, clipped.IsInside(new Sphere(Vector3f.Zero, 1.0f)));
        Assert.Equal(Intersection.Intersects, clipped.IsInside(new Sphere(Vector3f.Zero, 5.0f)));
        Assert.Equal(Intersection.Outside, clipped.IsInside(new Sphere(new Vector3f(8.0f, 8.0f, 8.0f), 0.5f)));
        Assert.Equal(Intersection.Inside, clipped.IsInsideFast(new Sphere(Vector3f.Zero, 1.0f)));
        Assert.Equal(Intersection.Outside, clipped.IsInsideFast(new Sphere(new Vector3f(8.0f, 8.0f, 8.0f), 0.5f)));
    }

    [Fact]
    public void Sphere_boundaries_cover_merge_classification_and_intersection()
    {
        var undefined = new Sphere();
        undefined.Define(new Vector3f(1.0f, 2.0f, 3.0f), 2.0f);
        Assert.True(undefined.Defined);
        undefined.Define(new Sphere(Vector3f.Zero, 1.0f));
        undefined.Define(ReadOnlySpan<Vector3f>.Empty);
        Assert.Equal(new Sphere(Vector3f.Zero, 1.0f), undefined);

        var fromVertices = new Sphere(new[] { new Vector3f(-2.0f, 0.0f, 0.0f), new Vector3f(2.0f, 0.0f, 0.0f) });
        Assert.Equal(Vector3f.Zero, fromVertices.Center);
        Assert.Equal(2.0f, fromVertices.Radius);

        var fromBox = new Sphere(new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.True(fromBox.Defined);
        Assert.True(fromBox.Radius > 3.0f);

        var merged = new Sphere();
        merged.Merge(new Vector3f(1.0f, 0.0f, 0.0f));
        Assert.Equal(Vector3f.Right, merged.Center);
        merged.Merge(new Vector3f(1.0f, 0.0f, 0.0f));
        merged.Merge(new Vector3f(4.0f, 0.0f, 0.0f));
        Assert.True(merged.Radius > 1.0f);

        var fits = new Sphere(Vector3f.Zero, 5.0f);
        fits.Merge(new Sphere(Vector3f.One, 1.0f));
        Assert.Equal(new Sphere(Vector3f.Zero, 5.0f), fits);

        var contains = new Sphere(Vector3f.Zero, 1.0f);
        contains.Merge(new Sphere(Vector3f.Zero, 4.0f));
        Assert.Equal(new Sphere(Vector3f.Zero, 4.0f), contains);

        var overlap = new Sphere(Vector3f.Zero, 2.0f);
        overlap.Merge(new Sphere(new Vector3f(3.0f, 0.0f, 0.0f), 2.0f));
        Assert.True(overlap.Radius > 2.0f && overlap.Radius < 4.0f);

        var boxMerge = new Sphere(Vector3f.Zero, 0.5f);
        boxMerge.Merge(new BoundingBox(-2.0f, 2.0f));
        boxMerge.Merge(new[] { new Vector3f(-4.0f, 0.0f, 0.0f), new Vector3f(4.0f, 0.0f, 0.0f) });
        Assert.True(boxMerge.Radius >= 4.0f);

        var sphere = new Sphere(Vector3f.Zero, 2.0f);
        Assert.Equal(Intersection.Inside, sphere.IsInside(Vector3f.One));
        Assert.Equal(Intersection.Outside, sphere.IsInside(new Vector3f(2.0f, 0.0f, 0.0f)));
        Assert.Equal(Intersection.Inside, sphere.IsInside(new Sphere(Vector3f.Zero, 0.5f)));
        Assert.Equal(Intersection.Intersects, sphere.IsInside(new Sphere(new Vector3f(3.0f, 0.0f, 0.0f), 2.0f)));
        Assert.Equal(Intersection.Outside, sphere.IsInside(new Sphere(new Vector3f(5.0f, 0.0f, 0.0f), 1.0f)));
        Assert.Equal(Intersection.Inside, sphere.IsInsideFast(new Sphere(Vector3f.One, 0.5f)));
        Assert.Equal(Intersection.Outside, sphere.IsInsideFast(new Sphere(new Vector3f(5.0f, 0.0f, 0.0f), 1.0f)));

        var unitBox = new BoundingBox(-1.0f, 1.0f);
        Assert.Equal(Intersection.Inside, new Sphere(Vector3f.Zero, 3.0f).IsInside(new BoundingBox(-0.5f, 0.5f)));
        Assert.Equal(Intersection.Intersects, new Sphere(Vector3f.Zero, 1.0f).IsInside(unitBox));
        Assert.Equal(Intersection.Outside, new Sphere(new Vector3f(3.0f, 3.0f, 3.0f), 1.0f).IsInside(unitBox));
        Assert.Equal(Intersection.Inside, new Sphere(Vector3f.Zero, 3.0f).IsInsideFast(new BoundingBox(-0.5f, 0.5f)));
        Assert.Equal(Intersection.Outside, new Sphere(new Vector3f(3.0f, 3.0f, 3.0f), 1.0f).IsInsideFast(unitBox));

        var valid = sphere.Intersect(new Sphere(new Vector3f(3.0f, 0.0f, 0.0f), 2.0f), out var distance);
        Assert.True(valid.IsValid);
        Assert.True(distance > 0.0f);
        Assert.True(valid.GetPoint(Vector3f.Up).Length > 0.0f);

        var disjoint = sphere.Intersect(new Sphere(new Vector3f(10.0f, 0.0f, 0.0f), 1.0f));
        Assert.False(disjoint.IsValid);
        var contained = new Sphere(Vector3f.Zero, 4.0f).Intersect(new Sphere(new Vector3f(0.5f, 0.0f, 0.0f), 1.0f));
        Assert.False(contained.IsValid);

        Assert.Equal(0.0f, sphere.Distance(Vector3f.One));
        Assert.Equal(1.0f, sphere.Distance(new Vector3f(3.0f, 0.0f, 0.0f)));
        Assert.True(sphere.GetLocalPoint(0.0f, 90.0f).ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 2.0f)));
        Assert.True(sphere.GetPoint(0.0f, 90.0f).ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 2.0f)));
    }

    [Fact]
    public void Ray_boundaries_cover_parallel_miss_orientation_and_geometry_paths()
    {
        var ray = new Ray(Vector3f.Zero, Vector3f.Right);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), ray.Project(new Vector3f(2.0f, 3.0f, 4.0f)));
        Assert.Equal(5.0f, ray.Distance(new Vector3f(2.0f, 3.0f, 4.0f)));
        Assert.Equal(Vector3f.Zero, ray.ClosestPoint(new Ray(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Right)));
        Assert.True(ray.ClosestPoint(new Ray(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Up)).X >= 0.0f);

        var plane = new Plane(Vector3f.Up, Vector3f.Zero);
        Assert.Equal(float.PositiveInfinity, ray.HitDistance(plane));
        Assert.Equal(float.PositiveInfinity, new Ray(Vector3f.Zero, Vector3f.Down).HitDistance(new Plane(Vector3f.Up, new Vector3f(0.0f, 1.0f, 0.0f))));
        Assert.Equal(1.0f, new Ray(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Down).HitDistance(plane));

        var box = new BoundingBox(-1.0f, 1.0f);
        var undefinedBox = new BoundingBox();
        Assert.True(float.IsPositiveInfinity(new Ray(Vector3f.Zero, Vector3f.Right).HitDistanceAndNormal(undefinedBox).Distance));

        var directions = new[]
        {
            (new Vector3f(-2.0f, 0.0f, 0.0f), Vector3f.Right, new Vector3f(-1.0f, 0.0f, 0.0f)),
            (new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Left, new Vector3f(1.0f, 0.0f, 0.0f)),
            (new Vector3f(0.0f, -2.0f, 0.0f), Vector3f.Up, new Vector3f(0.0f, -1.0f, 0.0f)),
            (new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down, new Vector3f(0.0f, 1.0f, 0.0f)),
            (new Vector3f(0.0f, 0.0f, -2.0f), Vector3f.Forward, new Vector3f(0.0f, 0.0f, -1.0f)),
            (new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Back, new Vector3f(0.0f, 0.0f, 1.0f))
        };
        foreach (var (origin, direction, normal) in directions)
        {
            var hit = new Ray(origin, direction).HitDistanceAndNormal(box);
            Assert.Equal(1.0f, hit.Distance);
            Assert.Equal(normal, hit.Normal);
        }

        var diagonalMiss = new Ray(new Vector3f(-2.0f, 2.0f, 0.0f), Vector3f.Right).HitDistanceAndNormal(box);
        Assert.True(float.IsPositiveInfinity(diagonalMiss.Distance));
        var insideHit = ray.HitDistanceAndNormal(box);
        Assert.Equal(0.0f, insideHit.Distance);
        Assert.Equal(-Vector3f.Right, insideHit.Normal);

        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        Assert.Equal(0.0f, ray.HitDistance(sphere));
        Assert.True(float.IsPositiveInfinity(new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Right).HitDistance(sphere)));
        Assert.Equal(1.0f, new Ray(new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Left).HitDistance(sphere));

        var triangleA = new Vector3f(0.0f, 0.0f, 0.0f);
        var triangleB = new Vector3f(0.0f, 1.0f, 0.0f);
        var triangleC = new Vector3f(1.0f, 0.0f, 0.0f);
        var triangleRay = new Ray(new Vector3f(0.25f, 0.25f, -1.0f), Vector3f.Forward);
        Assert.Equal(1.0f, triangleRay.HitDistance(triangleA, triangleB, triangleC, out _, out var barycentric));
        Assert.True(barycentric.X > 0.0f && barycentric.Y > 0.0f && barycentric.Z > 0.0f);
        Assert.True(float.IsPositiveInfinity(triangleRay.HitDistance(triangleA, triangleC, triangleB, out _, out _)));
        Assert.True(float.IsPositiveInfinity(new Ray(new Vector3f(2.0f, 2.0f, -1.0f), Vector3f.Forward)
            .HitDistance(triangleA, triangleB, triangleC, out _, out _)));
        Assert.True(float.IsPositiveInfinity(new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Forward)
            .HitDistance(triangleA, triangleB, triangleC, out _, out _)));

        const int stride = 20;
        Span<byte> vertices = stackalloc byte[stride * 3];
        Write(vertices, 0, triangleA);
        Write(vertices, 12, Vector2f.Zero);
        Write(vertices, stride, triangleB);
        Write(vertices, stride + 12, Vector2f.Up);
        Write(vertices, stride * 2, triangleC);
        Write(vertices, stride * 2 + 12, Vector2f.Right);

        var noGeometry = triangleRay.HitDistance(vertices, stride, 0u, 0u, out _, out var emptyUv, 12u);
        Assert.True(float.IsPositiveInfinity(noGeometry));
        Assert.Equal(Vector2f.Zero, emptyUv);
        Assert.False(triangleRay.InsideGeometry(vertices, stride, 0u, 0u));

        Span<byte> indices16 = stackalloc byte[sizeof(ushort) * 3];
        Write(indices16, 0, (ushort)0);
        Write(indices16, sizeof(ushort), (ushort)1);
        Write(indices16, sizeof(ushort) * 2, (ushort)2);
        Assert.False(triangleRay.InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 3u));

        Span<byte> indices32 = stackalloc byte[sizeof(uint) * 3];
        Write(indices32, 0, 0u);
        Write(indices32, sizeof(uint), 1u);
        Write(indices32, sizeof(uint) * 2, 2u);
        Assert.True(new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Back)
            .InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 3u));

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 4.0f));
        var insideFrustum = new Ray(new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Forward);
        Assert.Equal(0.0f, insideFrustum.HitDistance(frustum));
        Assert.True(insideFrustum.HitDistance(frustum, solidInside: false) > 0.0f);
        Assert.True(new Ray(new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward).HitDistance(frustum) > 0.0f);
    }

    [Fact]
    public void Color_boundaries_cover_packing_color_spaces_and_channel_branches()
    {
        var lhs = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        var rhs = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        AssertColor(new Color(0.02f, 0.08f, 0.18f, 0.32f), lhs * rhs);
        AssertColor(new Color(0.4f, 0.8f, 1.2f, 1.6f), lhs * 2.0f);
        AssertColor(new Color(0.3f, 0.6f, 0.9f, 1.2f), lhs + rhs);
        AssertColor(new Color(-0.2f, -0.4f, -0.6f, -0.8f), -lhs);
        AssertColor(new Color(0.1f, 0.2f, 0.3f, 0.4f), lhs - rhs);
        AssertColor(new Color(0.2f, 0.4f, 0.6f, 0.5f), new Color(lhs, 0.5f));
        Assert.Equal(new Vector3f(0.2f, 0.4f, 0.6f), lhs.ToVector3());
        Assert.Equal(new Vector4f(0.2f, 0.4f, 0.6f, 0.8f), lhs.ToVector4());

        var clippedPacking = new Color(-0.1f, 1.2f, 0.5f, 2.0f);
        Assert.Equal(0xFF7FFF00u, clippedPacking.ToUInt());
        var unpacked = new Color(0x80402010u);
        Assert.Equal(16.0f / 255.0f, unpacked.R);
        Assert.Equal(32.0f / 255.0f, unpacked.G);
        Assert.Equal(64.0f / 255.0f, unpacked.B);
        Assert.Equal(128.0f / 255.0f, unpacked.A);

        var partialMask = new Color.ChannelMask(0, 0x000000ff, 0, 0);
        var fromMask = new Color(0xffffffffu, partialMask);
        Assert.Equal(0.0f, fromMask.R);
        Assert.Equal(1.0f, fromMask.G);
        Assert.Equal(0.0f, fromMask.B);
        Assert.Equal(1.0f, fromMask.A);
        Assert.Equal(0x00000000u, new Color(1.0f, 1.0f, 1.0f, 1.0f).ToUIntMask(new Color.ChannelMask(0, 0, 0, 0)));
        Assert.NotEqual(0u, lhs.ToUIntArgb());

        var hslLow = new Color();
        hslLow.FromHSL(0.1f, 1.0f, 0.25f, 0.75f);
        Assert.Equal(0.75f, hslLow.A);
        var hslHigh = new Color();
        hslHigh.FromHSL(0.9f, 1.0f, 0.75f);
        Assert.Equal(1.0f, hslHigh.A);

        foreach (var hue in new[] { 0.1f, 0.25f, 0.4f, 0.55f, 0.75f, 0.9f, 1.2f, -0.2f })
        {
            var hsv = new Color();
            hsv.FromHSV(hue, 1.0f, 1.0f, 0.6f);
            Assert.Equal(0.6f, hsv.A);
            Assert.InRange(hsv.R, 0.0f, 1.0f);
            Assert.InRange(hsv.G, 0.0f, 1.0f);
            Assert.InRange(hsv.B, 0.0f, 1.0f);
        }

        var black = Color.Black;
        Assert.Equal(0.0f, black.Hue());
        Assert.Equal(0.0f, black.SaturationHSV());
        Assert.Equal(0.0f, black.SaturationHSL());
        Assert.Equal(0.0f, black.Lightness());

        var white = Color.White;
        Assert.Equal(0.0f, white.SaturationHSV());
        Assert.Equal(0.0f, white.SaturationHSL());
        Assert.Equal(1.0f, white.Value());
        Assert.Equal(1.0f, white.Lightness());

        foreach (var color in new[]
        {
            new Color(0.8f, 0.2f, 0.1f, 1.0f),
            new Color(0.8f, 0.2f, 0.5f, 1.0f),
            new Color(0.1f, 0.2f, 0.8f, 1.0f),
            new Color(0.4f, 0.8f, 0.2f, 1.0f),
            new Color(0.6f, 0.8f, 0.9f, 1.0f),
            new Color(1.2f, -0.2f, 0.5f, 1.0f)
        })
        {
            color.Bounds(out var min, out var max);
            color.Bounds(out var clippedMin, out var clippedMax, clipped: true);
            Assert.True(min <= max);
            Assert.InRange(clippedMin, 0.0f, 1.0f);
            Assert.InRange(clippedMax, 0.0f, 1.0f);
            _ = color.ToHSL();
            _ = color.ToHSV();
            _ = color.Chroma();
            _ = color.Hue();
            _ = color.SaturationHSL();
            _ = color.SaturationHSV();
            _ = color.Range();
            _ = color.MaxRGB();
            _ = color.MinRGB();
        }

        Assert.Equal(0.01f / 12.92f, Color.ConvertGammaToLinear(0.01f));
        Assert.InRange(Color.ConvertGammaToLinear(0.5f), 0.0f, 1.0f);
        Assert.Equal(1.0f, Color.ConvertGammaToLinear(1.0f));
        Assert.Equal(0.0f, Color.ConvertLinearToGamma(-1.0f));
        Assert.Equal(12.92f * 0.001f, Color.ConvertLinearToGamma(0.001f));
        Assert.InRange(Color.ConvertLinearToGamma(0.5f), 0.0f, 1.0f);
        Assert.Equal(1.0f, Color.ConvertLinearToGamma(1.0f));
        AssertColor(new Color(Color.ConvertGammaToLinear(lhs.R), Color.ConvertGammaToLinear(lhs.G), Color.ConvertGammaToLinear(lhs.B), lhs.A), lhs.GammaToLinear());
        Assert.True(lhs.GammaToLinear().LinearToGamma().Equals(lhs, 0.0001f));

        var mutable = new Color(1.5f, -0.5f, 0.5f, -1.0f);
        mutable.Clip();
        AssertColor(new Color(1.0f, 0.0f, 0.5f, -1.0f), mutable);
        mutable.Clip(clipAlpha: true);
        Assert.Equal(0.0f, mutable.A);
        mutable.Invert();
        mutable.Invert(invertAlpha: true);
        AssertColor(new Color(1.0f, 0.0f, 0.5f, 1.0f), mutable);
        AssertColor(new Color(0.15f, 0.3f, 0.45f, 0.6f), lhs.Lerp(rhs, 0.5f));
        AssertColor(new Color(0.2f, 0.4f, 0.6f, 0.8f), new Color(-lhs.R, -lhs.G, -lhs.B, -lhs.A).Abs());
        Assert.True(lhs.Equals(new Color(lhs.R + MathDefs.Epsilon * 0.5f, lhs.G, lhs.B, lhs.A), MathDefs.Epsilon));
        Assert.False(lhs.Equals(new Color(lhs.R + 0.1f, lhs.G, lhs.B, lhs.A), MathDefs.Epsilon));
        Assert.Contains("0.2", lhs.ToString());
        Assert.NotEqual(0u, lhs.ToHash());
    }

    [Fact]
    public void Spline_boundaries_cover_modes_types_clamping_and_mutations()
    {
        using var empty = new Spline(InterpolationMode.LinearCurve);
        using var noPoint = empty.GetPoint(0.5f);
        Assert.Equal(VariantType.None, noPoint.Type);

        using var firstKnot = new Variant(3.0f);
        empty.AddKnot(firstKnot);
        using var onePoint = empty.GetPoint(-1.0f);
        Assert.Equal(3.0f, onePoint.GetFloat());
        using var secondKnot = new Variant(9.0f);
        empty.AddKnot(secondKnot, 99);
        Assert.Equal(2, empty.GetKnots().Count);
        using var knotCopy = empty.GetKnot(0);
        Assert.Equal(3.0f, knotCopy.GetFloat());

        using var changed = new Variant(4.0f);
        empty.SetKnot(changed, 0);
        using var mismatched = new Variant(new Vector3f(1.0f, 1.0f, 1.0f));
        empty.SetKnot(mismatched, 1);
        empty.AddKnot(mismatched);
        empty.AddKnot(mismatched, 1);
        Assert.Equal(2, empty.GetKnots().Count);
        empty.RemoveKnot(1);
        empty.RemoveKnot();
        Assert.Equal(0, empty.GetKnots().Count);
        empty.AddKnot(changed);
        Assert.Equal(1, empty.GetKnots().Count);
        empty.Clear();

        foreach (var (lhs, rhs) in new[]
        {
            (new Variant(0.0f), new Variant(10.0f)),
            (new Variant(Vector2f.Zero), new Variant(new Vector2f(10.0f, 10.0f))),
            (new Variant(Vector3f.Zero), new Variant(new Vector3f(10.0f, 10.0f, 10.0f))),
            (new Variant(Vector4f.Zero), new Variant(new Vector4f(10.0f, 10.0f, 10.0f, 10.0f))),
            (new Variant(Color.Black), new Variant(Color.White)),
            (new Variant(0.0), new Variant(10.0))
        })
        {
            using (lhs)
            using (rhs)
            using (var knots = new VariantVector { lhs, rhs })
            using (var spline = new Spline(knots, InterpolationMode.BezierCurve))
            using (var point = spline.GetPoint(0.5f))
            {
                Assert.NotEqual(VariantType.None, point.Type);
            }
        }

        using var unsupportedBezierKnots = new VariantVector { new(1), new(2) };
        using var unsupportedBezier = new Spline(unsupportedBezierKnots, InterpolationMode.BezierCurve);
        using var unsupportedBezierPoint = unsupportedBezier.GetPoint(0.5f);
        Assert.Equal(VariantType.None, unsupportedBezierPoint.Type);

        using var bezierKnots = new VariantVector { new(0.0f), new(10.0f), new(20.0f) };
        using var bezier = new Spline(bezierKnots, InterpolationMode.BezierCurve);
        using var bezierPoint = bezier.GetPoint(0.5f);
        Assert.Equal(10.0f, bezierPoint.GetFloat());

        foreach (var (mode, knots) in new[]
        {
            (InterpolationMode.LinearCurve, new VariantVector { new(0.0f), new(10.0f), new(20.0f) }),
            (InterpolationMode.CatmullRomCurve, new VariantVector { new(0.0f), new(10.0f), new(20.0f), new(30.0f) }),
            (InterpolationMode.CatmullRomFullCurve, new VariantVector { new(0.0f), new(10.0f), new(20.0f) })
        })
        {
            using (knots)
            using (var spline = new Spline(knots, mode))
            using (var below = spline.GetPoint(-1.0f))
            using (var middle = spline.GetPoint(0.5f))
            using (var above = spline.GetPoint(2.0f))
            using (var end = spline.GetPoint(1.0f))
            {
                Assert.NotEqual(VariantType.None, below.Type);
                Assert.NotEqual(VariantType.None, middle.Type);
                Assert.NotEqual(VariantType.None, above.Type);
                Assert.NotEqual(VariantType.None, end.Type);
            }
        }

        using var cyclicKnots = new VariantVector { new(0.0f), new(10.0f), new(20.0f), new(0.0f) };
        using var cyclic = new Spline(cyclicKnots, InterpolationMode.CatmullRomFullCurve);
        using var cyclicPoint = cyclic.GetPoint(0.25f);
        Assert.NotEqual(VariantType.None, cyclicPoint.Type);

        foreach (var values in new[]
        {
            new[] { 0.0f, 10.0f, 20.0f, 30.0f },
            new[] { 0.0f, 10.0f, 20.0f, 30.0f }
        })
        {
            using var knots = new VariantVector();
            foreach (var value in values)
                knots.Add(new Variant(value));
            using var spline = new Spline(knots, InterpolationMode.CatmullRomCurve);
            using var point = spline.GetPoint(0.5f);
            Assert.Equal(VariantType.Float, point.Type);
        }

        using var cloneSourceKnots = new VariantVector { new(0.0f), new(10.0f) };
        using var cloneSource = new Spline(cloneSourceKnots, InterpolationMode.LinearCurve);
        using var clone = (Spline)cloneSource.Clone();
        Assert.True(clone == cloneSource);
        Assert.False(clone != cloneSource);
        Assert.Equal(cloneSource.GetHashCode(), clone.GetHashCode());
        Assert.Equal(InterpolationMode.LinearCurve, clone.GetInterpolationMode());
        clone.SetInterpolationMode(InterpolationMode.BezierCurve);
        Assert.NotEqual(cloneSource, clone);

        using var replacement = new VariantVector { new(2.0f), new(4.0f) };
        clone.SetKnots(replacement);
        using var replacementPoint = clone.GetPoint(0.5f);
        Assert.Equal(3.0f, replacementPoint.GetFloat());
        Assert.True(clone.Equals((object)clone));
        Assert.False(clone.Equals(null));
    }

    [Fact]
    public void Numeric_and_wrapped_ranges_cover_empty_exclusive_and_multiwrap_boundaries()
    {
        var invalid = new NumericRange<int>();
        Assert.False(invalid.Equals((object)new NumericRange<int>(1, 2)));
        _ = invalid.GetHashCode();
        Assert.True(new NumericRange<int>(2, 5) != new NumericRange<int>(2, 6));
        Assert.Equal(new NumericRange<int>(7, 2), new NumericRange<int>(5, 2) & new NumericRange<int>(7, 9));

        var noWrap = new WrappedScalarRange<float>(2.0f, 8.0f, 0.0f, 10.0f, 0);
        Assert.False(noWrap.ContainsExclusive(2.0f));
        Assert.False(noWrap.ContainsExclusive(8.0f));
        Assert.True(noWrap.ContainsExclusive(5.0f));
        Assert.False(noWrap.ContainsInclusive(-1.0f));
        Assert.NotEqual(noWrap, new WrappedScalarRange<float>(2.0f, 8.0f, 0.0f, 10.0f, 1));
        _ = noWrap.GetHashCode();

        var wrappedForward = new WrappedScalarRange<float>(8.0f, 2.0f, 0.0f, 10.0f, 1);
        Assert.True(wrappedForward.ContainsInclusive(0.0f));
        Assert.True(wrappedForward.ContainsInclusive(10.0f));
        Assert.False(wrappedForward.ContainsExcludingBegin(8.0f));
        Assert.False(wrappedForward.ContainsExcludingEnd(2.0f));

        var always = new WrappedScalarRange<float>(8.0f, 2.0f, 0.0f, 10.0f, 2);
        Assert.True(always.ContainsExclusive(5.0f));

        var scalar = new WrappedScalar<float>(5.0f, 0.0f, 10.0f);
        Assert.Equal(scalar, new WrappedScalar<float>(5.0f, 0.0f, 10.0f));
        Assert.NotEqual(scalar, new WrappedScalar<float>(6.0f, 0.0f, 10.0f));
        _ = scalar.GetHashCode();
        Assert.Equal(5.0f, scalar.UpdateWrapped(0.0f).Begin());
        scalar = new WrappedScalar<float>(5.0f, 0.0f, 10.0f);
        var backward = scalar.UpdateWrapped(-25.0f);
        Assert.Equal(10.0f, scalar.Value());
        Assert.True(backward.ContainsInclusive(0.0f));
        Assert.True(backward.ContainsInclusive(10.0f));
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.True(actual.Equals(expected, 0.000001f), $"Expected {expected}, got {actual}");
    }

    private static void Write<T>(Span<byte> destination, int offset, T value) where T : struct
    {
        MemoryMarshal.Write(destination[offset..], in value);
    }

}
