// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneTransformCoverageTests
{
    [Fact]
    public void Node_transforms_cover_local_parent_world_and_2d_paths()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        parent.SetTransform(new Vector3f(10, 2, 0), Quaternion.IDENTITY, new Vector3f(2, 2, 2));
        var child = parent.CreateChild("Child");
        child.SetPosition(new Vector3f(1, 2, 3));
        child.SetRotation2D(0.25f);
        child.SetScale(Vector3f.Zero);

        Assert.Equal(MathDefs.Epsilon, child.Scale.X);
        Assert.Equal(new Vector3f(12, 6, 6), child.WorldPosition);
        _ = child.WorldRotation;
        _ = child.WorldDirection;
        _ = child.WorldUp;
        _ = child.WorldRight;
        _ = child.WorldScale;
        _ = child.GetSignedWorldScale();
        _ = child.GetWorldTransform();
        _ = child.GetWorldPosition2D();
        _ = child.GetWorldRotation2D();
        _ = child.GetWorldScale2D();
        _ = child.Direction;
        _ = child.Right;
        _ = child.Up;
        _ = child.GetTransform();
        _ = child.GetTransformMatrix();
        _ = child.GetPosition2D();
        _ = child.GetScale2D();

        var world = child.LocalToWorld(new Vector3f(1, 0, 0));
        Assert.InRange(world.X, 11.99f, 12.01f);
        Assert.InRange(world.Y, 5.99f, 6.01f);
        Assert.InRange(world.Z, 5.99f, 6.01f);
        Assert.Equal(new Vector3f(1, 0, 0), child.WorldToLocal(world));
        _ = child.LocalToWorld(new Vector4f(1, 0, 0, 0));
        _ = child.WorldToLocal(new Vector4f(1, 0, 0, 0));
        _ = child.LocalToWorld2D(new Vector2f(1, 2));
        _ = child.WorldToLocal2D(new Vector2f(1, 2));

        child.Translate(new Vector3f(1, 0, 0), TransformSpace.Local);
        child.Translate(new Vector3f(0, 1, 0), TransformSpace.Parent);
        child.Translate(new Vector3f(0, 0, 1), TransformSpace.World);
        child.Translate2D(new Vector2f(1, 1), TransformSpace.Local);
        child.Rotate(new Quaternion(0.1f), TransformSpace.Local);
        child.Rotate(new Quaternion(0.1f), TransformSpace.Parent);
        child.Rotate(new Quaternion(0.1f), TransformSpace.World);
        child.Rotate2D(0.1f, TransformSpace.Local);
        child.Pitch(0.1f);
        child.Roll(0.1f);
        child.Yaw(0.1f);
        child.RotateAround(Vector3f.Zero, Quaternion.IDENTITY, TransformSpace.Local);
        child.RotateAround(Vector3f.Zero, Quaternion.IDENTITY, TransformSpace.Parent);
        child.RotateAround(Vector3f.Zero, Quaternion.IDENTITY, TransformSpace.World);
        child.RotateAround2D(Vector2f.Zero, 0.1f);
        child.ScaleBy(1.1f);
        child.ScaleBy(new Vector3f(1, 1, 1));
        child.ScaleBy2D(new Vector2f(1, 1));
        child.ScaleAround(Vector3f.Zero, Vector3f.One, TransformSpace.Local);
        child.ScaleAround(Vector3f.Zero, Vector3f.One, TransformSpace.Parent);
        child.ScaleAround(Vector3f.Zero, Vector3f.One, TransformSpace.World);
        child.ScaleAround2D(Vector2f.Zero, Vector2f.One);
        child.SetDirection(Vector3f.Forward);
        child.SetPositionSilent(new Vector3f(1, 2, 3));
        child.SetRotationSilent(Quaternion.IDENTITY);
        child.SetScaleSilent(Vector3f.One);
        child.SetPosition2D(new Vector2f(4, 5));
        child.SetPosition2D(6, 7);
        child.SetTransform2D(new Vector2f(1, 2), 0.1f);
        child.SetTransform2D(new Vector2f(1, 2), 0.1f, 2.0f);
        child.SetTransform2D(new Vector2f(1, 2), 0.1f, new Vector2f(2, 3));
        child.SetTransformMatrix(child.GetTransformMatrix());
        child.SetTransformMatrixSilent(child.GetTransformMatrix());

        Assert.False(child.LookAt(child.WorldPosition));
        Assert.True(child.LookAt(parent.WorldPosition + Vector3f.Forward));
        Assert.True(child.LookAt(new Vector3f(1, 1, 1), space: TransformSpace.Local));
        Assert.True(child.LookAt(new Vector3f(1, 1, 1), space: TransformSpace.Parent));
        child.SetWorldPosition(new Vector3f(20, 20, 20));
        child.SetWorldPosition2D(new Vector2f(3, 4));
        child.SetWorldPosition2D(5, 6);
        child.SetWorldRotation(Quaternion.IDENTITY);
        child.SetWorldRotation2D(0.2f);
        child.SetWorldScale(2.0f);
        child.SetWorldScale(new Vector3f(2, 3, 4));
        child.SetWorldScale2D(new Vector2f(2, 3));
        child.SetWorldScale2D(4, 5);
        child.SetWorldTransform(new Vector3f(1, 2, 3), Quaternion.IDENTITY);
        child.SetWorldTransform(new Vector3f(1, 2, 3), Quaternion.IDENTITY, 2.0f);
        child.SetWorldTransform(new Vector3f(1, 2, 3), Quaternion.IDENTITY, Vector3f.One);
        child.SetWorldTransform(child.GetWorldTransform());
        child.SetWorldTransform2D(new Vector2f(1, 2), 0.1f);
        child.SetWorldTransform2D(new Vector2f(1, 2), 0.1f, 2.0f);
        child.SetWorldTransform2D(new Vector2f(1, 2), 0.1f, new Vector2f(2, 3));
    }

    [Fact]
    public void Node_variables_listeners_traversal_and_reordering_cover_empty_paths()
    {
        using var scene = new Scene();
        var root = scene.CreateChild("Root");
        var first = root.CreateChild("First");
        var second = root.CreateChild("Second");
        var nested = first.CreateChild("Nested");
        using var component = new Component();
        first.AddComponent(component);

        root.SetVar("value", 1);
        root.SetVarByHash(new RedBolt.Container.StringHash("value"), 2);
        root.SetVarByHash(new RedBolt.Container.StringHash("missing"), 3);
        Assert.Equal(2, root.GetVar("value"));
        Assert.Equal(2, root.GetMutableVar("value"));
        root.SetVariables([
            new(new RedBolt.Container.StringHash("a"), "A"),
            new(new RedBolt.Container.StringHash("b"), "B")
        ]);
        Assert.Null(root.GetVar("value"));
        Assert.Equal("A", root.GetVar("a"));
        Assert.Equal(2, root.GetVars().Count);

        root.AddListener(component);
        root.AddListener(component);
        Assert.Single(root.GetListeners());
        root.MarkDirty();
        root.RemoveListener(component);
        root.RemoveListener(null);
        Assert.Empty(root.GetListeners());

        var visitedNodes = new List<Node>();
        var visitedComponents = new List<Component>();
        root.TraverseDepthFirst(node =>
        {
            visitedNodes.Add(node);
            return true;
        }, visitedComponents.Add);
        Assert.Equal(3, visitedNodes.Count);
        Assert.Same(first, visitedNodes[0]);
        Assert.Same(nested, visitedNodes[1]);
        Assert.Same(second, visitedNodes[2]);
        Assert.Single(visitedComponents);
        Assert.Same(component, visitedComponents[0]);

        root.ReorderChild(second, 0);
        Assert.Same(second, root.GetChild(0));
        root.ReorderChild(first, 99);
        root.AddChild(root);
        root.AddChild(root, 0);
        using var orphan = new Node();
        root.RemoveChild(orphan);
        root.RemoveChild(null!);
        Assert.Same(first, nested.Parent);
        Assert.Equal(0, root.GetChildIndex(second));
        Assert.Equal(1, first.GetIndexInParent());

        Assert.Equal(default, root.FindComponentAttribute("invalid"));
        Assert.Equal(default, root.FindComponentAttribute("@Missing/Value"));
        using var clone = root.Clone();
        Assert.NotNull(clone);
        using var standalone = new Node();
        Assert.Null(standalone.Clone());
        root.SetName(null!);
        Assert.Equal(string.Empty, root.Name);
        root.SetName("Root");
        root.SetWorldOriginUpdateMode(WorldOriginUpdateMode.Ignore);
        Assert.Equal(WorldOriginUpdateMode.Ignore, root.GetWorldOriginUpdateMode());
        Assert.Throws<ArgumentNullException>(() => root.SetTags(null!));
    }
}
