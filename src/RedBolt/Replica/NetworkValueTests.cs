// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.Replica;

public sealed class NetworkValueTests
{
    [Fact]
    public void Network_value_is_updated_and_sampled()
    {
        var value = new NetworkValue<float>();
        value.Resize(5);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Null(value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        value.Set((NetworkFrame)2, 1000.0f);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Equal(1000.0f, value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        Assert.Equal(1000.0f, value.GetClosestRaw((NetworkFrame)1));
        Assert.Equal(1000.0f, value.GetClosestRaw((NetworkFrame)2));
        Assert.Equal(1000.0f, value.GetClosestRaw((NetworkFrame)5));

        Assert.Equal(1000.0f, value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)));
        Assert.Equal(1000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)));
        Assert.Equal(1000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)));

        value.Set((NetworkFrame)2, 2000.0f);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Equal(2000.0f, value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)1));
        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)2));
        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)5));

        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)));
        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)));
        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)));

        value.Set((NetworkFrame)4, 4000.0f);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Equal(2000.0f, value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Equal(4000.0f, value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)1));
        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)2));
        Assert.Equal(2000.0f, value.GetClosestRaw((NetworkFrame)3));
        Assert.Equal(4000.0f, value.GetClosestRaw((NetworkFrame)4));
        Assert.Equal(4000.0f, value.GetClosestRaw((NetworkFrame)5));

        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)));
        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)));
        Assert.Equal(2500.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)));
        Assert.Equal(3000.0f, value.SampleValid(new NetworkTime((NetworkFrame)3, 0.0f)));
        Assert.Equal(3500.0f, value.SampleValid(new NetworkTime((NetworkFrame)3, 0.5f)));
        Assert.Equal(4000.0f, value.SampleValid(new NetworkTime((NetworkFrame)4, 0.0f)));
        Assert.Equal(4000.0f, value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)));

        value.Set((NetworkFrame)3, 3000.0f);
        value.Set((NetworkFrame)5, 5000.0f);
        value.Set((NetworkFrame)6, 6000.0f);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Equal(2000.0f, value.GetRaw((NetworkFrame)2));
        Assert.Equal(3000.0f, value.GetRaw((NetworkFrame)3));
        Assert.Equal(4000.0f, value.GetRaw((NetworkFrame)4));
        Assert.Equal(5000.0f, value.GetRaw((NetworkFrame)5));
        Assert.Equal(6000.0f, value.GetRaw((NetworkFrame)6));

        Assert.Equal(5000.0f, value.GetClosestRaw((NetworkFrame)5));
        Assert.Equal(6000.0f, value.GetClosestRaw((NetworkFrame)6));
        Assert.Equal(6000.0f, value.GetClosestRaw((NetworkFrame)7));

        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)));
        Assert.Equal(2000.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)));
        Assert.Equal(2500.0f, value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)));
        Assert.Equal(3000.0f, value.SampleValid(new NetworkTime((NetworkFrame)3, 0.0f)));
        Assert.Equal(3500.0f, value.SampleValid(new NetworkTime((NetworkFrame)3, 0.5f)));
        Assert.Equal(4000.0f, value.SampleValid(new NetworkTime((NetworkFrame)4, 0.0f)));
        Assert.Equal(4500.0f, value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)));
        Assert.Equal(5750.0f, value.SampleValid(new NetworkTime((NetworkFrame)5, 0.75f)));
        Assert.Equal(6000.0f, value.SampleValid(new NetworkTime((NetworkFrame)6, 0.0f)));
        Assert.Equal(6000.0f, value.SampleValid(new NetworkTime((NetworkFrame)6, 0.5f)));

        value.Set((NetworkFrame)9, 9000.0f);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Null(value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Equal(5000.0f, value.GetRaw((NetworkFrame)5));
        Assert.Equal(6000.0f, value.GetRaw((NetworkFrame)6));
        Assert.Null(value.GetRaw((NetworkFrame)7));
        Assert.Null(value.GetRaw((NetworkFrame)8));
        Assert.Equal(9000.0f, value.GetRaw((NetworkFrame)9));

        Assert.Equal(5000.0f, value.GetClosestRaw((NetworkFrame)4));
        Assert.Equal(5000.0f, value.GetClosestRaw((NetworkFrame)5));
        Assert.Equal(6000.0f, value.GetClosestRaw((NetworkFrame)6));
        Assert.Equal(6000.0f, value.GetClosestRaw((NetworkFrame)7));
        Assert.Equal(6000.0f, value.GetClosestRaw((NetworkFrame)8));
        Assert.Equal(9000.0f, value.GetClosestRaw((NetworkFrame)9));
        Assert.Equal(9000.0f, value.GetClosestRaw((NetworkFrame)10));

        Assert.Equal(5000.0f, value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)));
        Assert.Equal(5000.0f, value.SampleValid(new NetworkTime((NetworkFrame)5, 0.0f)));
        Assert.Equal(5500.0f, value.SampleValid(new NetworkTime((NetworkFrame)5, 0.5f)));
        Assert.Equal(6000.0f, value.SampleValid(new NetworkTime((NetworkFrame)6, 0.0f)));
        Assert.Equal(6500.0f, value.SampleValid(new NetworkTime((NetworkFrame)6, 0.5f)));
    }

    [Fact]
    public void Network_value_sampler_is_smoothly_sampled()
    {
        const uint maxExtrapolation = 10;
        const float smoothing = 5.0f;
        const float snapThreshold = 10000.0f;

        var traits = new ValueWithDerivativeNetworkValueTraits<float>();
        var value = new NetworkValue<ValueWithDerivative<float>>(traits);
        value.Resize(11);
        var sampler = new NetworkValueSampler<ValueWithDerivative<float>, float>(traits);
        sampler.Setup(maxExtrapolation, smoothing, snapThreshold);

        value.Set((NetworkFrame)5, new ValueWithDerivative<float>(5000.0f, 1000.0f));
        value.Set((NetworkFrame)7, new ValueWithDerivative<float>(7000.0f, 1000.0f));

        Assert.Equal(5000.0f, Sample(sampler, value, 4.0f, 0.5f));
        Assert.Equal(5000.0f, Sample(sampler, value, 4.5f, 0.5f));
        Assert.Equal(5000.0f, Sample(sampler, value, 5.0f, 0.5f));
        Assert.Equal(5500.0f, Sample(sampler, value, 5.5f, 0.5f));

        value.Set((NetworkFrame)6, new ValueWithDerivative<float>(6000.0f, 1000.0f));

        Assert.Equal(5500.0f, Sample(sampler, value, 5.5f, 0.0f));
        Assert.Equal(6000.0f, Sample(sampler, value, 6.0f, 0.5f));
        Assert.Equal(6500.0f, Sample(sampler, value, 6.5f, 0.5f));

        Assert.Equal(7000.0f, Sample(sampler, value, 7.0f, 0.5f));
        Assert.Equal(7500.0f, Sample(sampler, value, 7.5f, 0.5f));
        Assert.Equal(8000.0f, Sample(sampler, value, 8.0f, 0.5f));
        Assert.Equal(8500.0f, Sample(sampler, value, 8.5f, 0.5f));

        value.Set((NetworkFrame)8, new ValueWithDerivative<float>(8000.0f, 1000.0f));

        Assert.Equal(8500.0f, Sample(sampler, value, 8.5f, 0.0f));
        Assert.Equal(9000.0f, Sample(sampler, value, 9.0f, 0.5f));

        Assert.Equal(11000.0f, Sample(sampler, value, 11.0f, 2.0f));

        value.Set((NetworkFrame)10, new ValueWithDerivative<float>(10000.0f, 2000.0f));

        Assert.Equal(11000.0f, Sample(sampler, value, 11.0f, 0.0f));
        AssertClose(13000.0f, Sample(sampler, value, 11.5f, 0.5f), 200.0f);
        AssertClose(14000.0f, Sample(sampler, value, 12.0f, 0.5f), 40.0f);
        AssertClose(15000.0f, Sample(sampler, value, 12.5f, 0.5f), 6.0f);
        AssertClose(16000.0f, Sample(sampler, value, 13.0f, 0.5f), 1.0f);

        value.Set((NetworkFrame)15, new ValueWithDerivative<float>(15000.0f, 1000.0f));

        AssertClose(16000.0f, Sample(sampler, value, 13.0f, 0.0f), 1.0f);
        AssertClose(13500.0f, Sample(sampler, value, 13.5f, 0.5f), 600.0f);
        AssertClose(14000.0f, Sample(sampler, value, 14.0f, 0.5f), 100.0f);
        AssertClose(14500.0f, Sample(sampler, value, 14.5f, 0.5f), 20.0f);
        AssertClose(15000.0f, Sample(sampler, value, 15.0f, 0.5f), 3.0f);

        value.Set((NetworkFrame)25, new ValueWithDerivative<float>(25000.0f, 1000.0f));
        AssertClose(15000.0f, Sample(sampler, value, 15.5f, 0.5f), 0.6f);
        AssertClose(15000.0f, Sample(sampler, value, 16.0f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 16.5f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 17.0f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 17.5f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 18.0f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 18.5f, 0.5f), 0.0f);
        AssertClose(15000.0f, Sample(sampler, value, 19.0f, 0.5f), 0.0f);
        AssertClose(25000.0f, Sample(sampler, value, 20.5f, 1.5f), 0.0f);
        AssertClose(25000.0f, Sample(sampler, value, 25.0f, 4.5f), 0.0f);
        AssertClose(25500.0f, Sample(sampler, value, 25.5f, 0.5f), 0.0f);
    }

    [Fact]
    public void Network_value_sampler_for_quaternion_is_smoothly_sampled()
    {
        const uint maxExtrapolation = 0;
        const float smoothing = 5.0f;

        var value = new NetworkValue<Quaternion>();
        value.Resize(10);
        var sampler = new NetworkValueSampler<Quaternion>();
        sampler.Setup(maxExtrapolation, smoothing, MathDefs.LargeValue);

        value.Set((NetworkFrame)5, new Quaternion(0.0f));
        value.Set((NetworkFrame)6, new Quaternion(90.0f));
        value.Set((NetworkFrame)7, new Quaternion(180.0f));

        AssertQuaternion(sampler, value, 5.0f, 0.0f, new Quaternion(0.0f));
        AssertQuaternion(sampler, value, 5.5f, 0.5f, new Quaternion(45.0f));
        AssertQuaternion(sampler, value, 6.0f, 0.5f, new Quaternion(90.0f));
        AssertQuaternion(sampler, value, 6.5f, 0.5f, new Quaternion(135.0f));
        AssertQuaternion(sampler, value, 7.0f, 0.5f, new Quaternion(180.0f));
        AssertQuaternion(sampler, value, 7.5f, 0.5f, new Quaternion(180.0f));

        value.Set((NetworkFrame)8, new Quaternion(270.0f));
        value.Set((NetworkFrame)9, new Quaternion(360.0f));

        AssertQuaternion(sampler, value, 7.5f, 0.0f, new Quaternion(180.0f));
        AssertQuaternion(sampler, value, 8.0f, 0.5f, new Quaternion(270.0f), 0.003f);
        AssertQuaternion(sampler, value, 8.5f, 0.5f, new Quaternion(315.0f), 0.0001f);
        AssertQuaternion(sampler, value, 9.0f, 0.5f, new Quaternion(360.0f), 0.00001f);
    }

    [Fact]
    public void Network_value_sampler_for_quaternion_is_extrapolated()
    {
        const uint maxExtrapolation = 10;
        const float smoothing = 5.0f;

        var traits = new QuaternionValueWithDerivativeNetworkValueTraits();
        var value = new NetworkValue<QuaternionValueWithDerivative>(traits);
        value.Resize(10);
        var sampler = new NetworkValueSampler<QuaternionValueWithDerivative, Quaternion>(traits);
        sampler.Setup(maxExtrapolation, smoothing, MathDefs.LargeValue);

        var velocity = new Quaternion(90.0f).AngularVelocity;
        value.Set((NetworkFrame)5, new QuaternionValueWithDerivative(new Quaternion(90.0f), velocity));

        AssertQuaternion(sampler, value, 4.0f, 0.0f, new Quaternion(90.0f));
        AssertQuaternion(sampler, value, 4.5f, 0.5f, new Quaternion(90.0f));
        AssertQuaternion(sampler, value, 5.0f, 0.5f, new Quaternion(90.0f));
        AssertQuaternion(sampler, value, 5.5f, 0.5f, new Quaternion(135.0f));
        AssertQuaternion(sampler, value, 6.0f, 0.5f, new Quaternion(180.0f));
    }

    [Fact]
    public void Network_value_vector_is_updated_and_sampled()
    {
        const uint size = 2;

        var value = new NetworkValueVector<float>();
        value.Resize(size, 5);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Null(value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        value.Set((NetworkFrame)2, [1000.0f, 10000.0f]);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        AssertMemory(value.GetRaw((NetworkFrame)2), 1000.0f, 10000.0f);
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        AssertMemory(value.GetClosestRaw((NetworkFrame)1), 1000.0f, 10000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)2), 1000.0f, 10000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)5), 1000.0f, 10000.0f);

        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)), 1000.0f, 10000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)), 1000.0f, 10000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)), 1000.0f, 10000.0f);

        value.Set((NetworkFrame)2, [2000.0f, 20000.0f]);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        AssertMemory(value.GetRaw((NetworkFrame)2), 2000.0f, 20000.0f);
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        Assert.Null(value.GetRaw((NetworkFrame)5));

        AssertMemory(value.GetClosestRaw((NetworkFrame)1), 2000.0f, 20000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)2), 2000.0f, 20000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)5), 2000.0f, 20000.0f);

        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)), 2000.0f, 20000.0f);

        value.Set((NetworkFrame)4, [4000.0f, 40000.0f]);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        AssertMemory(value.GetRaw((NetworkFrame)2), 2000.0f, 20000.0f);
        Assert.Null(value.GetRaw((NetworkFrame)3));
        AssertMemory(value.GetRaw((NetworkFrame)4), 4000.0f, 40000.0f);
        Assert.Null(value.GetRaw((NetworkFrame)5));

        AssertMemory(value.GetClosestRaw((NetworkFrame)1), 2000.0f, 20000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)2), 2000.0f, 20000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)3), 2000.0f, 20000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)4), 4000.0f, 40000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)5), 4000.0f, 40000.0f);

        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)), 2500.0f, 25000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)3, 0.0f)), 3000.0f, 30000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)3, 0.5f)), 3500.0f, 35000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)4, 0.0f)), 4000.0f, 40000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)), 4000.0f, 40000.0f);

        value.Set((NetworkFrame)3, [3000.0f, 30000.0f]);
        value.Set((NetworkFrame)5, [5000.0f, 50000.0f]);
        value.Set((NetworkFrame)6, [6000.0f, 60000.0f]);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        AssertMemory(value.GetRaw((NetworkFrame)2), 2000.0f, 20000.0f);
        AssertMemory(value.GetRaw((NetworkFrame)3), 3000.0f, 30000.0f);
        AssertMemory(value.GetRaw((NetworkFrame)4), 4000.0f, 40000.0f);
        AssertMemory(value.GetRaw((NetworkFrame)5), 5000.0f, 50000.0f);
        AssertMemory(value.GetRaw((NetworkFrame)6), 6000.0f, 60000.0f);

        AssertMemory(value.GetClosestRaw((NetworkFrame)5), 5000.0f, 50000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)6), 6000.0f, 60000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)7), 6000.0f, 60000.0f);

        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)1, 0.5f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.0f)), 2000.0f, 20000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)2, 0.5f)), 2500.0f, 25000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)3, 0.0f)), 3000.0f, 30000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)3, 0.5f)), 3500.0f, 35000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)4, 0.0f)), 4000.0f, 40000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)), 4500.0f, 45000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)5, 0.75f)), 5750.0f, 57500.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)6, 0.0f)), 6000.0f, 60000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)6, 0.5f)), 6000.0f, 60000.0f);

        value.Set((NetworkFrame)9, [9000.0f, 90000.0f]);

        Assert.Null(value.GetRaw((NetworkFrame)1));
        Assert.Null(value.GetRaw((NetworkFrame)2));
        Assert.Null(value.GetRaw((NetworkFrame)3));
        Assert.Null(value.GetRaw((NetworkFrame)4));
        AssertMemory(value.GetRaw((NetworkFrame)5), 5000.0f, 50000.0f);
        AssertMemory(value.GetRaw((NetworkFrame)6), 6000.0f, 60000.0f);
        Assert.Null(value.GetRaw((NetworkFrame)7));
        Assert.Null(value.GetRaw((NetworkFrame)8));
        AssertMemory(value.GetRaw((NetworkFrame)9), 9000.0f, 90000.0f);

        AssertMemory(value.GetClosestRaw((NetworkFrame)4), 5000.0f, 50000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)5), 5000.0f, 50000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)6), 6000.0f, 60000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)7), 6000.0f, 60000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)8), 6000.0f, 60000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)9), 9000.0f, 90000.0f);
        AssertMemory(value.GetClosestRaw((NetworkFrame)10), 9000.0f, 90000.0f);

        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)4, 0.5f)), 5000.0f, 50000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)5, 0.0f)), 5000.0f, 50000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)5, 0.5f)), 5500.0f, 55000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)6, 0.0f)), 6000.0f, 60000.0f);
        AssertSpan(value.SampleValid(new NetworkTime((NetworkFrame)6, 0.5f)), 6500.0f, 65000.0f);
    }

    private static float Sample(NetworkValueSampler<ValueWithDerivative<float>, float> sampler,
        NetworkValue<ValueWithDerivative<float>> value, float time, float timeStep) =>
        sampler.UpdateAndSample(value, NetworkTime.FromDouble(time), timeStep).GetValueOrDefault();

    private static void AssertClose(float expected, float actual, float margin)
    {
        const float catchApproxEpsilon = 1.1920929e-7f * 100.0f;
        var tolerance = System.Math.Max(margin, catchApproxEpsilon * (System.Math.Abs(expected) + 1.0f));
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private static void AssertQuaternion(NetworkValueSampler<Quaternion> sampler,
        NetworkValue<Quaternion> value, float time, float timeStep, Quaternion expected,
        float epsilon = MathDefs.Epsilon)
    {
        var actual = sampler.UpdateAndSample(value, NetworkTime.FromDouble(time), timeStep);
        Assert.True(actual.HasValue && actual.Value.Equivalent(expected, epsilon));
    }

    private static void AssertQuaternion(
        NetworkValueSampler<QuaternionValueWithDerivative, Quaternion> sampler,
        NetworkValue<QuaternionValueWithDerivative> value, float time, float timeStep,
        Quaternion expected, float epsilon = MathDefs.Epsilon)
    {
        var actual = sampler.UpdateAndSample(value, NetworkTime.FromDouble(time), timeStep);
        Assert.True(actual.HasValue && actual.Value.Equivalent(expected, epsilon));
    }

    private static void AssertMemory(ReadOnlyMemory<float>? actual, params float[] expected)
    {
        Assert.True(actual.HasValue);
        Assert.Equal(expected, actual.Value.ToArray());
    }

    private static void AssertMemory(ReadOnlyMemory<float> actual, params float[] expected) =>
        Assert.Equal(expected, actual.ToArray());

    private static void AssertSpan(InterpolatedValueSpan<float> actual, params float[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; ++i)
            Assert.Equal(expected[i], actual[i]);
    }
}
