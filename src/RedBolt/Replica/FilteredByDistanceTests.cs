// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.Replica;
using RedBolt.Scene;

public sealed class FilteredByDistanceTests
{
    [Fact]
    public void FilteredByDistance_handles_object_hierarchies()
    {
        using var fixture = new ReplicaTestContext();
        var filteredPrefab = CreateFilteredTestPrefab();
        var unfilteredPrefab = CreateUnfilteredTestPrefab();

        using var serverScene = new Scene();
        using var clientScene = new Scene();
        var quality = new ConnectionQuality(0.08f, 0.12f, 0.20f, 0.02f, 0.02f);
        using var simulator = new NetworkSimulator(serverScene);
        simulator.AddClient(clientScene, quality);
        simulator.SimulateTime(5.0f);

        var clientNode = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverScene, filteredPrefab, "Client Node");
        clientNode.GetComponent<BehaviorNetworkObject>()!
            .SetOwner(simulator.GetServerToClientConnection(clientScene));
        clientNode.SetWorldPosition(Vector3f.Zero);

        var filteredParentNode = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverScene, filteredPrefab, "Filtered Parent Node");
        filteredParentNode.SetWorldPosition(Vector3f.Zero);

        var unfilteredChildNode = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            filteredParentNode, unfilteredPrefab, "Unfiltered Child Node");
        unfilteredChildNode.SetWorldPosition(new Vector3f(0.0f, 0.0f, 8.0f));

        simulator.SimulateTime(8.0f);

        clientNode = clientScene.GetChild("Client Node", true)!;
        filteredParentNode = clientScene.GetChild("Filtered Parent Node", true)!;
        unfilteredChildNode = clientScene.GetChild("Unfiltered Child Node", true)!;
        Assert.NotNull(clientNode);
        Assert.NotNull(filteredParentNode);
        Assert.NotNull(unfilteredChildNode);
        Assert.Same(clientScene, clientNode.Parent);
        Assert.Same(clientScene, filteredParentNode.Parent);
        Assert.Same(filteredParentNode, unfilteredChildNode.Parent);
        Assert.Equal(Vector3f.Zero, clientNode.GetWorldPosition());
        Assert.Equal(Vector3f.Zero, filteredParentNode.GetWorldPosition());
        Assert.Equal(new Vector3f(0.0f, 0.0f, 8.0f), unfilteredChildNode.GetWorldPosition());

        serverScene.GetChild("Filtered Parent Node", true)!
            .SetWorldPosition(new Vector3f(0.0f, 0.0f, -12.0f));
        simulator.SimulateTime(8.0f);

        Assert.NotNull(clientScene.GetChild("Client Node", true));
        Assert.Null(clientScene.GetChild("Filtered Parent Node", true));
        Assert.Null(clientScene.GetChild("Unfiltered Child Node", true));

        serverScene.GetChild("Filtered Parent Node", true)!
            .SetWorldPosition(new Vector3f(0.0f, 0.0f, -8.0f));
        simulator.SimulateTime(8.0f);

        clientNode = clientScene.GetChild("Client Node", true)!;
        filteredParentNode = clientScene.GetChild("Filtered Parent Node", true)!;
        unfilteredChildNode = clientScene.GetChild("Unfiltered Child Node", true)!;
        Assert.NotNull(clientNode);
        Assert.NotNull(filteredParentNode);
        Assert.NotNull(unfilteredChildNode);
        Assert.Same(clientScene, clientNode.Parent);
        Assert.Same(clientScene, filteredParentNode.Parent);
        Assert.Same(filteredParentNode, unfilteredChildNode.Parent);
        Assert.Equal(Vector3f.Zero, clientNode.GetWorldPosition());
        Assert.Equal(new Vector3f(0.0f, 0.0f, -8.0f), filteredParentNode.GetWorldPosition());
        Assert.Equal(Vector3f.Zero, unfilteredChildNode.GetWorldPosition());

        serverScene.GetChild("Unfiltered Child Node", true)!.Remove();
        simulator.SimulateTime(8.0f);

        Assert.NotNull(clientScene.GetChild("Client Node", true));
        Assert.NotNull(clientScene.GetChild("Filtered Parent Node", true));
        Assert.Null(clientScene.GetChild("Unfiltered Child Node", true));
    }

    private static PrefabResource CreateFilteredTestPrefab()
    {
        using var node = new Node();
        node.CreateComponent<ReplicatedTransform>();
        var filter = node.CreateComponent<FilteredByDistance>();
        filter.IsRelevant = false;
        filter.Distance = 10.0f;
        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@/FilteredByDistance/FilteredTest.prefab");
    }

    private static PrefabResource CreateUnfilteredTestPrefab()
    {
        using var node = new Node();
        node.CreateComponent<ReplicatedTransform>();
        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@/FilteredByDistance/UnfilteredTest.prefab");
    }
}
