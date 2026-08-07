namespace RedBolt.Tests;

using RedBolt.Editor.Project;

public sealed class CloseDialogTests
{
    [Fact]
    public void Close_resource_request_clone_has_independent_resource_names_and_copied_callbacks()
    {
        var saveCount = 0;
        var request = new CloseResourceRequest
        {
            ResourceNames = ["Scenes/First.xml"],
            OnSave = () => ++saveCount,
        };

        var clone = request.Clone();
        request.ResourceNames.Add("Scenes/Second.xml");
        request.OnSave = static () => { };

        Assert.Equal(["Scenes/First.xml"], clone.ResourceNames);
        clone.OnSave();
        Assert.Equal(1, saveCount);
        clone.OnDiscard();
        clone.OnCancel();
    }
}
