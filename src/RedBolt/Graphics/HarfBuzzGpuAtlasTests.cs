// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;
using RedBolt.Graphics;
using RedBolt.Math;

public sealed class HarfBuzzGpuAtlasTests
{
    private const int Width = 512;
    private const int InitialHeight = 8;
    private const int MaximumHeight = 512;
    private const int TexelSize = 8;
    private const int MaximumTexels = Width * MaximumHeight;

    [Fact]
    public void Atlas_grows_without_moving_offsets_and_fails_at_the_hard_limit()
    {
        using var atlas = new AtlasHandle();
        Assert.True(atlas.Data.AsSpan(0, 8).SequenceEqual("RBHBGPU1"u8));
        Assert.Equal(1, atlas.UsedTexels);
        Assert.Equal(8, atlas.Height);

        var initialPayload = new byte[(Width * InitialHeight - 1) * TexelSize];
        initialPayload[0] = 0x5a;
        initialPayload[^1] = 0xa5;
        Assert.True(atlas.TryAppend(initialPayload, out var firstOffset));
        Assert.Equal(1, firstOffset);
        Assert.Equal(8, atlas.Height);

        Assert.True(atlas.TryAppend(new byte[TexelSize], out var grownOffset));
        Assert.Equal(Width * InitialHeight, grownOffset);
        Assert.Equal(16, atlas.Height);
        Assert.Equal(1, firstOffset);
        Assert.Equal(0x5a, atlas.Data[firstOffset * TexelSize]);
        Assert.Equal(0xa5, atlas.Data[(grownOffset * TexelSize) - 1]);

        var remainingTexels = MaximumTexels - atlas.UsedTexels;
        Assert.True(atlas.TryAppend(new byte[remainingTexels * TexelSize], out _));
        Assert.Equal(MaximumHeight, atlas.Height);
        Assert.Equal(MaximumTexels, atlas.UsedTexels);
        Assert.False(atlas.TryAppend(new byte[TexelSize], out var overflowOffset));
        Assert.Equal(0, overflowOffset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(262143)]
    public void Packed_uv_round_trips_offset_and_padded_local_coordinates(int offset)
    {
        var packed = Uv.Pack(offset, -0.25f, 1.25f);
        Assert.Equal(offset, Uv.UnpackOffset(packed));
        var local = Uv.UnpackLocal(packed);
        Assert.InRange(MathF.Abs(local.X + 0.25f), 0.0f, 0.00001f);
        Assert.InRange(MathF.Abs(local.Y - 1.25f), 0.0f, 0.00001f);
        Assert.InRange(packed.X - MathF.Floor(packed.X), 0.125f, 0.875f);
        Assert.InRange(packed.Y - MathF.Floor(packed.Y), 0.125f, 0.875f);
    }

    [Theory]
    [InlineData(0.125f)]
    [InlineData(0.5f)]
    [InlineData(0.875f)]
    public void Packed_uv_preserves_affine_local_coordinates_when_a_quad_is_clipped(float amount)
    {
        const int offset = 512;
        var start = Uv.Pack(offset, -0.25f, -0.25f);
        var end = Uv.Pack(offset, 1.25f, 1.25f);
        var clipped = start + (end - start) * amount;

        Assert.Equal(offset, Uv.UnpackOffset(clipped));
        var expectedLocal = -0.25f + 1.5f * amount;
        var local = Uv.UnpackLocal(clipped);
        Assert.InRange(MathF.Abs(local.X - expectedLocal), 0.0f, 0.00001f);
        Assert.InRange(MathF.Abs(local.Y - expectedLocal), 0.0f, 0.00001f);
    }

    [Fact]
    public void Glyph_padding_is_bounded_to_one_pixel_and_one_quarter_dimension()
    {
        Assert.Equal(0.25f, Uv.GetPadding(1.0f));
        Assert.Equal(1.0f, Uv.GetPadding(4.0f));
        Assert.Equal(1.0f, Uv.GetPadding(100.0f));
    }

    private static Type GetInternalType(string name) =>
        typeof(Font).Assembly.GetType($"RedBolt.Graphics.{name}", throwOnError: true)!;

    private sealed class AtlasHandle : IDisposable
    {
        private static readonly Type Type = GetInternalType("HarfBuzzGpuAtlas");
        private static readonly PropertyInfo DataProperty = Type.GetProperty(
            "DataForTests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo AppendMethod = Type.GetMethod(
            "TryAppendForTests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly object _value = Activator.CreateInstance(Type, nonPublic: true)!;

        public byte[] Data => (byte[])DataProperty.GetValue(_value)!;
        public int Height => (int)Type.GetProperty("Height")!.GetValue(_value)!;
        public int UsedTexels => (int)Type.GetProperty("UsedTexels")!.GetValue(_value)!;

        public bool TryAppend(byte[] blob, out int offset)
        {
            object?[] arguments = [blob, 0];
            var result = (bool)AppendMethod.Invoke(_value, arguments)!;
            offset = (int)arguments[1]!;
            return result;
        }

        public void Dispose() => ((IDisposable)_value).Dispose();
    }

    private static class Uv
    {
        private static readonly Type Type = GetInternalType("HarfBuzzGpuUv");

        public static Vector2f Pack(int offset, float x, float y) =>
            (Vector2f)Invoke("Pack", offset, x, y)!;

        public static int UnpackOffset(Vector2f uv) => (int)Invoke("UnpackOffset", uv)!;
        public static Vector2f UnpackLocal(Vector2f uv) => (Vector2f)Invoke("UnpackLocal", uv)!;
        public static float GetPadding(float dimension) => (float)Invoke("GetPadding", dimension)!;

        private static object? Invoke(string name, params object[] arguments) =>
            Type.GetMethod(name, BindingFlags.Static | BindingFlags.Public)!.Invoke(null, arguments);
    }
}
