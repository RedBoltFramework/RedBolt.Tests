namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IK;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

public sealed class RetargetAnimationsTransformerTests
{
    [Fact]
    public void Task_clone_preserves_resource_aliases_and_copies_collections()
    {
        using var sourceModel = new Model();
        using var sourceAnimation = new Animation();
        var task = new RetargetAnimationTask
        {
            SourceModel = sourceModel,
            SourceAnimation = sourceAnimation,
            SourceToTargetBones = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SourceRoot"] = "TargetRoot",
            },
            TargetToSourceBones = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TargetRoot"] = "SourceRoot",
            },
            IKChains = [["Hip", "Knee", "Foot"]],
        };

        var clone = task.Clone();
        clone.SourceToTargetBones["SourceRoot"] = "Changed";
        clone.IKChains[0][0] = "Changed";

        Assert.Same(sourceModel, clone.SourceModel);
        Assert.Same(sourceAnimation, clone.SourceAnimation);
        Assert.Equal("TargetRoot", task.SourceToTargetBones["SourceRoot"]);
        Assert.Equal("Hip", task.IKChains[0][0]);
    }

    [Fact]
    public void Retarget_animation_scales_root_motion_applies_initial_pose_and_copies_metadata()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(Scene).Assembly);
            context.RegisterAssembly(typeof(IKSolver).Assembly);

            using var sourceModel = CreateSingleBoneModel(
                "Root", new Vector3f(0.0f, 1.0f, 0.0f), Quaternion.IDENTITY);
            using var targetModel = CreateSingleBoneModel(
                "Root", new Vector3f(0.0f, 2.0f, 0.0f), new Quaternion(0.0f, 15.0f, 0.0f));

            using var sourceAnimation = new Animation();
            sourceAnimation.SetLength(1.0f);
            sourceAnimation.SetAnimationName("Walk");
            var sourceTrack = sourceAnimation.CreateTrack("Root");
            sourceTrack.ChannelMask =
                AnimationChannel.Position | AnimationChannel.Rotation | AnimationChannel.Scale;
            sourceTrack.AddKeyFrame(new AnimationKeyFrame(
                0.0f, new Vector3f(0.0f, 1.0f, 0.0f), Quaternion.IDENTITY, Vector3f.One));
            sourceTrack.AddKeyFrame(new AnimationKeyFrame(
                1.0f, new Vector3f(1.0f, 1.0f, 0.0f),
                new Quaternion(0.0f, 30.0f, 0.0f), new Vector3f(2.0f, 2.0f, 2.0f)));
            using (var frameRate = new Variant(30.0f))
                sourceAnimation.AddMetadata(AnimationMetadata.FrameRate, frameRate);

            using var initialPose = new Animation();
            var initialPoseTrack = initialPose.CreateTrack("Root");
            initialPoseTrack.ChannelMask =
                AnimationChannel.Position | AnimationChannel.Rotation | AnimationChannel.Scale;
            var targetBindRotation = new Quaternion(0.0f, 45.0f, 0.0f);
            initialPoseTrack.AddKeyFrame(new AnimationKeyFrame(
                0.0f, new Vector3f(0.0f, 3.0f, 0.0f), targetBindRotation,
                new Vector3f(1.5f, 1.5f, 1.5f)));

            using var transformer = new RetargetAnimationsTransformer();
            using var targetAnimation = Assert.IsType<Animation>(transformer.RetargetAnimation(
                new RetargetAnimationTask
                {
                    SourceModel = sourceModel,
                    SourceAnimation = sourceAnimation,
                    TargetModel = targetModel,
                    TargetInitialPose = initialPose,
                    TargetAnimationName = "Animations/Walk.ani",
                }));

            Assert.Equal("Animations/Walk.ani", targetAnimation.GetName());
            Assert.Equal("Walk", targetAnimation.AnimationName);
            Assert.Equal(1.0f, targetAnimation.Length);
            Assert.Equal(30.0f, targetAnimation.GetMetadata(AnimationMetadata.FrameRate).GetFloat());

            var targetTrack = Assert.IsType<AnimationTrack>(targetAnimation.GetTrack("Root"));
            Assert.Equal(sourceTrack.ChannelMask, targetTrack.ChannelMask);
            Assert.Equal(2, targetTrack.KeyFrames.Count);
            Assert.Equal(new Vector3f(0.0f, 3.0f, 0.0f), targetTrack.KeyFrames[0].Position);
            Assert.Equal(new Vector3f(3.0f, 3.0f, 0.0f), targetTrack.KeyFrames[1].Position);
            Assert.True(targetTrack.KeyFrames[0].Rotation.Equivalent(targetBindRotation));
            Assert.True(targetTrack.KeyFrames[1].Rotation.Equivalent(
                new Quaternion(0.0f, 30.0f, 0.0f) * targetBindRotation));
            Assert.Equal(new Vector3f(2.0f, 2.0f, 2.0f), targetTrack.KeyFrames[1].Scale);

            Assert.Null(transformer.RetargetAnimation(new RetargetAnimationTask()));

            var invalidIKTask = new RetargetAnimationTask
            {
                SourceModel = sourceModel,
                SourceAnimation = sourceAnimation,
                TargetModel = targetModel,
                IKChains = [["Root", "Root"]],
            };
            Assert.Null(transformer.RetargetAnimation(invalidIKTask));
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
            Path.GetTempPath(), "RedBoltRetargetAnimations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(Path.Combine(directory, "RetargetAnimations.json"), "{}");

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(RetargetAnimationsTransformer).Assembly);
            var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountDir(directory);
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(new ResourceCache());

            using var reflected = Assert.IsType<RetargetAnimationsTransformer>(
                context.CreateObject(new StringHash(nameof(RetargetAnimationsTransformer))));
            using var transformer = new RetargetAnimationsTransformer();
            var input = new AssetTransformerInput
            {
                InputFileName = "RetargetAnimations.json",
                ResourceName = "RetargetAnimations.json",
            };
            var output = new AssetTransformerOutput();

            Assert.True(transformer.Execute(input, output, [transformer]));
            Assert.Empty(output.ModifiedResourceNames);
            Assert.Equal("RetargetAnimations.json", transformer.GetParametersFileName());
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }

    private static Model CreateSingleBoneModel(
        string boneName, Vector3f initialPosition, Quaternion initialRotation)
    {
        using var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        var bone = skeleton.GetModifiableBones()[0];
        bone.Name = boneName;
        bone.NameHash = new StringHash(boneName);
        bone.ParentIndex = 0;
        bone.InitialPosition = initialPosition;
        bone.InitialRotation = initialRotation;
        skeleton.SetRootBoneIndex(0);
        skeleton.UpdateBoneOrder();

        var model = new Model();
        model.SetSkeleton(skeleton);
        return model;
    }
}
