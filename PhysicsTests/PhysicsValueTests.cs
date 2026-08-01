namespace RedBolt.Tests;

using RedBolt.JoltPhysics;
using RedBolt.Math;

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
}
