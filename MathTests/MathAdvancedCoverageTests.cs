namespace RedBolt.Tests.MathTests;

using System.Reflection;
using System.Runtime.InteropServices;

using RedBolt.Math;

public sealed class MathAdvancedCoverageTests
{
    [Fact]
    public void Matrix2_and_matrix3_cover_arithmetic_scaling_and_predicates()
    {
        var matrix2 = new Matrix2(2.0f, 3.0f, 5.0f, 7.0f);
        var other2 = new Matrix2(-1.0f, 4.0f, 6.0f, -2.0f);

        Assert.Equal(new Matrix2(1.0f, 7.0f, 11.0f, 5.0f), matrix2 + other2);
        Assert.Equal(new Matrix2(3.0f, -1.0f, -1.0f, 9.0f), matrix2 - other2);
        Assert.Equal(new Matrix2(4.0f, 6.0f, 10.0f, 14.0f), matrix2 * 2.0f);
        Assert.Equal(matrix2 * 2.0f, 2.0f * matrix2);
        Assert.Equal(new Vector2f(8.0f, 19.0f), matrix2 * new Vector2f(1.0f, 2.0f));
        Assert.Equal(new Matrix2(16.0f, 2.0f, 37.0f, 6.0f), matrix2 * other2);

        var scaled2 = matrix2;
        scaled2.SetScale(new Vector2f(11.0f, 13.0f));
        Assert.Equal(new Matrix2(11.0f, 3.0f, 5.0f, 13.0f), scaled2);
        scaled2.SetScale(17.0f);
        Assert.Equal(new Matrix2(17.0f, 3.0f, 5.0f, 17.0f), scaled2);
        Assert.Equal(new Vector2f(MathF.Sqrt(29.0f), MathF.Sqrt(58.0f)), matrix2.Scale);
        Assert.Equal(new Matrix2(4.0f, 9.0f, 10.0f, 21.0f), matrix2.Scaled(new Vector2f(2.0f, 3.0f)));
        Assert.Equal(new Matrix2(2.0f, 5.0f, 3.0f, 7.0f), matrix2.Transpose);
        Assert.Equal(new[] { 2.0f, 3.0f, 5.0f, 7.0f }, matrix2.Data.ToArray());
        Assert.True(matrix2.ApproximatelyEquivalent(new Matrix2(2.0005f, 3.0f, 5.0f, 7.0f), 0.001f));
        Assert.False(matrix2.ApproximatelyEquivalent(new Matrix2(9.0f, 3.0f, 5.0f, 7.0f)));
        Assert.False(matrix2.ApproximatelyEquivalent(new Matrix2(2.0f, 9.0f, 5.0f, 7.0f)));
        Assert.False(matrix2.ApproximatelyEquivalent(new Matrix2(2.0f, 3.0f, 9.0f, 7.0f)));
        Assert.False(matrix2.ApproximatelyEquivalent(new Matrix2(2.0f, 3.0f, 5.0f, 9.0f)));
        Assert.Equal("2 3 5 7", matrix2.ToString());

        Assert.True(matrix2 == new Matrix2(matrix2.M00, matrix2.M01, matrix2.M10, matrix2.M11));
        Assert.False(matrix2 == new Matrix2(9.0f, 3.0f, 5.0f, 7.0f));
        Assert.False(matrix2 == new Matrix2(2.0f, 9.0f, 5.0f, 7.0f));
        Assert.False(matrix2 == new Matrix2(2.0f, 3.0f, 9.0f, 7.0f));
        Assert.False(matrix2 == new Matrix2(2.0f, 3.0f, 5.0f, 9.0f));
        Assert.True(matrix2 != other2);
        Assert.False(matrix2 != new Matrix2(matrix2.M00, matrix2.M01, matrix2.M10, matrix2.M11));
        Assert.True(matrix2.Equals((object)matrix2));
        Assert.False(matrix2.Equals((object?)null));
        _ = matrix2.GetHashCode();

        foreach (var value in new[]
        {
            new Matrix2(float.NaN, 0.0f, 0.0f, 0.0f),
            new Matrix2(0.0f, float.NaN, 0.0f, 0.0f),
            new Matrix2(0.0f, 0.0f, float.NaN, 0.0f),
            new Matrix2(0.0f, 0.0f, 0.0f, float.NaN)
        })
            Assert.True(value.IsNaN);
        Assert.False(Matrix2.Zero.IsNaN);

        foreach (var value in new[]
        {
            new Matrix2(float.PositiveInfinity, 0.0f, 0.0f, 0.0f),
            new Matrix2(0.0f, float.PositiveInfinity, 0.0f, 0.0f),
            new Matrix2(0.0f, 0.0f, float.PositiveInfinity, 0.0f),
            new Matrix2(0.0f, 0.0f, 0.0f, float.PositiveInfinity)
        })
            Assert.True(value.IsInf);
        Assert.False(Matrix2.Zero.IsInf);

        var matrix3 = new Matrix3(
            2.0f, 3.0f, 4.0f,
            5.0f, 6.0f, 7.0f,
            8.0f, 9.0f, 10.0f);
        var other3 = new Matrix3(
            -1.0f, 4.0f, 6.0f,
            -2.0f, 3.0f, 5.0f,
            7.0f, -8.0f, 9.0f);

        Assert.Equal(
            new Matrix3(1.0f, 7.0f, 10.0f, 3.0f, 9.0f, 12.0f, 15.0f, 1.0f, 19.0f),
            matrix3 + other3);
        Assert.Equal(
            new Matrix3(3.0f, -1.0f, -2.0f, 7.0f, 3.0f, 2.0f, 1.0f, 17.0f, 1.0f),
            matrix3 - other3);
        Assert.Equal(
            new Matrix3(4.0f, 6.0f, 8.0f, 10.0f, 12.0f, 14.0f, 16.0f, 18.0f, 20.0f),
            matrix3 * 2.0f);
        Assert.Equal(matrix3 * 2.0f, 2.0f * matrix3);
        Assert.Equal(new Vector3f(20.0f, 38.0f, 56.0f), matrix3 * new Vector3f(1.0f, 2.0f, 3.0f));
        Assert.Equal(
            new Matrix3(20.0f, -15.0f, 63.0f, 32.0f, -18.0f, 123.0f, 44.0f, -21.0f, 183.0f),
            matrix3 * other3);

        var scaled3 = matrix3;
        scaled3.SetScale(new Vector3f(11.0f, 13.0f, 17.0f));
        Assert.Equal(new Matrix3(11.0f, 3.0f, 4.0f, 5.0f, 13.0f, 7.0f, 8.0f, 9.0f, 17.0f), scaled3);
        scaled3.SetScale(19.0f);
        Assert.Equal(new Matrix3(19.0f, 3.0f, 4.0f, 5.0f, 19.0f, 7.0f, 8.0f, 9.0f, 19.0f), scaled3);

        var signed = new Matrix3(-2.0f, 0.0f, 0.0f, 0.0f, 3.0f, 0.0f, 0.0f, 0.0f, -4.0f);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), signed.Scale);
        Assert.Equal(new Vector3f(-2.0f, 3.0f, -4.0f), signed.SignedScale(Matrix3.Identity));
        Assert.Equal(
            new Matrix3(4.0f, 9.0f, 16.0f, 10.0f, 18.0f, 28.0f, 16.0f, 27.0f, 40.0f),
            matrix3.Scaled(new Vector3f(2.0f, 3.0f, 4.0f)));
        Assert.Equal(
            new Matrix3(2.0f, 5.0f, 8.0f, 3.0f, 6.0f, 9.0f, 4.0f, 7.0f, 10.0f),
            matrix3.Transpose);
        Assert.True(matrix3.ApproximatelyEquivalent(new Matrix3(matrix3), 0.0001f));

        foreach (var value in new[]
        {
            new Matrix3(float.NaN, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, float.NaN, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, float.NaN, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, float.NaN, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, float.NaN, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.NaN, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.NaN, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.NaN, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.NaN)
        })
            Assert.True(value.IsNaN);
        Assert.False(Matrix3.Zero.IsNaN);

        foreach (var value in new[]
        {
            new Matrix3(float.PositiveInfinity, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.PositiveInfinity, 0.0f, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.PositiveInfinity, 0.0f),
            new Matrix3(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, float.PositiveInfinity)
        })
            Assert.True(value.IsInf);
        Assert.False(Matrix3.Zero.IsInf);

        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            9.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 9.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 9.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 9.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 5.0f, 9.0f, 7.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 9.0f, 8.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 9.0f, 9.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 10.0f, 10.0f)));
        Assert.False(matrix3.ApproximatelyEquivalent(new Matrix3(
            2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 11.0f)));
        Assert.True(matrix3.Equals((object)matrix3));
        Assert.False(matrix3.Equals((object?)null));
        Assert.Contains("2", matrix3.ToString());
        _ = matrix3.ToHash();
        _ = matrix3.GetHashCode();
    }

    [Fact]
    public void Quaternion_covers_alternate_rotation_and_interpolation_paths()
    {
        Assert.Equal(Quaternion.IDENTITY, Quaternion.FromAngularVelocity(Vector3f.Zero));

        var oppositeRight = new Quaternion(Vector3f.Right, Vector3f.Left);
        Assert.True((oppositeRight * Vector3f.Right).ApproximatelyEquivalent(Vector3f.Left, 0.0001f));

        foreach (var axis in new[] { Vector3f.Right, Vector3f.Up, Vector3f.Forward })
        {
            var matrix = new Matrix3(180.0f, axis);
            Assert.True(new Quaternion(matrix).RotationMatrix.ApproximatelyEquivalent(matrix, 0.0001f));
        }

        var lookFallback = new Quaternion(10.0f, 20.0f, 30.0f);
        Assert.True(lookFallback.FromLookRotation(Vector3f.Forward, Vector3f.Forward));
        Assert.True(lookFallback.Equivalent(Quaternion.IDENTITY));

        var nonUnit = new Quaternion(2.0f, 0.0f, 0.0f, 0.0f);
        Assert.Equal(new Quaternion(0.5f, 0.0f, 0.0f, 0.0f), nonUnit.Inverse);
        Assert.Equal(Quaternion.IDENTITY, Quaternion.ZERO.Inverse);
        Assert.Equal(Quaternion.IDENTITY, Quaternion.IDENTITY.Inverse);
        Assert.Equal(Quaternion.IDENTITY, nonUnit.Normalized);
        Assert.Equal(Quaternion.IDENTITY, Quaternion.IDENTITY.Normalized);

        var positiveRotation = new Quaternion(60.0f, Vector3f.Up);
        var expectedVelocity = new Vector3f(0.0f, 60.0f * MathDefs.DegToRad, 0.0f);
        Assert.True(positiveRotation.AngularVelocity.ApproximatelyEquivalent(expectedVelocity, 0.0001f));
        Assert.True((-positiveRotation).AngularVelocity.ApproximatelyEquivalent(expectedVelocity, 0.0001f));
        Assert.Equal(Vector3f.Zero, Quaternion.IDENTITY.AngularVelocity);
        Assert.Equal(Vector3f.Up, Quaternion.IDENTITY.Axis);
        Assert.True(positiveRotation.Axis.ApproximatelyEquivalent(Vector3f.Up, 0.0001f));

        Assert.True(Quaternion.IDENTITY.Slerp(positiveRotation, 0.5f)
            .Equivalent(new Quaternion(30.0f, Vector3f.Up), 0.001f));
        Assert.Equal(Quaternion.IDENTITY, Quaternion.IDENTITY.Slerp(Quaternion.IDENTITY, 0.5f));
        Assert.True(Quaternion.IDENTITY.Slerp(-positiveRotation, 0.5f)
            .Equivalent(new Quaternion(30.0f, Vector3f.Up), 0.001f));
        Assert.True(Quaternion.IDENTITY.Nlerp(-positiveRotation, 0.5f, shortestPath: true)
            .Equivalent(new Quaternion(30.0f, Vector3f.Up), 0.001f));
        Assert.True(Quaternion.IDENTITY.Nlerp(positiveRotation, 0.5f)
            .Equivalent(new Quaternion(30.0f, Vector3f.Up), 0.001f));

        Assert.True(MathF.Abs(new Quaternion(30.0f, Vector3f.Up).TwistAngle(Vector3f.Up) - 30.0f) < 0.001f);
        Assert.True(MathF.Abs(new Quaternion(30.0f, Vector3f.Up).TwistAngle(Vector3f.Down) + 30.0f) < 0.001f);

        foreach (var value in new[]
        {
            new Quaternion(float.PositiveInfinity, 0.0f, 0.0f, 0.0f),
            new Quaternion(0.0f, float.PositiveInfinity, 0.0f, 0.0f),
            new Quaternion(0.0f, 0.0f, float.PositiveInfinity, 0.0f),
            new Quaternion(0.0f, 0.0f, 0.0f, float.PositiveInfinity)
        })
            Assert.True(value.IsInf);
        Assert.False(Quaternion.ZERO.IsInf);
        Assert.True(positiveRotation.Equals((object)positiveRotation));
        Assert.False(positiveRotation.Equals((object?)null));
        Assert.Contains("0.866", positiveRotation.ToString());
        _ = positiveRotation.ToHash();
        _ = positiveRotation.GetHashCode();
    }

    [Fact]
    public void Rect_covers_merge_clip_containment_and_value_paths()
    {
        var undefined = new Rect();
        Assert.False(undefined.Defined);
        undefined.Clear();
        Assert.False(undefined.Defined);

        var rect = new Rect(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));
        Assert.Equal(new[] { 1.0f, 2.0f, 3.0f, 4.0f }, rect.Data().ToArray());
        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), rect.ToVector4());
        Assert.Equal(new Vector2f(2.0f, 3.0f), rect.Center);
        Assert.Equal(new Vector2f(2.0f, 2.0f), rect.Size);
        Assert.Equal(new Vector2f(1.0f, 1.0f), rect.HalfSize);
        Assert.Equal(1.0f, rect.Left);
        Assert.Equal(2.0f, rect.Top);
        Assert.Equal(3.0f, rect.Right);
        Assert.Equal(4.0f, rect.Bottom);

        var merged = rect;
        merged.Merge(new Rect(-1.0f, 0.0f, 5.0f, 6.0f));
        Assert.Equal(new Rect(-1.0f, 0.0f, 5.0f, 6.0f), merged);
        merged.Merge(new Rect(-1.0f, 0.0f, 5.0f, 6.0f));
        merged.Merge(new Vector2f(-2.0f, 7.0f));
        Assert.Equal(new Rect(-2.0f, 0.0f, 5.0f, 7.0f), merged);

        var clipped = new Rect(0.0f, 0.0f, 10.0f, 10.0f);
        clipped.Clip(new Rect(2.0f, -1.0f, 12.0f, 9.0f));
        Assert.Equal(new Rect(2.0f, 0.0f, 10.0f, 9.0f), clipped);

        var disjoint = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
        disjoint.Clip(new Rect(2.0f, 2.0f, 3.0f, 3.0f));
        Assert.False(disjoint.Defined);

        Assert.Equal(new Rect(2.0f, 4.0f, 6.0f, 8.0f), rect * 2.0f);
        Assert.Equal(rect, (rect * 2.0f) / 2.0f);
        Assert.Equal(new Rect(2.0f, 4.0f, 6.0f, 8.0f), rect + rect);
        Assert.Equal(Rect.Zero, rect - rect);
        Assert.True(rect.ApproximatelyEquivalent(new Rect(1.0005f, 2.0f, 3.0f, 4.0f), 0.001f));
        Assert.False(rect.ApproximatelyEquivalent(new Rect(9.0f, 2.0f, 3.0f, 4.0f)));

        Assert.Equal(Intersection.Inside, rect.IsInside(new Vector2f(1.0f, 2.0f)));
        Assert.Equal(Intersection.Outside, rect.IsInside(new Vector2f(0.0f, 2.0f)));
        Assert.Equal(Intersection.Inside, rect.IsInside(new Rect(1.0f, 2.0f, 3.0f, 4.0f)));
        Assert.Equal(Intersection.Intersects, rect.IsInside(new Rect(0.0f, 2.0f, 2.0f, 3.0f)));
        Assert.Equal(Intersection.Outside, rect.IsInside(new Rect(4.0f, 2.0f, 5.0f, 3.0f)));

        Assert.True(rect == new Rect(rect));
        Assert.False(rect == new Rect(9.0f, 2.0f, 3.0f, 4.0f));
        Assert.True(rect != new Rect(9.0f, 2.0f, 3.0f, 4.0f));
        Assert.False(rect != new Rect(rect));
        Assert.True(rect.Equals((object)rect));
        Assert.False(rect.Equals((object?)null));
        Assert.Contains("1", rect.ToString());
        _ = rect.ToHash();
        _ = rect.GetHashCode();
        Assert.Equal(rect, rect.Clone());
        Assert.Equal(rect, ((ICloneable)rect).Clone());
    }

    [Fact]
    public void Ray_covers_intersection_and_geometry_branches()
    {
        var ray = new Ray(new Vector3f(-2.0f, 0.0f, 0.0f), Vector3f.Right);
        var sameRay = new Ray(ray);
        var differentOrigin = new Ray(new Vector3f(-3.0f, 0.0f, 0.0f), Vector3f.Right);
        var differentDirection = new Ray(ray.Origin, Vector3f.Up);
        Assert.True(ray == sameRay);
        Assert.False(ray == differentOrigin);
        Assert.False(ray == differentDirection);
        Assert.False(ray != sameRay);
        Assert.True(ray != differentOrigin);
        Assert.True(ray.Equals((object)ray));
        Assert.False(ray.Equals((object?)null));
        _ = ray.GetHashCode();
        Assert.Equal(ray, ray.Clone());

        var box = new BoundingBox(-1.0f, 1.0f);
        var expectedHits = new[]
        {
            (new Vector3f(-2.0f, 0.0f, 0.0f), Vector3f.Right, new Vector3f(-1.0f, 0.0f, 0.0f)),
            (new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Left, new Vector3f(1.0f, 0.0f, 0.0f)),
            (new Vector3f(0.0f, -2.0f, 0.0f), Vector3f.Up, new Vector3f(0.0f, -1.0f, 0.0f)),
            (new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down, new Vector3f(0.0f, 1.0f, 0.0f)),
            (new Vector3f(0.0f, 0.0f, -2.0f), Vector3f.Forward, new Vector3f(0.0f, 0.0f, -1.0f)),
            (new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Back, new Vector3f(0.0f, 0.0f, 1.0f))
        };
        foreach (var (origin, direction, expectedNormal) in expectedHits)
        {
            var hit = new Ray(origin, direction).HitDistanceAndNormal(box);
            Assert.Equal(1.0f, hit.Distance);
            Assert.Equal(expectedNormal, hit.Normal);
        }

        foreach (var origin in new[]
        {
            new Vector3f(-2.0f, -2.0f, 0.0f),
            new Vector3f(-2.0f, 2.0f, 0.0f),
            new Vector3f(-2.0f, 0.0f, -2.0f),
            new Vector3f(-2.0f, 0.0f, 2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Right).HitDistanceAndNormal(box).Distance));

        foreach (var origin in new[]
        {
            new Vector3f(-2.0f, -2.0f, 0.0f),
            new Vector3f(2.0f, -2.0f, 0.0f),
            new Vector3f(0.0f, -2.0f, -2.0f),
            new Vector3f(0.0f, -2.0f, 2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Up).HitDistanceAndNormal(box).Distance));

        foreach (var origin in new[]
        {
            new Vector3f(-2.0f, 0.0f, -2.0f),
            new Vector3f(2.0f, 0.0f, -2.0f),
            new Vector3f(0.0f, -2.0f, -2.0f),
            new Vector3f(0.0f, 2.0f, -2.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Forward).HitDistanceAndNormal(box).Distance));

        Assert.Equal(0.0f, new Ray(Vector3f.Zero, Vector3f.Right).HitDistanceAndNormal(box).Distance);
        Assert.True(float.IsPositiveInfinity(new Ray(Vector3f.Zero, Vector3f.Right)
            .HitDistanceAndNormal(new BoundingBox()).Distance));

        var plane = new Plane(Vector3f.Up, Vector3f.Zero);
        Assert.Equal(float.PositiveInfinity, new Ray(Vector3f.Zero, Vector3f.Right).HitDistance(plane));
        Assert.Equal(float.PositiveInfinity, new Ray(new Vector3f(0.0f, -1.0f, 0.0f), Vector3f.Down).HitDistance(plane));
        Assert.Equal(1.0f, new Ray(Vector3f.Up, Vector3f.Down).HitDistance(plane));

        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        Assert.Equal(0.0f, new Ray(Vector3f.Zero, Vector3f.Right).HitDistance(sphere));
        Assert.True(float.IsPositiveInfinity(new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Right)
            .HitDistance(sphere)));
        Assert.Equal(1.0f, new Ray(new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Left).HitDistance(sphere));
        Assert.Equal(-1.0f, new Ray(new Vector3f(2.0f, 0.0f, 0.0f), Vector3f.Right).HitDistance(sphere));

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 4.0f));
        Assert.Equal(0.0f, new Ray(new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Forward).HitDistance(frustum));
        Assert.True(new Ray(new Vector3f(0.0f, 0.0f, 2.0f), Vector3f.Forward)
            .HitDistance(frustum, solidInside: false) > 0.0f);
        Assert.True(float.IsPositiveInfinity(new Ray(new Vector3f(5.0f, 0.0f, 2.0f), Vector3f.Right)
            .HitDistance(frustum)));

        var v0 = new Vector3f(0.0f, 0.0f, 0.0f);
        var v1 = new Vector3f(0.0f, 1.0f, 0.0f);
        var v2 = new Vector3f(1.0f, 0.0f, 0.0f);
        var triangleRay = new Ray(new Vector3f(0.25f, 0.25f, -1.0f), Vector3f.Forward);
        Assert.Equal(1.0f, triangleRay.HitDistance(v0, v1, v2, out var triangleNormal, out var barycentric));
        Assert.Equal(new Vector3f(0.0f, 0.0f, -1.0f), triangleNormal);
        Assert.True(barycentric.ApproximatelyEquivalent(new Vector3f(0.5f, 0.25f, 0.25f), 0.0001f));
        foreach (var origin in new[]
        {
            new Vector3f(0.25f, -0.25f, -1.0f),
            new Vector3f(0.25f, 1.25f, -1.0f),
            new Vector3f(-0.25f, 0.25f, -1.0f),
            new Vector3f(0.75f, 0.5f, -1.0f),
            new Vector3f(0.25f, 0.25f, 1.0f)
        })
            Assert.True(float.IsPositiveInfinity(new Ray(origin, Vector3f.Forward)
                .HitDistance(v0, v1, v2, out _, out _)));
        Assert.True(float.IsPositiveInfinity(triangleRay.HitDistance(v0, v2, v1, out _, out _)));

        const int stride = 20;
        Span<byte> vertices = stackalloc byte[stride * 3];
        Write(vertices, 0, v0);
        Write(vertices, 12, Vector2f.Zero);
        Write(vertices, stride, v1);
        Write(vertices, stride + 12, Vector2f.Up);
        Write(vertices, stride * 2, v2);
        Write(vertices, stride * 2 + 12, Vector2f.Right);

        Assert.Equal(1.0f, triangleRay.HitDistance(vertices, stride, 0u, 3u, out _, out var nonIndexedUv, 12u));
        Assert.True(nonIndexedUv.ApproximatelyEquivalent(new Vector2f(0.25f, 0.25f), 0.0001f));
        Assert.True(float.IsPositiveInfinity(triangleRay.HitDistance(vertices, stride, 0u, 0u, out _, out var emptyUv, 12u)));
        Assert.Equal(Vector2f.Zero, emptyUv);

        Span<byte> indices16 = stackalloc byte[sizeof(ushort) * 3];
        Write(indices16, 0, (ushort)0);
        Write(indices16, sizeof(ushort), (ushort)1);
        Write(indices16, sizeof(ushort) * 2, (ushort)2);
        Assert.Equal(1.0f, triangleRay.HitDistance(
            vertices, stride, indices16, sizeof(ushort), 0u, 3u, out _, out var indexed16Uv, 12u));
        Assert.True(indexed16Uv.ApproximatelyEquivalent(new Vector2f(0.25f, 0.25f), 0.0001f));
        Assert.True(float.IsPositiveInfinity(triangleRay.HitDistance(
            vertices, stride, indices16, sizeof(ushort), 0u, 0u, out _, out var empty16Uv, 12u)));
        Assert.Equal(Vector2f.Zero, empty16Uv);

        Span<byte> indices32 = stackalloc byte[sizeof(uint) * 3];
        Write(indices32, 0, 0u);
        Write(indices32, sizeof(uint), 1u);
        Write(indices32, sizeof(uint) * 2, 2u);
        Assert.Equal(1.0f, triangleRay.HitDistance(
            vertices, stride, indices32, sizeof(uint), 0u, 3u, out _, out var indexed32Uv, 12u));
        Assert.True(indexed32Uv.ApproximatelyEquivalent(new Vector2f(0.25f, 0.25f), 0.0001f));
        Assert.True(float.IsPositiveInfinity(triangleRay.HitDistance(
            vertices, stride, indices32, sizeof(uint), 0u, 0u, out _, out var empty32Uv, 12u)));
        Assert.Equal(Vector2f.Zero, empty32Uv);

        Assert.False(triangleRay.InsideGeometry(vertices, stride, 0u, 3u));
        Assert.True(new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Back)
            .InsideGeometry(vertices, stride, 0u, 3u));
        Assert.False(triangleRay.InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 3u));
        Assert.True(new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Back)
            .InsideGeometry(vertices, stride, indices16, sizeof(ushort), 0u, 3u));
        Assert.False(triangleRay.InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 3u));
        Assert.True(new Ray(new Vector3f(0.25f, 0.25f, 1.0f), Vector3f.Back)
            .InsideGeometry(vertices, stride, indices32, sizeof(uint), 0u, 3u));
    }

    [Fact]
    public void TetrahedralMesh_covers_public_boundaries_and_implementation_solvers()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        ]);
        var tetrahedron = new Tetrahedron();
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 1;
        tetrahedron.Indices[2] = 2;
        tetrahedron.Indices[3] = 3;
        tetrahedron.CalculateInnerMatrix(mesh.Vertices);
        mesh.Tetrahedrons.Add(tetrahedron);
        mesh.NumInnerTetrahedrons = 1;

        var edges = new List<(uint First, uint Second)> { (99, 100) };
        mesh.CollectEdges(edges);
        Assert.Equal(
        [
            (0u, 0u), (0u, 1u), (0u, 2u), (0u, 3u),
            (1u, 1u), (1u, 2u), (1u, 3u),
            (2u, 2u), (2u, 3u), (3u, 3u)
        ], edges);

        var circumsphere = mesh.GetTetrahedronCircumsphere(0);
        Assert.True(circumsphere.Center.ApproximatelyEquivalent(new Vector3d(0.5, 0.5, 0.5), 0.0001f));
        Assert.True(System.Math.Abs(circumsphere.Radius - System.Math.Sqrt(0.75)) < 0.0001);

        var degenerate = new TetrahedralMesh();
        degenerate.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            new Vector3f(1.0f, 1.0f, 0.0f)
        ]);
        var degenerateTetrahedron = new Tetrahedron();
        degenerateTetrahedron.Indices[0] = 0;
        degenerateTetrahedron.Indices[1] = 1;
        degenerateTetrahedron.Indices[2] = 2;
        degenerateTetrahedron.Indices[3] = 3;
        degenerate.Tetrahedrons.Add(degenerateTetrahedron);
        var degenerateSphere = degenerate.GetTetrahedronCircumsphere(0);
        Assert.Equal((double)(MathDefs.LargeValue * MathDefs.LargeValue), degenerateSphere.Radius);

        var empty = new TetrahedralMesh();
        var emptyHint = 17u;
        Assert.Equal(Vector4f.Zero, empty.GetInterpolationFactors(Vector3f.One, ref emptyHint));
        Assert.Equal(17u, emptyHint);

        var validHint = 99u;
        var weights = mesh.GetInterpolationFactors(new Vector3f(0.25f, 0.25f, 0.25f), ref validHint);
        Assert.True(weights.ApproximatelyEquivalent(new Vector4f(0.25f, 0.25f, 0.25f, 0.25f), 0.0001f));
        Assert.Equal(0u, validHint);

        var findTetrahedron = PrivateInstance("FindTetrahedron", typeof(Vector3f), typeof(List<bool>));
        var removed = new List<bool> { false };
        foreach (var position in new[]
        {
            new Vector3f(0.4f, 0.4f, 0.4f),
            new Vector3f(-0.25f, 0.25f, 0.25f),
            new Vector3f(0.25f, -0.25f, 0.25f),
            new Vector3f(0.25f, 0.25f, -0.25f)
        })
            Assert.Equal(uint.MaxValue, (uint)findTetrahedron.Invoke(mesh, [position, removed])!);
        Assert.Equal(uint.MaxValue, (uint)findTetrahedron.Invoke(mesh, [Vector3f.Zero, new List<bool> { true }])!);

        var solveCubicEquation = PrivateStatic("SolveCubicEquation", typeof(double[]), typeof(double), typeof(double), typeof(double), typeof(double));
        var cubicRoots = new double[3];
        Assert.Equal(3, (int)solveCubicEquation.Invoke(null, [cubicRoots, -6.0, 11.0, -6.0, 1e-12])!);
        Array.Sort(cubicRoots);
        Assert.Equal(1.0, cubicRoots[0], 8);
        Assert.Equal(2.0, cubicRoots[1], 8);
        Assert.Equal(3.0, cubicRoots[2], 8);
        Assert.Equal(1, (int)solveCubicEquation.Invoke(null, [cubicRoots, 0.0, 0.0, 1.0, MathDefs.Epsilon])!);
        Assert.Equal(1, (int)solveCubicEquation.Invoke(null, [cubicRoots, 0.0, 0.0, -1.0, MathDefs.Epsilon])!);

        var solveQuadratic = PrivateStatic("SolveQuadratic", typeof(Vector3f));
        Assert.True(MathF.Abs((float)solveQuadratic.Invoke(null, [new Vector3f(0.0f, 2.0f, -4.0f)])! - 2.0f) < 0.00001f);
        Assert.True(MathF.Abs((float)solveQuadratic.Invoke(null, [new Vector3f(1.0f, -3.0f, 2.0f)])! - 1.0f) < 0.00001f);
        Assert.True(MathF.Abs((float)solveQuadratic.Invoke(null, [new Vector3f(1.0f, 0.0f, 1.0f)])!) < 0.00001f);

        var edgeType = typeof(TetrahedralMesh).GetNestedType("TetrahedralMeshEdge", BindingFlags.NonPublic)!;
        var edgeConstructor = edgeType.GetConstructor([typeof(uint), typeof(uint), typeof(uint), typeof(float)])!;
        var firstEdge = edgeConstructor.Invoke([3u, 1u, 0u, 2.0f]);
        var secondEdge = edgeConstructor.Invoke([2u, 4u, 0u, 1.0f]);
        var sameIndicesHigherScore = edgeConstructor.Invoke([1u, 3u, 0u, 4.0f]);
        var lessThan = edgeType.GetMethod("op_LessThan", BindingFlags.Public | BindingFlags.Static)!;
        var greaterThan = edgeType.GetMethod("op_GreaterThan", BindingFlags.Public | BindingFlags.Static)!;
        var isSameEdge = edgeType.GetMethod("IsSameEdge", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.True((bool)lessThan.Invoke(null, [firstEdge, secondEdge])!);
        Assert.True((bool)lessThan.Invoke(null, [firstEdge, sameIndicesHigherScore])!);
        Assert.True((bool)greaterThan.Invoke(null, [secondEdge, firstEdge])!);
        Assert.True((bool)isSameEdge.Invoke(firstEdge, [sameIndicesHigherScore])!);
        Assert.False((bool)isSameEdge.Invoke(firstEdge, [secondEdge])!);

        var outerMesh = new TetrahedralMesh();
        outerMesh.Vertices.AddRange([Vector3f.Zero, Vector3f.Right, Vector3f.Up]);
        outerMesh.HullNormals.AddRange([Vector3f.Zero, Vector3f.Zero, Vector3f.Zero]);
        var outerTetrahedron = new Tetrahedron();
        outerTetrahedron.Indices[0] = 0;
        outerTetrahedron.Indices[1] = 1;
        outerTetrahedron.Indices[2] = 2;
        outerTetrahedron.Indices[3] = Tetrahedron.Infinity3;
        outerMesh.Tetrahedrons.Add(outerTetrahedron);
        var calculateOuterMatrices = PrivateInstance("CalculateOuterMatrices");
        calculateOuterMatrices.Invoke(outerMesh, null);
        Assert.Equal(Tetrahedron.Infinity2, outerMesh.Tetrahedrons[0].Indices[3]);
    }

    private static MethodInfo PrivateStatic(string name, params Type[] parameterTypes) =>
        typeof(TetrahedralMesh).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, parameterTypes)!;

    private static MethodInfo PrivateInstance(string name, params Type[] parameterTypes) =>
        typeof(TetrahedralMesh).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, parameterTypes)!;

    private static void Write<T>(Span<byte> destination, int offset, T value) where T : struct =>
        MemoryMarshal.Write(destination[offset..], in value);
}
