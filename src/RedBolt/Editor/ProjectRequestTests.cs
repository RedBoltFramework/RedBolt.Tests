namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Project;

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

    private static void WithRequest(Action<ProjectRequest> test)
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var request = new ProjectRequest();
            test(request);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
