// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Particles;
using RedBolt.Particles.ParticleGraphNodes;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ParticleGraphRenderingTests
{
    public ParticleGraphRenderingTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        if (fixture.Context.GetSubsystem<ParticleGraphSystem>() is null)
            fixture.Context.RegisterSubsystem(new ParticleGraphSystem());
    }

    [Fact]
    public void BillboardRendererCreatesVisibleRaycastableParticleAndTearsDown()
    {
        using var effect = new ParticleGraphEffect();
        effect.SetNumLayers(1);
        var layer = effect.GetLayer(0);
        layer.Capacity = 2;
        var position = AddConstant(layer.InitGraph, Vector3f.Zero);
        var size = AddConstant(layer.InitGraph, new Vector2f(2.0f, 2.0f));
        var frame = AddConstant(layer.InitGraph, 0.0f);
        var color = AddConstant(layer.InitGraph, Color.Red);
        var rotation = AddConstant(layer.InitGraph, 0.0f);
        var direction = AddConstant(layer.InitGraph, Vector3f.Up);
        var render = new RenderBillboard { Rows = 1, Columns = 1 };
        Assert.True(render.SetPinSource(0, position));
        Assert.True(render.SetPinSource(1, size));
        Assert.True(render.SetPinSource(2, frame));
        Assert.True(render.SetPinSource(3, color));
        Assert.True(render.SetPinSource(4, rotation));
        Assert.True(render.SetPinSource(5, direction));
        layer.InitGraph.Add(render);

        using var scene = new Scene();
        var emitter = scene.CreateChild("Emitter").CreateComponent<ParticleGraphEmitter>();
        emitter.SetEffect(effect);
        var octree = scene.GetComponent<Octree>()!;
        Assert.Equal(1, CountDrawables(octree));
        Assert.True(emitter.EmitNewParticle(0));

        using var query = new RayOctreeQuery(
            new Ray(new Vector3f(0, 0, -5), Vector3f.Forward), RayQueryLevel.RayTriangle);
        octree.Raycast(query);

        var hit = Assert.Single(query.Result);
        Assert.IsType<BillboardSet>(hit.Drawable);
        Assert.Equal(0u, hit.SubObject);

        emitter.ViewMask = 0x1u;
        Assert.Empty(Raycast(octree, 0x2u));
        Assert.Single(Raycast(octree, 0x1u));

        emitter.Dispose();
        Assert.Equal(0, CountDrawables(octree));
    }

    [Fact]
    public void MeshRendererCreatesOneTransformAndTearsDown()
    {
        using var effect = new ParticleGraphEffect();
        effect.SetNumLayers(1);
        var layer = effect.GetLayer(0);
        layer.Capacity = 2;
        var transform = AddConstant(layer.InitGraph,
            new Matrix3x4(new Vector3f(1, 2, 3), new Quaternion(), Vector3f.One));
        var render = new RenderMesh
        {
            Model = new ResourceRef(typeof(Model).GetTypeId(), "Models/ProceduralBox")
        };
        Assert.True(render.SetPinSource(0, transform));
        layer.InitGraph.Add(render);

        using var scene = new Scene();
        var emitter = scene.CreateChild("Emitter").CreateComponent<ParticleGraphEmitter>();
        emitter.SetEffect(effect);
        var octree = scene.GetComponent<Octree>()!;
        Assert.Equal(1, CountDrawables(octree));
        Assert.True(emitter.EmitNewParticle(0));

        using var query = new AllContentOctreeQuery([], DrawableFlags.Geometry, Drawable.DefaultViewMask);
        octree.GetDrawables(query);
        var drawable = Assert.IsType<RenderMeshDrawable>(Assert.Single(query.Result));
        Assert.Equal(1u, drawable.NumTransforms);
        Assert.Equal(new Vector3f(1, 2, 3), drawable.Transforms[0].Translation);

        emitter.Dispose();
        Assert.Equal(0, CountDrawables(octree));
    }

    private static List<RayQueryResult> Raycast(Octree octree, uint viewMask)
    {
        var result = new List<RayQueryResult>();
        using var query = new RayOctreeQuery(result,
            new Ray(new Vector3f(0, 0, -5), Vector3f.Forward), RayQueryLevel.RayTriangle,
            viewMask: viewMask);
        octree.Raycast(query);
        return result;
    }

    private static int CountDrawables(Octree octree)
    {
        var result = new List<Drawable>();
        using var query = new AllContentOctreeQuery(result, DrawableFlags.Any, Drawable.DefaultViewMask);
        octree.GetDrawables(query);
        return result.Count;
    }

    private static int AddConstant(ParticleGraph graph, float value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Vector2f value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Vector3f value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Color value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Matrix3x4 value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, in Variant value)
    {
        var constant = new Constant();
        constant.SetValue(value);
        return graph.Add(constant);
    }

    private readonly HeadlessEngineFixture _fixture;
}
