// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class TriggerAnimatorTests
{
    public TriggerAnimatorTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Trigger_animator_mirrors_animation_time_across_first_enter_and_last_exit()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Trigger");
        var controller = node.CreateComponent<AnimationController>();
        var animator = node.CreateComponent<TestTriggerAnimator>();
        using var enterAnimation = new Animation();
        enterAnimation.SetName("Enter");
        enterAnimation.SetLength(2.0f);
        using var exitAnimation = new Animation();
        exitAnimation.SetName("Exit");
        exitAnimation.SetLength(4.0f);
        animator.SetEnterAnimation(enterAnimation);
        animator.SetExitAnimation(exitAnimation);

        var rejected = scene.CreateChild("Rejected");
        var acceptedA = scene.CreateChild("Accepted A");
        var acceptedB = scene.CreateChild("Accepted B");

        animator.Enter(rejected);
        Assert.Equal(0, animator.EnterCount);

        animator.Enter(acceptedA);
        animator.Enter(acceptedB);
        Assert.Equal(1, animator.EnterCount);
        Assert.Equal(0, animator.ExitCount);
        Assert.Same(enterAnimation, animator.GetEnterAnimation());
        Assert.Same(exitAnimation, animator.GetExitAnimation());

        var enterIndex = controller.FindLastAnimation(enterAnimation);
        Assert.True(enterIndex >= 0);
        controller.GetAnimationParameters(enterIndex).SetTime(0.5f);

        animator.Exit(acceptedA);
        Assert.Equal(0, animator.ExitCount);
        animator.Exit(acceptedB);
        Assert.Equal(1, animator.ExitCount);

        var exitIndex = controller.FindLastAnimation(exitAnimation);
        Assert.True(exitIndex >= 0);
        var exitParameters = controller.GetAnimationParameters(exitIndex);
        Assert.True(exitParameters.RemoveOnZeroWeight);
        Assert.InRange(exitParameters.GetTime(), 2.999f, 3.001f);

        var attributes = Context.Instance.GetReflection(ObjectReflection<TriggerAnimator>.TypeId)!.Attributes!;
        var names = attributes.Select(attribute => attribute.Name).ToList();
        Assert.True(names.IndexOf("Enter Animation") < names.IndexOf("Exit Animation"));
        Assert.True(names.IndexOf("Exit Animation") < names.IndexOf("Is Entered"));
    }

    [Fact]
    public void Trigger_animator_receives_node_collision_enter_and_exit_events()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var triggerNode = scene.CreateChild("Trigger");
        triggerNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var triggerBody = triggerNode.CreateComponent<RigidBody>();
        triggerBody.IsTrigger = true;
        triggerBody.CollisionEventMode = CollisionEventMode.Always;
        var animator = triggerNode.CreateComponent<TestTriggerAnimator>();

        var rejected = CreateDynamicBox(scene, "Rejected", Vector3f.Zero);
        world.Update(1.0f / 60.0f);
        Assert.Equal(0, animator.EnterCount);
        rejected.Position = new Vector3f(3.0f, 0.0f, 0.0f);
        world.Update(1.0f / 60.0f);

        var accepted = CreateDynamicBox(scene, "Accepted", Vector3f.Zero);
        world.Update(1.0f / 60.0f);
        Assert.Equal(1, animator.EnterCount);

        accepted.Position = new Vector3f(3.0f, 0.0f, 0.0f);
        world.Update(1.0f / 60.0f);
        Assert.Equal(1, animator.ExitCount);

        animator.Enabled = false;
        accepted.Position = Vector3f.Zero;
        world.Update(1.0f / 60.0f);
        Assert.Equal(1, animator.EnterCount);
    }

    private static RigidBody CreateDynamicBox(Scene scene, string name, Vector3f position)
    {
        var node = scene.CreateChild(name);
        node.SetPosition(position);
        node.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        body.UseGravity = false;
        body.CollisionEventMode = CollisionEventMode.Always;
        return body;
    }

    [Reflected]
    private sealed partial class TestTriggerAnimator : TriggerAnimator
    {
        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }

        public void Enter(Node node) => RegisterEnter(node);
        public void Exit(Node node) => RegisterExit(node);

        public override bool Filter(Node node) => node.Name.StartsWith("Accepted", StringComparison.Ordinal);

        public override void OnEnter()
        {
            ++EnterCount;
        }

        public override void OnExit()
        {
            ++ExitCount;
        }
    }
}
