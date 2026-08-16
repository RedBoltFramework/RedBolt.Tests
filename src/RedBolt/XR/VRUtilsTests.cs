// Copyright (c) 2026 RedBolt and contributors.

namespace RedBolt.Tests.XRTests;

using RedBolt.Core;
using RedBolt.XR;

public class VRUtilsTests
{
    [Fact]
    public void OpenXRSubsystemCanBeConstructed()
    {
        var previousContext = Context.Instance;
        using var context = new Context(previousContext.RuntimeServices);
        Context.SetInstance(context);
        try
        {
            using var openXR = new OpenXR();
            Assert.Equal(VRRuntime.OpenXR, openXR.Runtime);
            Assert.False(openXR.IsConnected);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void PublicTypesPreserveRbfxDefaultsAndEnumValues()
    {
        var parameters = new VRSessionParameters();

        Assert.Equal(string.Empty, parameters.ManifestPath);
        Assert.Equal(0, parameters.MultiSample);
        Assert.Equal(1.0f, parameters.ResolutionScale);
        Assert.Equal(-1, (int)VRHand.None);
        Assert.Equal(0, (int)VRHand.Left);
        Assert.Equal(1, (int)VRHand.Right);
        Assert.Equal(2, (int)VRHand.Count);
        Assert.Equal(-1, (int)VREye.None);
        Assert.False(new VRRigDesc().IsValid());
    }

    [Fact]
    public void ActionGroupFindsBindingsCaseInsensitivelyAndFiltersHands()
    {
        var left = new TestBinding("grab", VRHand.Left, VariantType.Float);
        var right = new TestBinding("grab", VRHand.Right, VariantType.Float);
        var group = new TestActionGroup(left, right);

        Assert.Same(left, group.FindBinding("GRAB", VRHand.Left));
        Assert.Same(left, group.FindBinding("grab", VRHand.None));
        Assert.Same(right, group.FindBinding("grab", VRHand.Right));
        Assert.Null(group.FindBinding("missing", VRHand.None));
        Assert.Equal([left, right], group.GetBindings());
    }

    [Fact]
    public void ButtonClickedFiresOnlyWhenTargetIsReleased()
    {
        var current = 4;

        Assert.False(VRUtils.ButtonClicked(4, ref current, 2));
        Assert.Equal(2, current);
        current = 4;
        Assert.True(VRUtils.ButtonClicked(4, ref current, 0));
        Assert.Equal(0, current);
    }

    [Fact]
    public void ButtonLongPressReportsProgressAndFiresOnce()
    {
        var current = 0;
        var time = 0.0f;
        var alreadyDone = false;

        Assert.False(VRUtils.ButtonLongPress(4, ref current, ref time, ref alreadyDone, 4, 0.5f, 1.0f,
            out var fraction));
        Assert.Equal(0.0f, fraction);
        Assert.False(VRUtils.ButtonLongPress(4, ref current, ref time, ref alreadyDone, 4, 0.6f, 1.0f,
            out fraction));
        Assert.Equal(0.6f, fraction);
        Assert.True(VRUtils.ButtonLongPress(4, ref current, ref time, ref alreadyDone, 4, 0.5f, 1.0f,
            out fraction));
        Assert.Equal(1.0f, fraction);
        Assert.True(alreadyDone);
        Assert.False(VRUtils.ButtonLongPress(4, ref current, ref time, ref alreadyDone, 4, 1.0f, 1.0f));
    }

    [Fact]
    public void ButtonShortOrLongPressPreservesRbfxReleaseBehavior()
    {
        var current = 0;
        var time = 0.0f;
        var alreadyDone = false;

        Assert.Equal(0, VRUtils.ButtonShortOrLongPress(4, ref current, ref time, ref alreadyDone,
            4, 0.0f, 3.0f, 1.0f));
        Assert.Equal(0, VRUtils.ButtonShortOrLongPress(4, ref current, ref time, ref alreadyDone,
            4, 0.25f, 3.0f, 1.0f));

        // rbfx computes the short-press result on release but returns zero.
        Assert.Equal(0, VRUtils.ButtonShortOrLongPress(4, ref current, ref time, ref alreadyDone,
            0, 0.0f, 3.0f, 1.0f));
        Assert.Equal(0, current);
    }

    [Fact]
    public void ButtonTapOrHoldDistinguishesTapAndHold()
    {
        var current = 0;
        var time = 0.0f;

        Assert.Equal(0, VRUtils.ButtonTapOrHold(2, ref current, ref time, 2, 0.0f, 0.5f));
        Assert.Equal(0, VRUtils.ButtonTapOrHold(2, ref current, ref time, 2, 0.25f, 0.5f));
        Assert.Equal(1, VRUtils.ButtonTapOrHold(2, ref current, ref time, 0, 0.0f, 0.5f));

        Assert.Equal(0, VRUtils.ButtonTapOrHold(2, ref current, ref time, 2, 0.0f, 0.5f));
        Assert.Equal(0, VRUtils.ButtonTapOrHold(2, ref current, ref time, 2, 0.6f, 0.5f));
        Assert.Equal(2, VRUtils.ButtonTapOrHold(2, ref current, ref time, 2, 0.1f, 0.5f));
    }

    [Fact]
    public void ButtonCommandProvidesStrictEdgeDetection()
    {
        var command = new ButtonCommand(4);

        Assert.True(command.CheckStrict(4));
        Assert.False(command.CheckStrict(4));
        Assert.False(command.CheckStrict(0));
        Assert.True(command.CheckStrict(4));
        command.Reset();
        Assert.Equal(0, command.CurrentCode);
        Assert.Equal(0.0f, command.Time);
        Assert.False(command.AlreadyDone);
    }

    private sealed class TestBinding(string name, VRHand hand, VariantType type)
        : XRBinding(name, name, hand, type, false, false);

    private sealed class TestActionGroup : XRActionGroup
    {
        public TestActionGroup(params XRBinding[] bindings) : base("default", "Default") =>
            Bindings.AddRange(bindings);
    }
}
