// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.PhysicsJolt;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltSpanProjectionTests
{
    public JoltSpanProjectionTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Gjk_closest_points_simplex_fills_span_outputs()
    {
        using var gjk = new global::RedBolt.Bindings.Jolt.GJKClosestPoint();
        var y = new BaseRealVector3<float>[4];
        var p = new BaseRealVector3<float>[4];
        var q = new BaseRealVector3<float>[4];
        uint numPoints = 99;

        gjk.GetClosestPointsSimplex(y, p, q, ref numPoints);

        Assert.True(numPoints <= 4);
        foreach (var point in y.Concat(p).Concat(q))
        {
            Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
        }
    }

    [Fact]
    public void Gjk_closest_points_simplex_rejects_short_spans()
    {
        using var gjk = new global::RedBolt.Bindings.Jolt.GJKClosestPoint();
        var ok = new BaseRealVector3<float>[4];
        var shortSpan = new BaseRealVector3<float>[3];
        uint numPoints = 0;

        Assert.Throws<ArgumentException>(() => gjk.GetClosestPointsSimplex(shortSpan, ok, ok, ref numPoints));
        Assert.Throws<ArgumentException>(() => gjk.GetClosestPointsSimplex(ok, shortSpan, ok, ref numPoints));
        Assert.Throws<ArgumentException>(() => gjk.GetClosestPointsSimplex(ok, ok, shortSpan, ref numPoints));
    }
}
