namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.SystemUI;

public sealed class BaseWidgetTests
{
    [Fact]
    public void Widget_uses_global_context_renders_content_and_unsubscribes_on_dispose()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var widget = new TestWidget();

        try
        {
            widget.SubscribeToEvent<TestEvent>(widget.OnTestEvent);

            Assert.Same(context, widget.Context);
            Assert.True(widget.HasSubscribedToEvent<TestEvent>());

            widget.RenderContent();
            widget.Dispose();

            Assert.Equal(1, widget.RenderCount);
            Assert.False(widget.HasSubscribedToEvent<TestEvent>());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private struct TestEvent
    {
    }

    private sealed class TestWidget : BaseWidget
    {
        public int RenderCount { get; private set; }

        public void OnTestEvent(ref TestEvent args)
        {
        }

        public override void RenderContent()
        {
            ++RenderCount;
        }
    }
}
