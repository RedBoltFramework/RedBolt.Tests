namespace RedBolt.Tests.MathTests;

using System.Reflection;

using RedBolt;
using RedBolt.Math;

public sealed class ResidualCoverageTests
{
    [Fact]
    public void Math_defs_residual_helpers_cover_scalar_vector_encoding_and_random_paths()
    {
        Assert.True(MathDefs.IsNaN(float.NaN));
        Assert.True(MathDefs.IsNaN(double.NaN));
        Assert.True(MathDefs.IsInf(float.PositiveInfinity));
        Assert.True(MathDefs.IsInf(double.NegativeInfinity));

        Assert.Equal(new Vector2f(0.0f, 1.0f), MathDefs.VectorClamp(
            new Vector2f(-1.0f, 2.0f), Vector2f.Zero, Vector2f.One));
        Assert.Equal(new Vector3f(0.0f, 1.0f, 0.5f), MathDefs.VectorClamp(
            new Vector3f(-1.0f, 2.0f, 0.5f), Vector3f.Zero, Vector3f.One));
        Assert.Equal(new Vector4f(0.0f, 1.0f, 0.5f, 1.0f), MathDefs.VectorClamp(
            new Vector4f(-1.0f, 2.0f, 0.5f, 2.0f), Vector4f.Zero, Vector4f.One));

        Assert.Equal(30.0f, MathDefs.Asin(0.5f), 0.0001f);
        Assert.Equal(45.0f, MathDefs.Atan(1.0f), 0.0001f);
        Assert.Equal(0.0f, MathDefs.Ln(1.0f));
        Assert.Equal(4.0f, MathDefs.SnapFloor(5.9f, 2.0f));
        Assert.Equal(6.0f, MathDefs.SnapCeil(5.1f, 2.0f));
        Assert.Equal(1, MathDefs.Wrap(6, 0, 5));

        MathDefs.SinCos(0.0f, out var sin, out var cos);
        Assert.Equal(0.0f, sin);
        Assert.Equal(1.0f, cos);
        Assert.Equal(5, MathDefs.MaxVariableLengthBytes<uint>());
        Assert.Equal(10, MathDefs.MaxVariableLengthBytes<ulong>());

        Span<byte> bytes = stackalloc byte[16];
        Assert.Equal(5u, MathDefs.EncodeVariableLength(uint.MaxValue, bytes));
        Assert.Equal(10u, MathDefs.EncodeVariableLength(ulong.MaxValue, bytes));

        _ = MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(sbyte.MinValue));
        foreach (var value in new sbyte[] { -1, 0, 1, sbyte.MaxValue })
            _ = MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value));
        foreach (var value in new short[] { short.MinValue, -1, 0, 1, short.MaxValue })
            _ = MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value));
        foreach (var value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            _ = MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value));
        foreach (var value in new[] { long.MinValue, -1L, 0L, 1L, long.MaxValue })
            _ = MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(value));

        MathDefs.SetRandomSeed(17);
        Assert.Equal(17u, MathDefs.GetRandomSeed());
        _ = MathDefs.RandStandardNormal();
        _ = MathDefs.Random();
        _ = MathDefs.Random(2.0f);
        _ = MathDefs.Random(-1.0f, 1.0f);
        _ = MathDefs.Random(3);
        _ = MathDefs.Random(-2, 2);
        _ = MathDefs.RandomNormal(1.0f, 2.0f);
    }

    [Fact]
    public void Random_engine_residual_helpers_cover_ranges_vectors_and_state()
    {
        var engine = new RandomEngine(1);
        Assert.Equal(0u, engine.GetUInt(0));
        Assert.InRange(engine.GetUInt(2), 0u, 1u);
        Assert.InRange(engine.GetUInt(3, 7), 3u, 6u);
        Assert.InRange(engine.GetInt(-3, 4), -3, 3);
        Assert.InRange(engine.GetDouble(), 0.0, 1.0);
        Assert.InRange(engine.GetDouble(-2.0, 2.0), -2.0, 2.0);
        Assert.InRange(engine.GetFloat(), 0.0f, 1.0f);
        Assert.InRange(engine.GetFloat(-2.0f, 2.0f), -2.0f, 2.0f);
        Assert.True(engine.GetBool(1.0f));
        Assert.False(engine.GetBool(0.0f));
        _ = engine.GetNormalFloat(1.0f, 2.0f);

        Span<int> values = stackalloc int[] { 1, 2, 3, 4 };
        engine.Shuffle(values);
        Assert.Equal(4, values.Length);

        var direction2 = engine.GetDirectionVector2();
        var direction3 = engine.GetDirectionVector3();
        var quaternion = engine.GetQuaternion();
        Assert.InRange(direction2.Length, 0.99f, 1.01f);
        Assert.InRange(direction3.Length, 0.99f, 1.01f);
        Assert.InRange(quaternion.LengthSquared, 0.99f, 1.01f);
        _ = engine.GetVector2(Vector2f.Left, Vector2f.Right);
        _ = engine.GetVector3(Vector3f.Left, Vector3f.Right);
        _ = engine.GetVector3(new BoundingBox(-1.0f, 1.0f));

        var saved = engine.Save();
        var restored = new RandomEngine(saved);
        Assert.Equal(saved, restored.Save());
        Assert.Equal(saved, ((RandomEngine)engine.Clone()).Save());
        restored.Load("not-a-state");
        restored.Load("1");
        Assert.Equal("1", restored.Save());
        Assert.Equal(RandomEngine.MaxRange(), RandomEngine.MaxRange());
        _ = RandomEngine.GetDefaultEngine();
    }

    [Fact]
    public void Integer_vectors_cover_remaining_operators_data_and_conversions()
    {
        var two = new Vector2i(-6, 4);
        Assert.Equal(( -6, 4), two.Tie());
        Assert.Equal(new Vector2i(6, -4), -two);
        Assert.Equal(new Vector2i(-3, 2), two / 2);
        Assert.Equal(new Vector2i(2, 2), new Vector2i(4, 6) / new Vector2i(2, 3));
        Assert.Equal(new Vector2i(12, 8), two * new Vector2i(-2, 2));
        Assert.Equal(new Vector2i(-12, 8), 2 * two);
        Assert.Equal(new Vector2i(6, 4), Vector2i.Abs(two));
        Assert.Equal(new Vector2i(-6, 1), Vector2i.Min(two, Vector2i.One));
        Assert.Equal(new Vector2i(1, 4), Vector2i.Max(two, Vector2i.One));
        two.MutableData()[0] = -5;
        Assert.Equal(-5, two.Data()[0]);

        var three = new Vector3i(-6, 4, 9);
        Assert.Equal((-6, 4, 9), three.Tie());
        Assert.Equal(new Vector3i(6, -4, -9), -three);
        Assert.Equal(new Vector3i(-3, 2, 4), three / 2);
        Assert.Equal(new Vector3i(2, 2, 3), new Vector3i(4, 6, 9) / new Vector3i(2, 3, 3));
        Assert.Equal(new Vector3i(12, 8, 18), three * new Vector3i(-2, 2, 2));
        Assert.Equal(new Vector3i(-12, 8, 18), 2 * three);
        Assert.Equal(new Vector3i(6, 4, 9), Vector3i.Abs(three));
        Assert.Equal(new Vector2f(-6, 4), three.ToVector2());
        Assert.Equal(new Vector2i(-6, 4), three.ToIntVector2());
        Assert.Equal(new Vector3f(-6, 4, 9), three.ToVector3());
        three.MutableData()[1] = 5;
        Assert.Equal(5, three.Data()[1]);
    }

    [Fact]
    public void Real_vectors_cover_projection_rounding_and_conversion_helpers()
    {
        var two = new Vector2f(-1.25f, 2.75f);
        Assert.Equal((-1.25f, 2.75f), two.Tie());
        Assert.Equal(1.25f * 1.0f + 2.75f * 2.0f, two.AbsDotProduct(new Vector2f(1, 2)));
        Assert.Equal(2.75f * 1.0f - (-1.25f * 2.0f), two.ScalarCrossProduct(new Vector2f(1, 2)));
        _ = two.ProjectOntoAxis(Vector2f.Right);
        _ = two.DistanceToPoint(Vector2f.Zero);
        _ = two.Angle(Vector2f.Right);
        _ = two.SignedAngle(Vector2f.Right);
        Assert.Equal(new Vector2f(1.25f, 2.75f), two.Abs);
        Assert.Equal(new Vector2f(2.75f, 1.25f), two.GetOrthogonalClockwise().Abs);
        Assert.Equal(new Vector4f(-1.25f, 2.75f, 3, 4), two.ToVector4(3, 4));
        _ = Vector2f.Min(two, Vector2f.One);
        _ = Vector2f.Max(two, Vector2f.One);
        _ = Vector2f.Floor(two);
        _ = Vector2f.Round(two);
        _ = Vector2f.Ceil(two);
        _ = Vector2f.RoundToInt(two);
        _ = Vector2f.StableRandom(2.0f);

        var three = new Vector3f(-1.25f, 2.75f, 3.5f);
        Assert.Equal((-1.25f, 2.75f, 3.5f), three.Tie());
        Assert.Equal(new Vector3f(-1.25f, 4, 2.75f), Vector3f.FromXZ(new Vector2f(-1.25f, 2.75f), 4));
        _ = three.AbsDotProduct(Vector3f.One);
        _ = three.ProjectOntoPlane(Vector3f.Zero, Vector3f.Up);
        Assert.Equal(0.5f, new Vector3f(0, 0, 0.5f).ProjectOntoLineScalar(Vector3f.Zero, Vector3f.Forward));
        Assert.Equal(0.0f, new Vector3f(-1, 0, 0).ProjectOntoLineScalar(Vector3f.Zero, Vector3f.Forward, true));
        Assert.Equal(1.0f, new Vector3f(0, 0, 2).ProjectOntoLineScalar(Vector3f.Zero, Vector3f.Forward, true));
        _ = three.ProjectOntoLine(Vector3f.Left, Vector3f.Right);
        _ = three.DistanceToPoint(Vector3f.Zero);
        _ = three.DistanceToPlane(Vector3f.Zero, Vector3f.Up);
        _ = three.Lerp(Vector3f.One, 0.25f);
        _ = Vector3f.Floor(three);
        _ = Vector3f.Round(three);
        _ = Vector3f.Ceil(three);
        _ = Vector3f.CeilToInt(three);
        _ = Vector3f.StableRandom(three);
    }

    [Fact]
    public void Geometry_value_helpers_cover_remaining_constructors_and_delegates()
    {
        var box = new BoundingBox(-1.0f, 2.0f);
        Assert.Equal(box, new BoundingBox(box));
        _ = new BoundingBox(new Rect(0, 0, 2, 3));

        var sphere = new Sphere(Vector3f.One, 2.0f);
        Assert.Equal(sphere, new Sphere(sphere));
        Assert.Equal(sphere, ((ICloneable)sphere).Clone());
        _ = new Sphere(new[] { Vector3f.Zero, Vector3f.One });
        _ = new Sphere(box);
        var frustum = new Frustum();
        frustum.Define(Matrix4.Identity);
        _ = new Sphere(frustum);
        var polyhedron = new Polyhedron(frustum);
        _ = new Sphere(polyhedron);
        sphere.Define(frustum);
        sphere.Define(polyhedron);
        sphere.Merge(frustum);
        Assert.NotEqual(0, sphere.GetHashCode());

        _ = SphericalHarmonics9.Zero;
        _ = SphericalHarmonicsColor9.Zero;
        _ = new SphericalHarmonicsColor9(Color.Black);
        _ = new Matrix3x4(Matrix3.Identity);
        _ = Matrix3x4.FromRotation(Quaternion.IDENTITY);
        _ = Transform.Lerp(new Transform(), new Transform(), 0.5f);
        _ = DoubleTransform.Lerp(new DoubleTransform(), new DoubleTransform(), 0.5f);
    }

    [Fact]
    public void Quaternion_slerp_covers_the_browser_fast_approximation_paths()
    {
        var overload = typeof(Quaternion).GetMethod(
            "Slerp",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Quaternion), typeof(float), typeof(bool)],
            modifiers: null)!;
        var identity = Quaternion.IDENTITY;
        var rotated = new Quaternion(45.0f, Vector3f.Up);

        foreach (var rhs in new[] { rotated, -rotated, identity })
        {
            var result = (Quaternion)overload.Invoke(identity, [rhs, 0.25f, true])!;
            Assert.InRange(result.LengthSquared, 0.99f, 1.01f);
        }
    }
}
