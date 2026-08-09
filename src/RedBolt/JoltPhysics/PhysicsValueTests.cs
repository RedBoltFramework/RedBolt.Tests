namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.PhysicsJolt;

public sealed class PhysicsValueTests
{
    [Fact]
    public void Physics_enums_and_value_payloads_match_native_order()
    {
        Assert.Equal(0, (int)ShapeType.Box);
        Assert.Equal(1, (int)ShapeType.Sphere);
        Assert.Equal(9, (int)ShapeType.GImpactMesh);
        Assert.Equal(0, (int)CollisionEventMode.Never);
        Assert.Equal(1, (int)CollisionEventMode.Active);
        Assert.Equal(2, (int)CollisionEventMode.Always);

        var result = new PhysicsRaycastResult();
        var contact = new PhysicsContact();
        Assert.Null(result.Body);
        Assert.Equal(Vector3f.Zero, result.Position);
        Assert.Equal(Vector3f.Zero, result.Normal);
        Assert.Equal(0.0f, result.Distance);
        Assert.Equal(0.0f, result.HitFraction);
        Assert.Equal(Vector3f.Zero, contact.Position);
        Assert.Equal(Vector3f.Zero, contact.Normal);
        Assert.Equal(0.0f, contact.Distance);
        Assert.Equal(0.0f, contact.Impulse);
    }

    [Fact]
    public void Detached_components_keep_safe_defaults_and_clamp_local_values()
    {
        var body = new RigidBody();
        var shape = new CollisionShape();

        Assert.Null(body.JoltBodyId);
        Assert.False(body.IsActive);
        Assert.Equal(0.0f, body.Mass);
        Assert.Equal(0.5f, body.Friction);
        Assert.Equal(Vector3f.One, body.LinearFactor);
        Assert.True(body.UseGravity);
        Assert.Equal(ShapeType.Box, shape.ShapeType);
        Assert.True(shape.IsSupported);

        body.Mass = -1.0f;
        body.LinearDamping = -1.0f;
        body.CcdRadius = -1.0f;
        body.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY);
        shape.Margin = -1.0f;
        shape.ShapeType = ShapeType.TriangleMesh;

        Assert.Equal(0.0f, body.Mass);
        Assert.Equal(0.0f, body.LinearDamping);
        Assert.Equal(0.0f, body.CcdRadius);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), body.Position);
        Assert.Equal(0.0f, shape.Margin);
        Assert.False(shape.IsSupported);

        body.Dispose();
        body.Dispose();
        shape.Dispose();
        shape.Dispose();
    }
}
