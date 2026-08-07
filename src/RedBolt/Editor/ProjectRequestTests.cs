namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Scene;

public sealed class ProjectRequestTests
{
    [Fact]
    public void Invoke_uses_highest_priority_callback_without_removing_it()
    {
        WithRequest(request =>
        {
            var lowCount = 0;
            var highCount = 0;
            var middleCount = 0;
            request.QueueProcessCallback(() => ++lowCount, int.MinValue);
            request.QueueProcessCallback(() => ++highCount, 10);
            request.QueueProcessCallback(() => ++middleCount, 0);

            request.InvokeProcessCallback();
            request.InvokeProcessCallback();

            Assert.Equal(0, lowCount);
            Assert.Equal(2, highCount);
            Assert.Equal(0, middleCount);
        });
    }

    [Fact]
    public void Invoke_with_no_callbacks_does_nothing()
    {
        WithRequest(static request => request.InvokeProcessCallback());
    }

    [Fact]
    public void Base_inspect_request_inherits_project_request_processing()
    {
        WithRequest(request =>
        {
            var invoked = false;
            request.QueueProcessCallback(() => invoked = true);

            request.InvokeProcessCallback();

            Assert.True(invoked);
            Assert.IsType<BaseInspectRequest>(request);
        }, static () => new BaseInspectRequest());
    }

    [Fact]
    public void Node_component_request_filters_nulls_orders_selection_and_returns_common_scene()
    {
        WithContext(() =>
        {
            using var scene = new Scene();
            var firstNode = scene.CreateChild("First", 100);
            var secondNode = scene.CreateChild("Second", 200);
            var firstComponent = firstNode.CreateComponent<Camera>(300);
            var secondComponent = secondNode.CreateComponent<Camera>(400);
            using var request = new InspectNodeComponentRequest(
                new Node?[] { secondNode, null, firstNode },
                new Component?[] { secondComponent, null, firstComponent });

            Assert.Equal([100u, 200u], request.Nodes.Select(GetTarget).Select(static node => node.Id));
            Assert.Equal([300u, 400u], request.Components.Select(GetTarget).Select(static component => component.Id));
            Assert.Same(scene, request.CommonScene);
            Assert.False(request.IsEmpty);
            Assert.True(request.HasNodes);
            Assert.True(request.HasComponents);
        });
    }

    [Fact]
    public void Node_component_request_rejects_missing_or_different_scenes()
    {
        WithContext(() =>
        {
            using var firstScene = new Scene();
            using var secondScene = new Scene();
            var firstNode = firstScene.CreateChild("First");
            var secondNode = secondScene.CreateChild("Second");
            using var mixedRequest = new InspectNodeComponentRequest(
                new[] { firstNode, secondNode }, Array.Empty<Component>());
            using var detachedNode = new Node();
            using var detachedRequest = new InspectNodeComponentRequest(
                new[] { detachedNode }, Array.Empty<Component>());

            Assert.Null(mixedRequest.CommonScene);
            Assert.Null(detachedRequest.CommonScene);
        });
    }

    [Fact]
    public void Create_resource_request_retains_factory_and_inherits_processing()
    {
        WithContext(() =>
        {
            using var factory = new TestResourceFactory();
            using var request = new CreateResourceRequest(factory);
            var invoked = false;
            request.QueueProcessCallback(() => invoked = true);

            request.InvokeProcessCallback();

            Assert.Same(factory, request.Factory);
            Assert.True(invoked);
        });
    }

    private static void WithRequest(
        Action<ProjectRequest> test, Func<ProjectRequest>? createRequest = null)
    {
        WithContext(() =>
        {
            using var request = createRequest?.Invoke() ?? new ProjectRequest();
            test(request);
        });
    }

    private static void WithContext(Action test)
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            test();
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private static T GetTarget<T>(WeakReference<T> reference)
        where T : class
    {
        Assert.True(reference.TryGetTarget(out var target));
        return target;
    }

    private sealed class TestResourceFactory() : ResourceFactory(0, "Test")
    {
        public override void Open(string baseFilePath, string baseResourcePath)
        {
        }

        public override void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit)
        {
        }

        public override void CommitAndClose()
        {
        }
    }
}
