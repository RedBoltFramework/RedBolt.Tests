namespace RedBolt.Tests;

using RedBolt.Math;

public sealed class TransformTests
{
    [Fact]
    public void Transform_identity_and_matrix_round_trip_preserve_components()
    {
        Assert.Equal(Vector3f.Zero, Transform.Identity.Position);
        Assert.Equal(Quaternion.IDENTITY, Transform.Identity.Rotation);
        Assert.Equal(Vector3f.One, Transform.Identity.Scale);

        var transform = new Transform(
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Quaternion(30.0f, Vector3f.Up),
            new Vector3f(2.0f, 3.0f, 4.0f));
        var roundTrip = Transform.FromMatrix3x4(transform.ToMatrix3x4());

        Assert.True(roundTrip.Position.ApproximatelyEquivalent(transform.Position, 0.001f));
        Assert.True(roundTrip.Rotation.ApproximatelyEquivalent(transform.Rotation, 0.001f));
        Assert.True(roundTrip.Scale.ApproximatelyEquivalent(transform.Scale, 0.001f));
    }

    [Fact]
    public void Transform_lerp_and_double_transform_lerp_are_componentwise()
    {
        var first = new Transform(Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One);
        var second = new Transform(new Vector3f(10.0f, 4.0f, -2.0f), Quaternion.IDENTITY, new Vector3f(3.0f, 5.0f, 7.0f));
        var midpoint = first.Lerp(second, 0.5f);

        Assert.Equal(new Vector3f(5.0f, 2.0f, -1.0f), midpoint.Position);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), midpoint.Scale);

        var doubleFirst = DoubleTransform.Identity;
        var doubleSecond = new DoubleTransform
        {
            Position = new Vector3d(10.0, 4.0, -2.0),
            Rotation = Quaternion.IDENTITY,
            Scale = new Vector3f(3.0f, 5.0f, 7.0f)
        };
        var doubleMidpoint = doubleFirst.Lerp(doubleSecond, 0.5f);

        Assert.Equal(new Vector3d(5.0, 2.0, -1.0), doubleMidpoint.Position);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), doubleMidpoint.Scale);
    }
}
