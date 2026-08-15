//
// Copyright (c) 2021 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Particles;
using RedBolt.Particles.ParticleGraphNodes;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class ParticleGraphBehaviorTests
{
    private static readonly string[] RegisteredNodeTypes =
    [
        "Add", "Bounce", "BurstTimer", "Cone", "Constant", "Curve", "Destroy", "Divide", "Emit", "Expire",
        "GetAttribute", "GetUniform", "Lerp", "Make", "Move", "Multiply", "Negate", "Print", "Random",
        "RenderBillboard", "SetAttribute", "SetUniform", "Slerp", "Subtract", "TimeStep", "TimeStepScale",
        "EffectTime", "NormalizedEffectTime", "RenderMesh", "Sphere", "Hemisphere", "LimitVelocity",
        "ApplyForce", "Normalized", "Length", "Break", "Circle", "Box", "Cast", "Noise3D", "CurlNoise3D"
    ];

    [Fact]
    public void ParticleGraphSerializationRoundTrips()
    {
        using var scope = new ParticleGraphTestScope();
        using var inputGraph = new Graph();
        using var one = new Variant(1.0f);
        using var half = new Variant(0.5f);
        var constant = inputGraph.Create("Constant").WithProperty("Value", one).WithOutput("out", VariantType.Float);
        var add = inputGraph.Create("Add")
            .WithInput("x", half)
            .WithInput("y", constant.GetOutput("out"), VariantType.Float)
            .WithOutput("out", VariantType.Float);
        inputGraph.Create("SetAttribute")
            .WithInput("", add.GetOutput("out"), VariantType.Float)
            .WithOutput("attr", VariantType.Float);
        inputGraph.Create("GetAttribute").WithOutput("attr1", VariantType.Float);

        using var source = new ParticleGraph();
        Assert.True(source.LoadGraph(inputGraph), "Load source graph");
        using var serialized = new Graph();
        Assert.True(source.SaveGraph(serialized), "Save source graph");
        using var restored = new ParticleGraph();
        Assert.True(restored.LoadGraph(serialized), "Load serialized graph");
        using var serializedAgain = new Graph();
        Assert.True(restored.SaveGraph(serializedAgain), "Save restored graph");

        Assert.Equal(serialized.GetNumNodes(), serializedAgain.GetNumNodes());
        Assert.Equal(source.NumNodes, restored.NumNodes);
    }

    [Fact]
    public void SimpleParticleGraphInitializesVectorAttribute()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        var constantIndex = AddConstant(layer.InitGraph, new Vector3f(1, 2, 3));
        AddSetAttribute(layer.InitGraph, "pos", VariantType.Vector3, constantIndex);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles());

        Assert.Equal(new Vector3f(1, 2, 3), instance.GetAttributeValues<Vector3f>(0)[0]);
    }

    [Fact]
    public void ConstantNodeInitializesAttribute()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        var constantIndex = AddConstant(layer.InitGraph, new Vector3f(1, 2, 3));
        AddSetAttribute(layer.InitGraph, "pos", VariantType.Vector3, constantIndex);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles());

        Assert.Equal(1, instance.NumAttributes);
        Assert.Equal(new Vector3f(1, 2, 3), instance.GetAttributeValues<Vector3f>(0)[0]);
    }

    [Fact]
    public void EmitNodeCreatesParticle()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        var countIndex = AddConstant(layer.EmitGraph, 1.0f);
        var emit = new Emit();
        Assert.True(emit.SetPinSource(0, countIndex));
        layer.EmitGraph.Add(emit);

        using var instance = CreateInstance(layer);
        instance.Update(0.1f, true);

        Assert.Equal(1, instance.NumActiveParticles);
    }

    [Fact]
    public void BurstNodeWaitsForDelay()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        var countIndex = AddConstant(layer.EmitGraph, 1.0f);
        var burst = new BurstTimer { Delay = 0.15f, Interval = 1.0f, Cycles = 2 };
        Assert.True(burst.SetPinSource(0, countIndex));
        var burstIndex = layer.EmitGraph.Add(burst);
        var emit = new Emit();
        Assert.True(emit.SetPinSource(0, burstIndex, 1));
        layer.EmitGraph.Add(emit);

        using var instance = CreateInstance(layer);
        instance.Update(0.1f, true);
        Assert.Equal(0, instance.NumActiveParticles);
        instance.Update(0.1f, true);
        Assert.Equal(1, instance.NumActiveParticles);
    }

    [Fact]
    public void ExpireNodeRemovesAllMatchingParticles()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        var timeIndex = AddConstant(layer.UpdateGraph, 1.0f);
        var lifetimeIndex = AddConstant(layer.UpdateGraph, 1.0f);
        var expire = new Expire();
        Assert.True(expire.SetPinSource(0, timeIndex));
        Assert.True(expire.SetPinSource(1, lifetimeIndex));
        layer.UpdateGraph.Add(expire);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles(3));
        instance.Update(0.1f, false);

        Assert.Equal(0, instance.NumActiveParticles);
    }

    [Fact]
    public void MakeNodeCreatesVector2()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        using var x = new Variant(2.0f);
        using var y = new Variant(3.0f);
        LoadMakeGraph(layer.InitGraph, x, y, VariantType.Vector2);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles());

        Assert.Equal(new Vector2f(2, 3), instance.GetAttributeValues<Vector2f>(0)[0]);
    }

    [Fact]
    public void MakeNodeCreatesIntVector2()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 10 };
        using var x = new Variant(2);
        using var y = new Variant(3);
        LoadMakeGraph(layer.InitGraph, x, y, VariantType.Vector2i);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles());

        Assert.Equal(new Vector2i(2, 3), instance.GetAttributeValues<Vector2i>(0)[0]);
    }

    [Fact]
    public void ParticlesAssemblyHasNoPhysicsJoltDependency()
    {
        var references = typeof(ParticleGraphEffect).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "RedBolt.PhysicsJolt");
    }

    [Fact]
    public void PhysicsJoltAssemblyHasNoParticlesDependency()
    {
        var references = typeof(RedBolt.PhysicsJolt.Physics).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "RedBolt.Particles");
        Assert.Contains(typeof(IPhysicsWorld3D), typeof(RedBolt.PhysicsJolt.PhysicsWorld).GetInterfaces());
    }

    [Fact]
    public void BounceLivesInParticlesAssembly()
    {
        using var scope = new ParticleGraphTestScope();
        using var bounce = scope.System.CreateObject(new StringHash("Bounce"));

        Assert.IsType<Bounce>(bounce);
        Assert.Same(typeof(ParticleGraphEffect).Assembly, typeof(Bounce).Assembly);
    }

    [Fact]
    public void ParticleGraphLibraryRegistersExactSourceNodeSetAndReflectionDefaults()
    {
        using var scope = new ParticleGraphTestScope();

        foreach (var typeName in RegisteredNodeTypes)
        {
            using var node = scope.System.CreateObject(new StringHash(typeName));
            Assert.NotNull(node);
            Assert.Equal(typeName, node.GetTypeName());
        }

        foreach (var typeName in new[]
                 {
                     "Clamp", "EmitterPosition", "EmitterRotation", "EmitterScale", "LocalToWorld", "WorldToLocal"
                 })
            Assert.Null(scope.System.CreateObject(new StringHash(typeName)));

        Assert.Equal(ObjectCategory.Category_Geometry,
            Context.Instance.GetReflection<ParticleGraphEmitter>().Category);
        Assert.Equal(16, Context.Instance.GetReflection<ParticleGraphLayer>()
            .GetAttribute("Capacity").DefaultValue.GetInt());
        Assert.Equal(1.0f, Context.Instance.GetReflection<ParticleGraphLayer>()
            .GetAttribute("TimeScale").DefaultValue.GetFloat());
        Assert.Equal(1.0f, Context.Instance.GetReflection<ParticleGraphLayer>()
            .GetAttribute("Duration").DefaultValue.GetFloat());
    }

    [Fact]
    public void CapacityFractionalEmissionResetAndMultipleLayersWork()
    {
        using var scope = new ParticleGraphTestScope();
        using var effect = new ParticleGraphEffect();
        effect.SetNumLayers(2);
        effect.GetLayer(0).Capacity = 2;
        effect.GetLayer(1).Capacity = 3;
        AddSetAttribute(effect.GetLayer(0).InitGraph, "value", VariantType.Int,
            AddConstant(effect.GetLayer(0).InitGraph, 10));
        AddSetAttribute(effect.GetLayer(1).InitGraph, "value", VariantType.Int,
            AddConstant(effect.GetLayer(1).InitGraph, 20));
        using var emitter = new ParticleGraphEmitter();
        emitter.SetEffect(effect);

        var first = emitter.GetLayer(0)!;
        var second = emitter.GetLayer(1)!;
        Assert.True(first.EmitNewParticles(0.4f));
        Assert.True(first.EmitNewParticles(0.4f));
        Assert.Equal(0, first.NumActiveParticles);
        Assert.True(first.EmitNewParticles(0.3f));
        Assert.Equal(1, first.NumActiveParticles);
        Assert.True(first.EmitNewParticles(10.0f));
        Assert.Equal(2, first.NumActiveParticles);
        Assert.False(first.EmitNewParticles());
        Assert.True(second.EmitNewParticles(2.0f));
        Assert.Equal(10, first.GetAttributeValues<int>(0)[0]);
        Assert.Equal(20, second.GetAttributeValues<int>(0)[0]);

        emitter.Reset();
        Assert.False(emitter.CheckActiveParticles());
        Assert.True(first.EmitNewParticles());
        Assert.Equal(1, first.NumActiveParticles);
    }

    [Fact]
    public void EffectReloadRecommitsLayersAndDropsExistingParticles()
    {
        using var scope = new ParticleGraphTestScope();
        using var effect = new ParticleGraphEffect();
        effect.SetNumLayers(1);
        var definition = effect.GetLayer(0);
        definition.Capacity = 1;
        AddSetAttribute(definition.InitGraph, "value", VariantType.Int,
            AddConstant(definition.InitGraph, 7));
        using var emitter = new ParticleGraphEmitter();
        emitter.SetEffect(effect);
        Assert.True(emitter.GetLayer(0)!.EmitNewParticles());

        definition.Capacity = 3;
        effect.SendEvent<ResourceEvents.ReloadFinished>();

        var reloaded = emitter.GetLayer(0)!;
        Assert.Equal(0, reloaded.NumActiveParticles);
        Assert.True(reloaded.EmitNewParticles(3.0f));
        Assert.Equal(3, reloaded.NumActiveParticles);
        Assert.Equal(7, reloaded.GetAttributeValues<int>(0)[2]);
    }

    [Fact]
    public void SceneAttachmentAndEnableStateDriveEmitterUpdates()
    {
        using var scope = new ParticleGraphTestScope();
        using var effect = new ParticleGraphEffect();
        effect.SetNumLayers(1);
        effect.GetLayer(0).Capacity = 4;
        var count = AddConstant(effect.GetLayer(0).EmitGraph, 1.0f);
        var emit = new Emit();
        Assert.True(emit.SetPinSource(0, count));
        effect.GetLayer(0).EmitGraph.Add(emit);
        using var scene = new Scene();
        var emitter = scene.CreateChild("Emitter").CreateComponent<ParticleGraphEmitter>();
        emitter.SetEffect(effect);

        var postUpdate = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = 0.1f };
        scene.SendEvent(ref postUpdate);
        Assert.Equal(1, emitter.GetLayer(0)!.NumActiveParticles);

        emitter.SetEnabled(false);
        scene.SendEvent(ref postUpdate);
        Assert.Equal(1, emitter.GetLayer(0)!.NumActiveParticles);

        emitter.SetEnabled(true);
        scene.SendEvent(ref postUpdate);
        Assert.Equal(2, emitter.GetLayer(0)!.NumActiveParticles);
    }

    [Fact]
    public void MotionTimeUniformShapeRandomAndNoiseNodesProduceValues()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 2, Duration = 2.0f };
        AddSetAttribute(layer.InitGraph, "position", VariantType.Vector3,
            AddConstant(layer.InitGraph, new Vector3f(1, 2, 3)));
        AddSetAttribute(layer.InitGraph, "velocity", VariantType.Vector3,
            AddConstant(layer.InitGraph, new Vector3f(2, 0, 0)));

        var sphere = new RedBolt.Particles.ParticleGraphNodes.Sphere
        {
            Radius = 2.0f,
            From = (int)EmitFrom.Surface,
            Scale = Vector3f.One,
            Rotation = new Quaternion()
        };
        var sphereIndex = layer.InitGraph.Add(sphere);
        AddSetAttribute(layer.InitGraph, "spherePosition", VariantType.Vector3, sphereIndex, 0);
        AddSetAttribute(layer.InitGraph, "sphereVelocity", VariantType.Vector3, sphereIndex, 1);

        LoadRandomGraph(layer.InitGraph, 4.0f, "random");

        var noiseInput = AddConstant(layer.InitGraph, new Vector3f(0.25f, 0.5f, 0.75f));
        var noise = new Noise3D();
        Assert.True(noise.SetPinSource(0, noiseInput), "Connect Noise3D input");
        var noiseIndex = layer.InitGraph.Add(noise);
        AddSetAttribute(layer.InitGraph, "noise", VariantType.Float, noiseIndex, 1);

        var getPosition = AddGetAttribute(layer.UpdateGraph, "position", VariantType.Vector3);
        var getVelocity = AddGetAttribute(layer.UpdateGraph, "velocity", VariantType.Vector3);
        var move = new Move();
        Assert.True(move.SetPinSource(0, getPosition), "Connect Move position");
        Assert.True(move.SetPinSource(1, getVelocity), "Connect Move velocity");
        var moveIndex = layer.UpdateGraph.Add(move);
        AddSetAttribute(layer.UpdateGraph, "position", VariantType.Vector3, moveIndex, 2);

        var uniformValue = AddConstant(layer.UpdateGraph, 9.0f);
        var setUniform = new SetUniform();
        setUniform.SetUniformName("shared");
        setUniform.SetUniformType(VariantType.Float);
        Assert.True(setUniform.SetPinSource(1, uniformValue), "Connect SetUniform value");
        layer.UpdateGraph.Add(setUniform);
        var getUniform = new GetUniform();
        getUniform.SetUniformName("shared");
        getUniform.SetUniformType(VariantType.Float);
        var getUniformIndex = layer.UpdateGraph.Add(getUniform);
        AddSetAttribute(layer.UpdateGraph, "uniform", VariantType.Float, getUniformIndex);

        var effectTime = layer.UpdateGraph.Add(new EffectTime());
        AddSetAttribute(layer.UpdateGraph, "effectTime", VariantType.Float, effectTime);
        var normalizedTime = layer.UpdateGraph.Add(new NormalizedEffectTime());
        AddSetAttribute(layer.UpdateGraph, "normalizedTime", VariantType.Float, normalizedTime);

        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles(), "Emit category-coverage particle");
        Assert.InRange(instance.GetAttributeValues<Vector3f>(2)[0].Length, 1.9999f, 2.0001f);
        Assert.InRange(instance.GetAttributeValues<Vector3f>(3)[0].Length, 0.9999f, 1.0001f);
        Assert.Equal(4.0f, instance.GetAttributeValues<float>(4)[0]);
        Assert.InRange(instance.GetAttributeValues<float>(5)[0], -1.0f, 1.0f);

        instance.Update(0.5f, false);
        instance.Update(0.5f, false);

        Assert.Equal(new Vector3f(3, 2, 3), instance.GetAttributeValues<Vector3f>(0)[0]);
        Assert.Equal(9.0f, instance.GetAttributeValues<float>(6)[0]);
        Assert.Equal(0.5f, instance.GetAttributeValues<float>(7)[0]);
        Assert.Equal(0.25f, instance.GetAttributeValues<float>(8)[0]);
    }

    [Fact]
    public void SteadyStateMotionUpdateDoesNotAllocate()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayer { Capacity = 1 };
        AddSetAttribute(layer.InitGraph, "position", VariantType.Vector3,
            AddConstant(layer.InitGraph, Vector3f.Zero));
        AddSetAttribute(layer.InitGraph, "velocity", VariantType.Vector3,
            AddConstant(layer.InitGraph, Vector3f.One));
        var getPosition = AddGetAttribute(layer.UpdateGraph, "position", VariantType.Vector3);
        var getVelocity = AddGetAttribute(layer.UpdateGraph, "velocity", VariantType.Vector3);
        var move = new Move();
        Assert.True(move.SetPinSource(0, getPosition));
        Assert.True(move.SetPinSource(1, getVelocity));
        var moveIndex = layer.UpdateGraph.Add(move);
        AddSetAttribute(layer.UpdateGraph, "position", VariantType.Vector3, moveIndex, 2);
        using var instance = CreateInstance(layer);
        Assert.True(instance.EmitNewParticles());
        for (var i = 0; i < 100; ++i)
            instance.Update(1.0f / 60.0f, false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; ++i)
            instance.Update(1.0f / 60.0f, false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void BounceCoversMissingPhysicsWorldFallback()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayerInstance();
        using var bounce = new Bounce();
        using var instance = (BounceInstance)bounce.CreateInstance(layer);
        using var node = new Node();
        var context = new UpdateContext { TimeStep = 0.5f };
        var position = new Vector3f(1, 2, 3);
        var velocity = new Vector3f(2, 0, 0);

        instance.RayCastAndBounce(in context, node, null, ref position, ref velocity);
        Assert.Equal(new Vector3f(2, 2, 3), position);
    }

    [Fact]
    public void BounceUsesBackendIndependentPhysicsQueries()
    {
        using var scope = new ParticleGraphTestScope();
        using var layer = new ParticleGraphLayerInstance();
        using var bounce = new Bounce { BounceFactor = 0.5f, Dampen = 0.2f };
        using var instance = (BounceInstance)bounce.CreateInstance(layer);
        using var node = new Node();
        var context = new UpdateContext { TimeStep = 0.5f };
        var physics = new TestPhysicsWorld3D
        {
            Result = new PhysicsSphereCastResult
            {
                HasHit = true,
                Position = new Vector3f(0, 0.2f, 0),
                Normal = Vector3f.Up
            }
        };
        var position = new Vector3f(0, 1, 0);
        var velocity = new Vector3f(0, -2, 0);

        instance.RayCastAndBounce(in context, node, physics, ref position, ref velocity);

        Assert.Equal(0.0f, position.X);
        Assert.InRange(position.Y, 0.20799f, 0.20801f);
        Assert.Equal(0.0f, position.Z);
        Assert.Equal(new Vector3f(0, 0.8f, 0), velocity);

        physics.Gravity = new Vector3f(0, -2, 0);
        physics.Result = default;
        position = Vector3f.Zero;
        velocity = new Vector3f(2, 0, 0);

        instance.RayCastAndBounce(in context, node, physics, ref position, ref velocity);

        Assert.Equal(new Vector3f(1, -0.5f, 0), position);
        Assert.Equal(new Vector3f(2, -1, 0), velocity);
    }

    private static ParticleGraphLayerInstance CreateInstance(ParticleGraphLayer layer)
    {
        var instance = new ParticleGraphLayerInstance();
        instance.Apply(layer);
        return instance;
    }

    private static int AddConstant(ParticleGraph graph, float value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, int value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Vector3f value)
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

    private static void AddSetAttribute(ParticleGraph graph, string name, VariantType type,
        int sourceNode, int sourcePin = 0)
    {
        var setAttribute = new SetAttribute();
        setAttribute.SetAttributeName(name);
        setAttribute.SetAttributeType(type);
        Assert.True(setAttribute.SetPinSource(1, sourceNode, sourcePin));
        graph.Add(setAttribute);
    }

    private static int AddGetAttribute(ParticleGraph graph, string name, VariantType type)
    {
        var getAttribute = new GetAttribute();
        getAttribute.SetAttributeName(name);
        getAttribute.SetAttributeType(type);
        return graph.Add(getAttribute);
    }

    private static void LoadMakeGraph(ParticleGraph graph, in Variant x, in Variant y, VariantType outputType)
    {
        using var serialized = new Graph();
        var make = serialized.Create("Make")
            .WithInput("x", x)
            .WithInput("y", y)
            .WithOutput("out", outputType);
        serialized.Create("SetAttribute")
            .WithInput("", make.GetOutput("out"), outputType)
            .WithOutput("attr", outputType);

        Assert.True(graph.LoadGraph(serialized));
    }

    private static void LoadRandomGraph(ParticleGraph graph, float value, string attributeName)
    {
        using var serialized = new Graph();
        using var limit = new Variant(value);
        var random = serialized.Create("Random")
            .WithProperty("Min", limit)
            .WithProperty("Max", limit)
            .WithOutput("out", VariantType.Float);
        serialized.Create("SetAttribute")
            .WithInput("", random.GetOutput("out"), VariantType.Float)
            .WithOutput(attributeName, VariantType.Float);

        Assert.True(graph.LoadGraph(serialized));
    }

    private sealed class ParticleGraphTestScope : IDisposable
    {
        public ParticleGraphTestScope()
        {
            _previousContext = Context.Instance;
            Context.SetInstance(new Context());
            _context = Context.Instance;
            _system = new ParticleGraphSystem();
            _context.RegisterSubsystem(_system);
        }

        public void Dispose()
        {
            _context.Dispose();
            Context.SetInstance(_previousContext);
        }

        private readonly Context _previousContext;
        private readonly Context _context;
        private readonly ParticleGraphSystem _system;

        public ParticleGraphSystem System => _system;
    }

    private sealed class TestPhysicsWorld3D : IPhysicsWorld3D
    {
        public Vector3f Gravity { get; set; }

        public bool UpdateEnabled { get; set; }

        public bool ManualUpdate { get; set; }

        public bool Interpolation { get; set; }

        public int Fps { get; set; } = 60;

        public PhysicsSphereCastResult Result { get; set; }

        public void Update(float timeStep)
        {
        }

        public void CustomUpdate(uint numSteps, float fixedTimeStep, float overtime,
            long? networkFrame = null, uint synchronizedStepOffset = 0)
        {
        }

        public void SphereCast(ref PhysicsSphereCastResult result, Ray ray, float radius, float maxDistance)
        {
            result = Result;
        }
    }
}
