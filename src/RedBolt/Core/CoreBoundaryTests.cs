// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class CoreBoundaryTests
{
    [Fact]
    public void String_hash_register_preserves_first_value_for_a_collision()
    {
        var register = new StringHashRegister(threadSafe: false);
        var hash = new StringHash("first");

        Assert.Equal(hash, register.RegisterString(hash, "first"));
        Assert.Equal(hash, register.RegisterString(hash, "second"));
        Assert.True(register.Contains(hash));
        Assert.Equal("first", register.GetString(hash));
        Assert.Equal("first", register.GetStringCopy(hash));
        Assert.Equal(string.Empty, register.GetString(new StringHash("missing")));
    }

    [Fact]
    public void String_hash_register_supports_thread_safe_registration_and_lookup()
    {
        var register = new StringHashRegister(threadSafe: true);

        var hash = register.RegisterString("stable");

        Assert.True(register.Contains(hash));
        Assert.Equal("stable", register.GetStringCopy(hash));
        Assert.Contains(hash, register.GetInternalMap().Keys);
    }

    [Fact]
    public void Spline_clamps_linear_sampling_and_clone_mutations_are_independent()
    {
        using var knots = new VariantVector { new(0.0f), new(10.0f) };
        using var spline = new Spline(knots, InterpolationMode.LinearCurve);
        using var clone = (Spline)spline.Clone();

        using var below = spline.GetPoint(-1.0f);
        using var middle = spline.GetPoint(0.5f);
        using var above = spline.GetPoint(2.0f);
        using var changed = new Variant(20.0f);
        clone.SetKnot(changed, 0);
        using var originalAfterCloneMutation = spline.GetPoint(0.0f);

        Assert.Equal(0.0f, below.GetFloat());
        Assert.Equal(5.0f, middle.GetFloat());
        Assert.Equal(10.0f, above.GetFloat());
        Assert.Equal(0.0f, originalAfterCloneMutation.GetFloat());
    }

    [Fact]
    public void Context_ref_event_exposes_active_sender_and_handler_and_allows_payload_mutation()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var sender = new TestObject();
            using var receiver = new TestObject();
            var eventData = new MutableEvent { Value = 3 };
            EventHandler? activeHandler = null;

            receiver.SubscribeToEvent<MutableEvent>((ref MutableEvent args) =>
            {
                args.Value *= 2;
                activeHandler = receiver.GetEventHandler();
                Assert.Same(sender, receiver.GetEventSender());
            });

            sender.SendEvent(ref eventData);

            Assert.Equal(6, eventData.Value);
            Assert.NotNull(activeHandler);
            Assert.Same(receiver, activeHandler!.Receiver);
            Assert.Null(activeHandler.Sender);
            Assert.Equal(typeof(MutableEvent), activeHandler.EventType);
            Assert.Null(context.GetEventSender());
            Assert.Null(context.GetEventHandler());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_clears_active_event_state_when_a_handler_throws()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var sender = new TestObject();
            using var receiver = new TestObject();
            receiver.SubscribeToEvent<ThrowingEvent>((ref ThrowingEvent _) =>
                throw new InvalidOperationException("handler failure"));

            var exception = Assert.Throws<InvalidOperationException>(() => sender.SendEvent<ThrowingEvent>());

            Assert.Equal("handler failure", exception.Message);
            Assert.Null(context.GetEventSender());
            Assert.Null(context.GetEventHandler());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Archive_exception_preserves_the_serialization_failure_message()
    {
        var exception = new ArchiveException("invalid archive token");

        Assert.IsAssignableFrom<InvalidOperationException>(exception);
        Assert.Equal("invalid archive token", exception.Message);
    }

    private struct MutableEvent
    {
        public int Value;
    }

    private struct ThrowingEvent
    {
    }

    private sealed class TestObject : RedObject
    {
    }
}
