// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltMathTypeCompatibilityTests
{
    private const ulong FnvOffsetBasis = 0xcbf29ce484222325UL;
    private const ulong HighBitSeed = 0x8000000000000000UL;

    private delegate ulong HashBytesDelegate(IntPtr inData, uint inSize, ulong inSeed);
    private delegate ulong HashStringDelegate(ReadOnlySpan<byte> inString, ulong inSeed);

    public JoltMathTypeCompatibilityTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void RedBolt_types_match_Jolt_projected_abi_layout()
    {
        Assert.Equal(12, Unsafe.SizeOf<Vector3f>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.X)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.Y)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.Z)));

        Assert.Equal(12, Unsafe.SizeOf<Vector3fAbi>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.X)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.Y)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.Z)));

        Assert.Equal(16, Unsafe.SizeOf<Quaternion>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.W)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.X)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.Y)));
        Assert.Equal((IntPtr)12, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.Z)));

        Assert.Equal(24, Unsafe.SizeOf<Ray>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Ray>(nameof(Ray.Origin)));
        Assert.Equal((IntPtr)12, Marshal.OffsetOf<Ray>(nameof(Ray.Direction)));

        Assert.Equal(64, Unsafe.SizeOf<Matrix4>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Matrix4>(nameof(Matrix4.M00)));
        Assert.Equal((IntPtr)12, Marshal.OffsetOf<Matrix4>(nameof(Matrix4.M03)));
        Assert.Equal((IntPtr)48, Marshal.OffsetOf<Matrix4>(nameof(Matrix4.M30)));
        Assert.Equal((IntPtr)60, Marshal.OffsetOf<Matrix4>(nameof(Matrix4.M33)));

        Assert.Equal(12, Unsafe.SizeOf<Float3>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Float3>(nameof(Float3.X)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Float3>(nameof(Float3.Y)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Float3>(nameof(Float3.Z)));
    }

    [Fact]
    public void RedBolt_math_values_round_trip_through_Jolt()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();

        var vector = new Vector3f(1.25f, -2.5f, 3.75f);
        world.World.SetGravity(vector);
        Assert.Equal(vector, world.World.GetGravity());

        var rotation = new Quaternion(0.5f, -0.25f, 0.125f, 0.75f).Normalized;
        using var settings = new BodyCreationSettings
        {
            MPosition = vector,
            MRotation = rotation
        };
        Assert.Equal(vector, settings.MPosition);
        Assert.Equal(rotation, settings.MRotation);

        var floorNode = scene.CreateChild("Floor");
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(4.0f, 1.0f, 4.0f));
        floorNode.CreateComponent<RigidBody>();

        var ray = new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down)
        {
            Direction = new Vector3f(0.0f, -4.0f, 0.0f)
        };
        var hit = new JoltRayCastResult { BodyId = uint.MaxValue, Fraction = 1.0f };
        Assert.True(world.NarrowPhaseQuery.CastRay(in ray, ref hit));
        Assert.InRange(hit.Fraction, 0.35f, 0.4f);
    }

    [Fact]
    public void Matrix4_round_trips_through_Jolt_body_world_transform()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();

        // A non-uniform rotation (not a multiple of 90 degrees around a coordinate axis) so a
        // row/column transposition or a wrong SIMD-column read would show up as a mismatched
        // off-diagonal element rather than accidentally cancelling out.
        var rotation = new Quaternion(37.0f, new Vector3f(1.0f, 2.0f, 3.0f).Normalized);
        var position = new Vector3f(1.5f, -2.25f, 3.75f);

        using var shape = new BoxShape(new Vector3f(0.5f, 0.5f, 0.5f));
        using var settings = new BodyCreationSettings(
            shape, position, rotation, EMotionType.Static,
            ObjectLayerPairFilterMask.SGetObjectLayer(2, uint.MaxValue & 0xffff));
        var bodyId = world.BodyInterface.CreateAndAddBody(settings, EActivation.DontActivate);
        try
        {
            var transform = world.BodyInterface.GetWorldTransform(in bodyId);
            var expected = new Matrix4(rotation, position);

            Assert.True(transform.ApproximatelyEquivalent(expected, 1.0e-4f));
            Assert.True(transform.Translation.ApproximatelyEquivalent(position, 1.0e-4f));
            Assert.True(transform.Rotation.Equivalent(rotation, 1.0e-4f));

            // Sanity check that the fixture is actually asymmetric, so the equality checks above
            // could not pass by accident if rows and columns were swapped.
            Assert.True(MathF.Abs(expected.M01 - expected.M10) > 0.01f);
            Assert.True(MathF.Abs(expected.M02 - expected.M20) > 0.01f);
            Assert.True(MathF.Abs(expected.M12 - expected.M21) > 0.01f);
        }
        finally
        {
            world.BodyInterface.RemoveBody(in bodyId);
            world.BodyInterface.DestroyBody(in bodyId);
        }
    }

    [Fact]
    public void Float3_array_round_trips_through_Jolt_mesh_shape_bounds()
    {
        using var scene = new Scene();
        scene.CreateComponent<PhysicsWorld>();

        // Distinct, asymmetric coordinates so a wrong element order or wrong array stride
        // (e.g. reading Float3's packed 12 bytes with Vec3's 16-byte-aligned stride) would
        // produce bounds that don't match any of the source vertices.
        var vertices = new[]
        {
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Vector3f(4.0f, -5.0f, 6.0f),
            new Vector3f(-7.0f, 8.0f, -9.0f)
        };

        using var nativeVertices = new global::JPH.Array<Float3>();
        foreach (var vertex in vertices)
            nativeVertices.Add(vertex);

        using var triangles = new global::JPH.Array<IndexedTriangle>();
        using (var triangle = new IndexedTriangle(0, 1, 2, 0))
            triangles.Add(triangle);

        using var meshSettings = new MeshShapeSettings(nativeVertices, triangles);
        using var result = meshSettings.Create();
        Assert.True(result.IsValid());
        using var shape = result.Get();

        using var bounds = shape.GetLocalBounds();
        Assert.True(bounds.MMin.ApproximatelyEquivalent(new Vector3f(-7.0f, -5.0f, -9.0f), 1.0e-4f));
        Assert.True(bounds.MMax.ApproximatelyEquivalent(new Vector3f(4.0f, 8.0f, 6.0f), 1.0e-4f));
    }

    [Fact]
    public void Jolt_fixed_width_hash_aliases_preserve_64_bit_seeds()
    {
        ReadOnlySpan<byte> emptyString = "\0"u8;
        var input = new byte[1];
        var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        try
        {
            var inputAddress = inputHandle.AddrOfPinnedObject();
            ulong hashBytesDefault = Jolt.HashBytes(inputAddress, 0);
            ulong hashStringDefault = Jolt.HashString(emptyString);
            ulong processorTickCount = Jolt.GetProcessorTickCount();
            HashBytesDelegate hashBytes = Jolt.HashBytes;
            HashStringDelegate hashString = Jolt.HashString;

            Assert.Equal(FnvOffsetBasis, hashBytesDefault);
            Assert.Equal(FnvOffsetBasis, hashStringDefault);
            Assert.Equal(HighBitSeed, hashBytes(inputAddress, 0, HighBitSeed));
            Assert.Equal(HighBitSeed, hashString(emptyString, HighBitSeed));
            Assert.Equal(ulong.MaxValue, hashBytes(inputAddress, 0, ulong.MaxValue));
            Assert.Equal(ulong.MaxValue, hashString(emptyString, ulong.MaxValue));
            Assert.IsType<ulong>(processorTickCount);
        }
        finally
        {
            inputHandle.Free();
        }
    }

    [Fact]
    public void BVec16_fixed_width_array_uses_ulong_elements()
    {
        var property = typeof(BVec16).GetProperty(nameof(BVec16.MU64));

        Assert.NotNull(property);
        Assert.Equal(typeof(global::SharpCpp.NativeArray<ulong>), property.PropertyType);
    }

    [Fact]
    public void Jolt_array_is_a_distinct_native_sequence_type()
    {
        using var scene = new Scene();
        scene.CreateComponent<PhysicsWorld>();
        using var values = new global::JPH.Array<uint>();
        values.Add(10);
        values.Add(20);

        Assert.Equal(typeof(global::JPH.Array<uint>), values.GetType());
        Assert.NotEqual(typeof(global::Std.Vector<uint>), values.GetType());
        Assert.IsAssignableFrom<global::SharpCpp.Support.VectorBase<uint>>(values);
        Assert.Equal(new uint[] { 10, 20 }, values.ToArray());
    }
}
