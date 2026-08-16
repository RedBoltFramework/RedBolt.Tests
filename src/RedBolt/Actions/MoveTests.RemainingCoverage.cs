// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;

public sealed partial class MoveTests
{
    [Fact]
    public void Move_attribute_actions_reject_wrong_attribute_types_without_manager_state()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var target = new MoveTarget();

        using var move = new ExposedMoveBy();
        move.SetAttributeName("Rotation");
        Assert.NotNull(move.Start(target));

        using var quadratic = new ExposedMoveByQuadratic();
        quadratic.SetAttributeName("Rotation");
        Assert.NotNull(quadratic.Start(target));

        using var jump = new ExposedJumpBy();
        jump.SetAttributeName("Rotation");
        Assert.NotNull(jump.Start(target));

        using var scale = new ExposedScaleBy();
        scale.SetAttributeName("Position2i");
        Assert.NotNull(scale.Start(target));

        using var rotate = new ExposedRotateBy();
        rotate.SetAttributeName("Position");
        Assert.NotNull(rotate.Start(target));
    }

    private sealed class ExposedMoveBy : MoveBy
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }

    private sealed class ExposedMoveByQuadratic : MoveByQuadratic
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }

    private sealed class ExposedJumpBy : JumpBy
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }

    private sealed class ExposedScaleBy : ScaleBy
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }

    private sealed class ExposedRotateBy : RotateBy
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }
}
