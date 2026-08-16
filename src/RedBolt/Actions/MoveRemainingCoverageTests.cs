// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class MoveRemainingCoverageTests
{
    [Fact]
    public void Move_actions_serialize_all_optional_fields()
    {
        using var move = new MoveBy();
        move.SetDuration(2.0f);
        move.SetPositionDelta(new Vector3f(1, 2, 3));

        using var quadratic = new MoveByQuadratic();
        quadratic.SetDuration(2.0f);
        quadratic.SetPositionDelta(new Vector3f(4, 5, 6));
        quadratic.SetControlDelta(new Vector3f(7, 8, 9));

        using var jump = new JumpBy();
        jump.SetDuration(2.0f);
        jump.SetPositionDelta(new Vector3f(2, 3, 4));

        using var scale = new ScaleBy();
        scale.SetDuration(2.0f);
        scale.SetScaleDelta(new Vector3f(2, 3, 4));

        using var rotate = new RotateBy();
        rotate.SetDuration(2.0f);
        rotate.SetRotationDelta(new Quaternion(45.0f, Vector3f.Up));

        using var around = new RotateAround();
        around.SetDuration(2.0f);
        around.SetRotationDelta(new Quaternion(60.0f, Vector3f.Right));
        around.SetPivot(new Vector3f(1, -2, 3));

        AssertSerialized(move, "delta");
        AssertSerialized(quadratic, "control");
        AssertSerialized(jump, "delta");
        AssertSerialized(scale, "delta");
        AssertSerialized(rotate, "delta");
        AssertSerialized(around, "pivot");
    }

    [Fact]
    public void Rotate_around_state_handles_non_serializable_and_invalid_attributes()
    {
        AssertInvalidRotateAroundTarget(new BaseAction());
        AssertInvalidRotateAroundTarget(new PositionOnlyTarget());
        AssertInvalidRotateAroundTarget(new WrongRotationTarget());
        AssertInvalidRotateAroundTarget(new RotationOnlyTarget());
        AssertInvalidRotateAroundTarget(new WrongPositionTarget());
    }

    private static void AssertSerialized(BaseAction action, string expectedField)
    {
        using (action)
        {
            using var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("action"))
                action.SerializeInBlock(output);

            Assert.Contains(expectedField, output.ToJsonString(), StringComparison.Ordinal);
        }
    }

    private static void AssertInvalidRotateAroundTarget(RedObject target)
    {
        using (target)
        {
            using var action = new ExposedRotateAround();
            var state = action.Start(target);
            state.Update(0.0f);
            (state as IDisposable)?.Dispose();
        }
    }

    private sealed class ExposedRotateAround : RotateAround
    {
        public ActionState Start(RedObject target) => StartAction(target);
    }

    [Reflected]
    private sealed partial class PositionOnlyTarget : Serializable
    {
        [Serialized("Position")]
        public Vector3f Position { get; set; }
    }

    [Reflected]
    private sealed partial class WrongRotationTarget : Serializable
    {
        [Serialized("Rotation")]
        public float Rotation { get; set; }

        [Serialized("Position")]
        public Vector3f Position { get; set; }
    }

    [Reflected]
    private sealed partial class RotationOnlyTarget : Serializable
    {
        [Serialized("Rotation")]
        public Quaternion Rotation { get; set; }
    }

    [Reflected]
    private sealed partial class WrongPositionTarget : Serializable
    {
        [Serialized("Rotation")]
        public Quaternion Rotation { get; set; }

        [Serialized("Position")]
        public float Position { get; set; }
    }
}
