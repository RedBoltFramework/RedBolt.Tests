// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

public sealed class ShadowCascadeEstimatorTests
{
    [Fact]
    public void Estimate_uses_fewest_cascades_that_meet_density_target()
    {
        var camera = new Scene().CreateChild("Camera").CreateComponent<Camera>();
        camera.SetNearClip(1.0f);
        camera.SetFarClip(500.0f);
        var settings = new ShadowCascadeEstimateSettings(100.0f)
        {
            MaxCascades = 3,
            TargetPixelsPerTexel = 25.0f
        };

        var cascade = ShadowCascadeEstimator.Estimate(camera, new Vector2i(1000, 600),
            new RenderPipelineSettings(), settings);

        Assert.True(cascade.Splits.X > camera.GetNearClip());
        Assert.Equal(100.0f, cascade.Splits.Y);
        Assert.Equal(0.0f, cascade.Splits.Z);

        cascade = ShadowCascadeEstimator.Estimate(camera, new Vector2i(100, 60),
            new RenderPipelineSettings(), settings);
        Assert.Equal(new Vector4f(100.0f, 0.0f, 0.0f, 0.0f), cascade.Splits);
    }

    [Fact]
    public void Estimate_generates_monotonic_pssm_splits_up_to_requested_distance()
    {
        var camera = new Scene().CreateChild("Camera").CreateComponent<Camera>();
        camera.SetNearClip(1.0f);
        camera.SetFarClip(150.0f);
        var settings = new ShadowCascadeEstimateSettings(200.0f)
        {
            MaxCascades = 3,
            TargetPixelsPerTexel = float.Epsilon,
            BlendFactor = 0.25f
        };

        var cascade = ShadowCascadeEstimator.Estimate(camera, new Vector2i(1920, 1080),
            new RenderPipelineSettings(), settings);

        Assert.True(cascade.Splits.X > camera.GetNearClip());
        Assert.True(cascade.Splits.Y > cascade.Splits.X);
        Assert.Equal(150.0f, cascade.Splits.Z);
        Assert.Equal(0.0f, cascade.Splits.W);
        Assert.Equal(0.25f, cascade.BlendFactor);
    }

    [Fact]
    public void Estimate_rejects_invalid_environment_parameters()
    {
        var camera = new Scene().CreateChild("Camera").CreateComponent<Camera>();
        var pipelineSettings = new RenderPipelineSettings();

        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowCascadeEstimator.Estimate(camera,
            new Vector2i(0, 720), pipelineSettings, new ShadowCascadeEstimateSettings(100.0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowCascadeEstimator.Estimate(camera,
            new Vector2i(1280, 720), pipelineSettings, new ShadowCascadeEstimateSettings(0.0f)));
    }
}
