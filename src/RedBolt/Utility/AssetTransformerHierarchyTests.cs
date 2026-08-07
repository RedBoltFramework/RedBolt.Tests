namespace RedBolt.Tests;

using RedBolt.Engine;
using RedBolt.Utility;

public sealed class AssetTransformerHierarchyTests
{
    [Fact]
    public void Hierarchy_orders_filters_deduplicates_removes_and_clears_transformers()
    {
        using var hierarchy = new AssetTransformerHierarchy(false);
        using var dependencyA = new DependencyA();
        using var dependencyB = new DependencyB();
        using var independent = new IndependentTransformer();
        hierarchy.AddTransformer(string.Empty, dependencyA);
        hierarchy.AddTransformer(string.Empty, independent);
        hierarchy.AddTransformer(string.Empty, dependencyB);

        Assert.True(hierarchy.AddDependency(dependencyA.GetTypeName(), dependencyB.GetTypeName()));
        Assert.False(hierarchy.AddDependency(dependencyB.GetTypeName(), dependencyA.GetTypeName()));
        hierarchy.CommitDependencies();

        Assert.Equal(
            [independent, dependencyB, dependencyA],
            hierarchy.GetTransformerCandidates("Models/Fox.mdl"));

        using var outer = new SingleTransformer("outer");
        using var inner = new SingleTransformer("inner");
        hierarchy.AddTransformer(string.Empty, outer);
        hierarchy.AddTransformer("Models", inner);
        Assert.Same(inner, Assert.Single(
            hierarchy.GetTransformerCandidates("Models/Fox.mdl").OfType<SingleTransformer>()));

        using var firstMultiple = new MultipleTransformer();
        using var secondMultiple = new MultipleTransformer();
        hierarchy.AddTransformer(string.Empty, firstMultiple);
        hierarchy.AddTransformer(string.Empty, secondMultiple);
        Assert.Equal(
            [firstMultiple, secondMultiple],
            hierarchy.GetTransformerCandidates("Models/Fox.mdl").OfType<MultipleTransformer>());

        using var genericFlavor = new FlavorTransformer("generic");
        using var exactFlavor = new FlavorTransformer("exact");
        var genericPattern = new ApplicationFlavorPattern("platform=desktop");
        var exactPattern = new ApplicationFlavorPattern("platform=desktop,linux");
        genericFlavor.SetFlavor(genericPattern);
        exactFlavor.SetFlavor(exactPattern);
        hierarchy.AddTransformer(string.Empty, genericFlavor);
        hierarchy.AddTransformer(string.Empty, exactFlavor);
        var flavor = new ApplicationFlavor("platform=desktop,linux");
        Assert.Same(exactFlavor, Assert.Single(
            hierarchy.GetTransformerCandidates("Models/Fox.mdl", flavor).OfType<FlavorTransformer>()));

        using var postTransformer = new PostTransformer();
        hierarchy.AddTransformer(string.Empty, postTransformer);
        Assert.DoesNotContain(postTransformer, hierarchy.GetTransformerCandidates("Models/Fox.mdl"));

        Assert.True(hierarchy.RemoveTransformers(typeof(SingleTransformer)));
        Assert.False(hierarchy.RemoveTransformers(typeof(SingleTransformer)));
        Assert.Empty(hierarchy.GetTransformerCandidates("Models/Fox.mdl").OfType<SingleTransformer>());

        hierarchy.Clear();
        Assert.Empty(hierarchy.GetTransformerCandidates("Models/Fox.mdl"));

        using var postHierarchy = new AssetTransformerHierarchy(true);
        postHierarchy.AddTransformer(string.Empty, independent);
        postHierarchy.AddTransformer(string.Empty, postTransformer);
        Assert.Equal([postTransformer], postHierarchy.GetTransformerCandidates("Models/Fox.mdl"));
    }

    private sealed class DependencyA : AssetTransformer;

    private sealed class DependencyB : AssetTransformer;

    private sealed class IndependentTransformer : AssetTransformer;

    private sealed class SingleTransformer(string name) : AssetTransformer
    {
        public string Name { get; } = name;
    }

    private sealed class MultipleTransformer : AssetTransformer
    {
        public override bool IsSingleInstanced() => false;
    }

    private sealed class FlavorTransformer(string name) : AssetTransformer
    {
        public string Name { get; } = name;
    }

    private sealed class PostTransformer : AssetTransformer
    {
        public override bool IsPostTransform() => true;
    }
}
