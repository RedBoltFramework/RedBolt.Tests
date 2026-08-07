namespace RedBolt.Tests;

using RedBolt.Editor.Project;

public sealed class ProjectTypesTests
{
    [Fact]
    public void Project_flags_combine_and_test_independently()
    {
        var flags = ProjectFlag.None;
        flags |= ProjectFlag.ReadOnly;

        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.ReadOnly);
        Assert.Equal(ProjectFlag.None, flags & ProjectFlag.SingleProcess);

        flags |= ProjectFlag.SingleProcess;

        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.ReadOnly);
        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.SingleProcess);
    }
}
