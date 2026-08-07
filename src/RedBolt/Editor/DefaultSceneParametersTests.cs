namespace RedBolt.Tests;

using RedBolt.Editor.Project;

public sealed class DefaultSceneParametersTests
{
    [Fact]
    public void Parameters_default_to_false_and_copy_by_value()
    {
        var source = new DefaultSceneParameters();

        Assert.False(source.HighQuality);
        Assert.False(source.CreateObjects);
        Assert.False(source.IsPrefab);

        source.HighQuality = true;
        var copy = source;
        copy.CreateObjects = true;
        copy.IsPrefab = true;

        Assert.True(source.HighQuality);
        Assert.False(source.CreateObjects);
        Assert.False(source.IsPrefab);
        Assert.True(copy.HighQuality);
        Assert.True(copy.CreateObjects);
        Assert.True(copy.IsPrefab);
    }
}
