namespace RedBolt.Tests;

using System.Reflection;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;

public sealed class VariantCurveRemainingCoverageTests
{
    [Fact]
    public void Variant_curve_archive_round_trip_covers_tangents_and_input_cleanup()
    {
        using var source = new VariantCurve
        {
            Name = "TangentCurve",
            Interpolation = KeyFrameInterpolation.TangentSpline,
            SplineTension = 0.25f
        };
        source.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant(1.0f) });
        source.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = new Variant(3.0f) });
        source.Type = VariantType.Float;
        source.InTangents.Add(new Variant(0.5f));
        source.InTangents.Add(new Variant(1.5f));
        source.OutTangents.Add(new Variant(0.75f));
        source.OutTangents.Add(new Variant(1.75f));

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("curve"))
            source.SerializeInBlock(output);

        using var destination = new VariantCurve
        {
            Name = "old",
            Interpolation = KeyFrameInterpolation.Linear
        };
        destination.AddKeyFrame(new VariantCurvePoint { Time = -1.0f, Value = new Variant(99.0f) });
        destination.InTangents.Add(new Variant(9.0f));
        destination.OutTangents.Add(new Variant(9.0f));

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("curve"))
            destination.SerializeInBlock(input);

        Assert.Equal(source.Name, destination.Name);
        Assert.Equal(KeyFrameInterpolation.TangentSpline, destination.Interpolation);
        Assert.Equal(2, destination.KeyFrames.Count);
        Assert.Equal(2, destination.InTangents.Count);
        Assert.Equal(2, destination.OutTangents.Count);
        Assert.Equal(1.90625f, destination.Sample(0.5f).GetFloat());
    }

    [Fact]
    public void Variant_curve_archive_output_uses_default_tangents_when_lists_are_short()
    {
        using var curve = new VariantCurve
        {
            Name = "ShortTangents",
            Interpolation = KeyFrameInterpolation.TangentSpline,
            Type = VariantType.Float
        };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant(1.0f) });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = new Variant(2.0f) });
        curve.InTangents.Add(new Variant(0.0f));
        curve.OutTangents.Add(new Variant(0.0f));

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("curve"))
            curve.SerializeInBlock(output);

        Assert.Contains("keyframes", output.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Variant_curve_clone_copies_tangents_without_sharing_variants()
    {
        using var curve = new VariantCurve
        {
            Name = "Clone",
            Interpolation = KeyFrameInterpolation.TangentSpline,
            Type = VariantType.Float
        };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant(1.0f) });
        curve.InTangents.Add(new Variant(2.0f));
        curve.OutTangents.Add(new Variant(3.0f));

        using var clone = curve.Clone();

        Assert.Equal(curve, clone);
        Assert.Equal(curve.InTangents[0], clone.InTangents[0]);
        Assert.Equal(curve.OutTangents[0], clone.OutTangents[0]);
        Assert.Equal(curve.KeyFrames[0].Value, clone.KeyFrames[0].Value);
    }

    [Fact]
    public void Variant_curve_private_spline_switches_cover_supported_and_default_types()
    {
        var interpolate = typeof(VariantCurve).GetMethod(
            "InterpolateSpline", BindingFlags.Static | BindingFlags.NonPublic)!;
        var subtract = typeof(VariantCurve).GetMethod(
            "SubtractAndMultiply", BindingFlags.Static | BindingFlags.NonPublic)!;

        foreach (var (type, first, second, tangentIn, tangentOut) in CreateSplineInputs())
        {
            using var interpolated = (Variant)interpolate.Invoke(
                null, [type, first, second, tangentIn, tangentOut, 0.4f])!;
            using var difference = (Variant)subtract.Invoke(
                null, [type, first, second, 0.5f])!;

            Assert.Equal(type, interpolated.Type);
            Assert.Equal(type, difference.Type);

            first.Dispose();
            second.Dispose();
            tangentIn.Dispose();
            tangentOut.Dispose();
        }

        using var unsupportedFirst = new Variant("first");
        using var unsupportedSecond = new Variant("second");
        using var unsupportedTangent = new Variant("tangent");
        using var unsupportedInterpolated = (Variant)interpolate.Invoke(
            null, [VariantType.String, unsupportedFirst, unsupportedSecond, unsupportedTangent, unsupportedTangent, 0.4f])!;
        using var unsupportedDifference = (Variant)subtract.Invoke(
            null, [VariantType.String, unsupportedFirst, unsupportedSecond, 0.5f])!;

        Assert.Equal(unsupportedFirst, unsupportedInterpolated);
        Assert.Equal(VariantType.String, unsupportedDifference.Type);
    }

    private static IEnumerable<(VariantType Type, Variant First, Variant Second, Variant InTangent, Variant OutTangent)> CreateSplineInputs()
    {
        yield return (VariantType.Float, new Variant(1.0f), new Variant(2.0f), new Variant(0.1f), new Variant(0.2f));
        yield return (VariantType.Vector2, new Variant(new Vector2f(1, 2)), new Variant(new Vector2f(3, 4)), new Variant(Vector2f.One), new Variant(Vector2f.One));
        yield return (VariantType.Vector3, new Variant(new Vector3f(1, 2, 3)), new Variant(new Vector3f(4, 5, 6)), new Variant(Vector3f.One), new Variant(Vector3f.One));
        yield return (VariantType.Vector4, new Variant(new Vector4f(1, 2, 3, 4)), new Variant(new Vector4f(5, 6, 7, 8)), new Variant(Vector4f.One), new Variant(Vector4f.One));
        yield return (VariantType.Quaternion, new Variant(Quaternion.IDENTITY), new Variant(Quaternion.IDENTITY), new Variant(Quaternion.IDENTITY), new Variant(Quaternion.IDENTITY));
        yield return (VariantType.Color, new Variant(Color.Black), new Variant(Color.White), new Variant(Color.White), new Variant(Color.Black));
        yield return (VariantType.Double, new Variant(1.0), new Variant(2.0), new Variant(0.1), new Variant(0.2));
    }
}
