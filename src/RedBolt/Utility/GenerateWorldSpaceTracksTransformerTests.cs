namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

public sealed class GenerateWorldSpaceTracksTransformerTests
{
    [Fact]
    public void Parameters_roundtrip_clone_and_omit_defaults()
    {
        var parameters = new GenerateWorldSpaceTracksParams
        {
            FillRotations = false,
            DeltaRotation = false,
            SampleRate = 60.0f,
            Bones = new HashSet<string>(["Root", "Hand"], StringComparer.Ordinal),
            BendTargetOffsets = new Dictionary<string, Vector3f>(StringComparer.Ordinal)
            {
                ["Elbow"] = new Vector3f(1.0f, 2.0f, 3.0f),
            },
        };
        var clone = parameters.Clone();
        clone.Bones.Remove("Root");
        clone.BendTargetOffsets["Elbow"] = Vector3f.Zero;
        Assert.Contains("Root", parameters.Bones);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), parameters.BendTargetOffsets["Elbow"]);

        using var jsonFile = new JSONFile();
        Assert.True(jsonFile.SaveObject("params", parameters));
        var json = jsonFile.ToString();
        Assert.Contains("fillRotations", json, StringComparison.Ordinal);
        Assert.Contains("bones", json, StringComparison.Ordinal);
        Assert.Contains("bendTargetOffsets", json, StringComparison.Ordinal);

        using var restoredFile = new JSONFile();
        Assert.True(restoredFile.FromString(json));
        var restored = new GenerateWorldSpaceTracksParams();
        Assert.True(restoredFile.LoadObject("params", restored));
        Assert.Equal(parameters.FillRotations, restored.FillRotations);
        Assert.Equal(parameters.DeltaRotation, restored.DeltaRotation);
        Assert.Equal(parameters.SampleRate, restored.SampleRate);
        Assert.Equal(parameters.Bones, restored.Bones);
        Assert.Equal(parameters.BendTargetOffsets, restored.BendTargetOffsets);

        using var emptyFile = new JSONFile();
        Assert.True(emptyFile.SaveObject("params", new GenerateWorldSpaceTracksParams()));
        var emptyJson = emptyFile.ToString();
        Assert.DoesNotContain("fillRotations", emptyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bones", emptyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bendTargetOffsets", emptyJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_tracks_samples_filtered_bones_and_bend_targets()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(Scene).Assembly);

            using var skeleton = new Skeleton();
            skeleton.SetNumBones(2);
            var bones = skeleton.GetModifiableBones();
            bones[0].Name = "Root";
            bones[0].NameHash = new StringHash("Root");
            bones[0].ParentIndex = 0;
            bones[0].InitialPosition = new Vector3f(0.0f, 1.0f, 0.0f);
            bones[1].Name = "Hand";
            bones[1].NameHash = new StringHash("Hand");
            bones[1].ParentIndex = 0;
            skeleton.SetRootBoneIndex(0);
            skeleton.UpdateBoneOrder();

            using var model = new Model();
            model.SetSkeleton(skeleton);

            using var sourceAnimation = new Animation();
            sourceAnimation.SetLength(1.0f);
            var sourceTrack = sourceAnimation.CreateTrack("Root");
            sourceTrack.ChannelMask = AnimationChannel.Position | AnimationChannel.Rotation;
            sourceTrack.AddKeyFrame(new AnimationKeyFrame(
                0.0f, new Vector3f(0.0f, 1.0f, 0.0f), Quaternion.IDENTITY));
            sourceTrack.AddKeyFrame(new AnimationKeyFrame(
                1.0f, new Vector3f(4.0f, 1.0f, 0.0f), Quaternion.IDENTITY));

            using var targetAnimation = new Animation();
            using var transformer = new GenerateWorldSpaceTracksTransformer();
            transformer.GenerateTracks(new GenerateWorldSpaceTracksTask
            {
                Model = model,
                SourceAnimation = sourceAnimation,
                TargetAnimation = targetAnimation,
                Params = new GenerateWorldSpaceTracksParams
                {
                    SampleRate = 2.0f,
                    Bones = new HashSet<string>(["Root"], StringComparer.Ordinal),
                    BendTargetOffsets = new Dictionary<string, Vector3f>(StringComparer.Ordinal)
                    {
                        ["Root"] = new Vector3f(0.0f, 0.0f, 1.0f),
                    },
                },
            });

            Assert.Equal(2, targetAnimation.NumTracks);
            var generatedTrack = Assert.IsType<AnimationTrack>(targetAnimation.GetTrack("Root_Target"));
            Assert.Equal(AnimationChannel.Position | AnimationChannel.Rotation, generatedTrack.ChannelMask);
            Assert.Equal(2, generatedTrack.KeyFrames.Count);
            Assert.Equal(new Vector3f(0.0f, 1.0f, 0.0f), generatedTrack.KeyFrames[0].Position);
            Assert.Equal(new Vector3f(2.0f, 1.0f, 0.0f), generatedTrack.KeyFrames[1].Position);
            Assert.True(generatedTrack.KeyFrames[0].Rotation.Equivalent(Quaternion.IDENTITY));

            var bendTrack = Assert.IsType<AnimationTrack>(targetAnimation.GetTrack("Root_BendTarget"));
            Assert.Equal(AnimationChannel.Position, bendTrack.ChannelMask);
            Assert.Equal(new Vector3f(0.0f, 1.0f, 1.0f), bendTrack.KeyFrames[0].Position);
            Assert.Null(targetAnimation.GetTrack("Hand_Target"));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Transformer_registers_and_empty_parameter_file_executes_without_tasks()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBoltGenerateWorldSpaceTracks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(
            Path.Combine(directory, "GenerateWorldSpaceTracks.json"), "{}");

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(GenerateWorldSpaceTracksTransformer).Assembly);
            var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountDir(directory);
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(new ResourceCache());

            using var reflected = Assert.IsType<GenerateWorldSpaceTracksTransformer>(
                context.CreateObject(new StringHash(nameof(GenerateWorldSpaceTracksTransformer))));
            using var transformer = new GenerateWorldSpaceTracksTransformer();
            var input = new AssetTransformerInput
            {
                InputFileName = "GenerateWorldSpaceTracks.json",
                ResourceName = "GenerateWorldSpaceTracks.json",
            };
            var output = new AssetTransformerOutput();

            Assert.True(transformer.Execute(input, output, [transformer]));
            Assert.Empty(output.ModifiedResourceNames);
            Assert.Equal("GenerateWorldSpaceTracks.json", transformer.GetParametersFileName());
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }
}
