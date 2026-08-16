// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Utility;

public sealed class CalculateAnimationVelocityTransformerTests
{
    [Fact]
    public void Parameters_roundtrip_clone_and_omit_defaults()
    {
        var parameters = new CalculateAnimationVelocityParams
        {
            TargetBones = ["LeftFoot", "RightFoot"],
            GroundThreshold = 0.02f,
            VelocityThreshold = 0.1f,
            SampleRate = 60.0f,
        };
        var clone = parameters.Clone();
        clone.TargetBones[0] = "Changed";
        Assert.Equal("LeftFoot", parameters.TargetBones[0]);

        using var jsonFile = new JSONFile();
        Assert.True(jsonFile.SaveObject("params", parameters));
        var json = jsonFile.ToString();
        Assert.Contains("targetBones", json, StringComparison.Ordinal);
        Assert.Contains("sampleRate", json, StringComparison.Ordinal);

        using var restoredFile = new JSONFile();
        Assert.True(restoredFile.FromString(json));
        var restored = new CalculateAnimationVelocityParams();
        Assert.True(restoredFile.LoadObject("params", restored));
        Assert.Equal(parameters.TargetBones, restored.TargetBones);
        Assert.Equal(parameters.GroundThreshold, restored.GroundThreshold);
        Assert.Equal(parameters.VelocityThreshold, restored.VelocityThreshold);
        Assert.Equal(parameters.SampleRate, restored.SampleRate);

        using var emptyFile = new JSONFile();
        Assert.True(emptyFile.SaveObject("params", new CalculateAnimationVelocityParams()));
        Assert.DoesNotContain("targetBones", emptyFile.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("groundThreshold", emptyFile.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Grounded_track_velocity_matches_looped_animation_algorithm()
    {
        var evaluateVelocity = typeof(CalculateAnimationVelocityTransformer).GetMethod(
            "EvaluateVelocity", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(evaluateVelocity);

        IReadOnlyList<Vector3f> track =
        [
            new(0.0f, 0.0f, 0.0f),
            new(-1.0f, 0.0f, 0.0f),
            new(-2.0f, 0.0f, 0.0f),
            new(-3.0f, 0.0f, 0.0f),
            new(-4.0f, 0.0f, 0.0f),
        ];
        var result = evaluateVelocity.Invoke(
            null, [track, 2.0f, new CalculateAnimationVelocityParams()]);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), Assert.IsType<Vector3f>(result));

        IReadOnlyList<Vector3f> shortTrack = track.Take(3).ToArray();
        Assert.Null(evaluateVelocity.Invoke(
            null, [shortTrack, 2.0f, new CalculateAnimationVelocityParams()]));
    }

    [Fact]
    public void Transformer_registers_and_empty_parameter_file_executes_without_tasks()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBoltCalculateAnimationVelocity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(
            Path.Combine(directory, "CalculateAnimationVelocity.json"), "{}");

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(CalculateAnimationVelocityTransformer).Assembly);
            var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountDir(directory);
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(new ResourceCache());

            using var reflected = Assert.IsType<CalculateAnimationVelocityTransformer>(
                context.CreateObject(new StringHash(nameof(CalculateAnimationVelocityTransformer))));
            using var transformer = new CalculateAnimationVelocityTransformer();
            var input = new AssetTransformerInput
            {
                InputFileName = "CalculateAnimationVelocity.json",
                ResourceName = "CalculateAnimationVelocity.json",
            };
            var output = new AssetTransformerOutput();

            Assert.True(transformer.Execute(input, output, [transformer]));
            Assert.Empty(output.ModifiedResourceNames);
            Assert.Equal("CalculateAnimationVelocity.json", transformer.GetParametersFileName());

            using var emptyAnimation = new Animation();
            Assert.Null(transformer.CalculateVelocity(new CalculateAnimationVelocityTask
            {
                Animation = emptyAnimation,
            }));
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }
}
