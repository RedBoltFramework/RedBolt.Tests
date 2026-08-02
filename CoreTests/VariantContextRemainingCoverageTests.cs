namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class VariantContextRemainingCoverageTests
{
    [Fact]
    public void Variant_private_storage_fallbacks_and_null_constructors_are_covered()
    {
        Assert.Equal(string.Empty, ReadPrivateProperty<string>("StringValue"));
        Assert.Equal(CString.Empty, ReadPrivateProperty<CString>("CStringValue"));
        Assert.Empty(ReadPrivateProperty<byte[]>("BufferValue"));
        Assert.Empty(ReadPrivateProperty<VariantVector>("VariantVectorValue"));
        Assert.Empty(ReadPrivateProperty<StringVector>("StringVectorValue"));
        Assert.Empty(ReadPrivateProperty<VariantMap>("VariantMapValue"));
        Assert.Empty(ReadPrivateProperty<StringVariantMap>("StringVariantMapValue"));
        Assert.Empty(ReadPrivateProperty<VariantCurve>("VariantCurveValue").KeyFrames);
        Assert.Equal(string.Empty, ReadPrivateProperty<ResourceRef>("ResourceRefValue").Name);
        Assert.Same(CustomVariantValue.Empty, ReadPrivateProperty<CustomVariantValue>("CustomValue"));

        using var nullBuffer = new Variant((byte[])null!);
        using var nullVector = new Variant((VariantVector)null!);
        using var nullStrings = new Variant((StringVector)null!);
        using var nullMap = new Variant((VariantMap)null!);
        using var nullStringMap = new Variant((StringVariantMap)null!);
        using var nullCurve = new Variant((VariantCurve)null!);
        using var nullCString = new Variant((CString)null!);
        var nullString = new Variant((string)null!);

        Assert.Equal(VariantType.Buffer, nullBuffer.Type);
        Assert.Empty(nullBuffer.GetBuffer());
        Assert.Empty(nullVector.GetVariantVector());
        Assert.Empty(nullStrings.GetStringVector());
        Assert.Empty(nullMap.GetVariantMap());
        Assert.Empty(nullStringMap.GetStringVariantMap());
        Assert.Empty(nullCurve.GetVariantCurve().KeyFrames);
        Assert.Equal(VariantType.CString, nullCString.Type);
        Assert.Empty(nullCString.GetCString().ConvertToString());
        Assert.True(nullString.IsEmpty);
    }

    [Fact]
    public void Variant_numeric_and_reference_getters_cover_true_false_and_mismatch_paths()
    {
        using var trueValue = new Variant(true);
        using var falseValue = new Variant(false);
        Assert.Equal(1, trueValue.GetInt());
        Assert.Equal(0, falseValue.GetInt());
        Assert.Equal(1L, trueValue.GetInt64());
        Assert.Equal(0L, falseValue.GetInt64());
        Assert.Equal(1.0f, trueValue.GetFloat());
        Assert.Equal(0.0f, falseValue.GetFloat());
        Assert.Equal(1.0, trueValue.GetDouble());
        Assert.Equal(0.0, falseValue.GetDouble());

        using var vector2 = new Variant(new Vector2f(1, 2));
        using var doubleVector2 = new Variant(new Vector2d(3, 4));
        using var doubleVector3 = new Variant(new Vector3d(5, 6, 7));
        using var vector4 = new Variant(new Vector4f(8, 9, 10, 11));
        Assert.Equal(new Vector2f(1, 2), vector2.GetVector2());
        Assert.Equal(Vector2f.Zero, falseValue.GetVector2());
        Assert.Equal(new Vector2d(3, 4), doubleVector2.GetDoubleVector2());
        Assert.Equal(Vector2d.Zero, falseValue.GetDoubleVector2());
        Assert.Equal(new Vector3d(5, 6, 7), doubleVector3.GetDoubleVector3());
        Assert.Equal(Vector3d.Zero, falseValue.GetDoubleVector3());
        Assert.Equal(new Vector4f(8, 9, 10, 11), vector4.GetVector4());
        Assert.Equal(Vector4f.Zero, falseValue.GetVector4());

        using var cstring = new Variant(new CString("value"));
        using var pointer = new Variant(new IntPtr(7));
        using var resourceList = new Variant(new ResourceRefList("Texture", ["one"]));
        using var stringVector = new Variant(new StringVector { "one" });
        using var variantMap = new Variant(new VariantMap { [new StringHash("one")] = new(1) });
        using var stringMap = new Variant(new StringVariantMap { ["one"] = new(1) });
        using var curve = new Variant(new VariantCurve());

        Assert.Equal("value", cstring.GetCString().ConvertToString());
        Assert.Equal(CString.Empty, falseValue.GetCString());
        Assert.Equal(new IntPtr(7), pointer.GetVoidPtr());
        Assert.Equal(IntPtr.Zero, falseValue.GetVoidPtr());
        Assert.Equal(["one"], resourceList.GetResourceRefList().Names);
        Assert.Equal(ResourceRefList.Empty, falseValue.GetResourceRefList());
        Assert.Equal(["one"], stringVector.GetStringVector());
        Assert.Empty(falseValue.GetStringVector());
        Assert.Equal(1, variantMap.GetVariantMap()[new StringHash("one")].GetInt());
        Assert.Empty(falseValue.GetVariantMap());
        Assert.Equal(1, stringMap.GetStringVariantMap()["one"].GetInt());
        Assert.Empty(falseValue.GetStringVariantMap());
        Assert.Empty(curve.GetVariantCurve().KeyFrames);
        Assert.Empty(falseValue.GetVariantCurve().KeyFrames);
    }

    [Fact]
    public void Variant_owned_clones_and_container_equality_cover_remaining_switches()
    {
        using var buffer = new Variant([1, 2, 3]);
        using var bufferClone = buffer.Clone();
        using var cstring = new Variant(new CString("clone"));
        using var cstringClone = cstring.Clone();
        using var resourceList = new Variant(new ResourceRefList("Texture", ["one", "two"]));
        using var resourceListClone = resourceList.Clone();
        using var vector = new Variant(new VariantVector { new(1), new(2) });
        using var vectorClone = vector.Clone();
        using var stringVector = new Variant(new StringVector { "one", "two" });
        using var stringVectorClone = stringVector.Clone();
        using var variantMap = new VariantMap { [new StringHash("one")] = new(1) };
        using var map = new Variant(variantMap);
        using var mapClone = map.Clone();
        using var stringMap = new Variant(new StringVariantMap { ["one"] = new(1) });
        using var stringMapClone = stringMap.Clone();
        using var curve = new Variant(new VariantCurve());
        using var curveClone = curve.Clone();
        using var custom = Variant.MakeCustomValue(new CloneableValue(5));
        using var customClone = custom.Clone();

        Assert.Equal(buffer, bufferClone);
        Assert.Equal(cstring, cstringClone);
        Assert.Equal(resourceList, resourceListClone);
        Assert.Equal(vector, vectorClone);
        Assert.Equal(stringVector, stringVectorClone);
        Assert.Equal(map, mapClone);
        Assert.Equal(stringMap, stringMapClone);
        Assert.Equal(curve, curveClone);
        var customValue = Assert.IsType<CloneableValue>(custom.GetCustomVariantValue().Value);
        var customCloneValue = Assert.IsType<CloneableValue>(customClone.GetCustomVariantValue().Value);
        Assert.Equal(customValue.Value, customCloneValue.Value);
        Assert.NotSame(customValue, customCloneValue);

        using var sameMap = new Variant(new VariantMap { [new StringHash("one")] = new(1) });
        using var differentMap = new Variant(new VariantMap { [new StringHash("one")] = new(2) });
        using var missingMap = new Variant(new VariantMap { [new StringHash("two")] = new(1) });
        using var largerMap = new Variant(new VariantMap
        {
            [new StringHash("one")] = new(1),
            [new StringHash("two")] = new(2)
        });
        Assert.True(map.Equals(sameMap));
        Assert.False(map.Equals(differentMap));
        Assert.False(map.Equals(missingMap));
        Assert.False(map.Equals(largerMap));

        using var sameStringMap = new Variant(new StringVariantMap { ["one"] = new(1) });
        using var differentStringMap = new Variant(new StringVariantMap { ["one"] = new(2) });
        Assert.True(stringMap.Equals(sameStringMap));
        Assert.False(stringMap.Equals(differentStringMap));

        _ = custom.GetHashCode();
        _ = map.GetHashCode();
        _ = stringMap.GetHashCode();
        _ = vector.GetHashCode();
        _ = stringVector.GetHashCode();
        _ = buffer.GetHashCode();
    }

    [Fact]
    public void Variant_parsing_and_object_conversion_cover_null_and_uncommon_inputs()
    {
        var nullString = default(Variant);
        nullString.FromString(VariantType.String, null!);
        Assert.Equal(string.Empty, nullString.GetString());
        nullString.Dispose();

        var parser = default(Variant);
        parser.FromString(VariantType.Bool, " \tYes");
        Assert.True(parser.GetBool());
        parser.FromString(VariantType.Bool, " \tNo");
        Assert.False(parser.GetBool());
        parser.FromString(VariantType.Ptr, "ignored");
        Assert.True(parser.IsEmpty);
        parser.Dispose();

        using var source = new Variant(7);
        using var fromVariant = Variant.FromObject(source);
        Assert.Equal(7, fromVariant.GetInt());

        var objectMap = new Dictionary<StringHash, object?>
        {
            [new StringHash("number")] = 7,
            [new StringHash("text")] = "value"
        };
        using var fromObjectMap = Variant.FromObject(objectMap);
        Assert.Equal(7, fromObjectMap.GetVariantMap()[new StringHash("number")].GetInt());
        Assert.Equal("value", fromObjectMap.GetVariantMap()[new StringHash("text")].GetString());

        using var fromStrings = Variant.FromObject(Array.Empty<string>());
        Assert.Equal(VariantType.StringVector, fromStrings.Type);
        Assert.Empty(fromStrings.GetStringVector());

        using var enumValue = Variant.FromObject(TestEnum.Enabled);
        Assert.Equal(VariantType.Int, enumValue.Type);
        Assert.Equal(1, enumValue.GetInt());

        using var customValue = Variant.FromObject(new CustomVariantValue(11));
        Assert.Equal(VariantType.Custom, customValue.Type);
        Assert.Equal(11, customValue.GetCustomVariantValue().Value);

        using var unknownValue = Variant.FromObject(new object());
        Assert.Equal(VariantType.Custom, unknownValue.Type);
    }

    [Fact]
    public void Variant_type_utility_safe_paths_cover_clr_mapping_interpolation_and_json()
    {
        Assert.Equal(VariantType.Int, VariantTypeUtility.GetVariantType<StringHash>());
        Assert.Equal(VariantType.CString, VariantTypeUtility.GetVariantType(new CString("value")));
        Assert.Equal(VariantType.None, VariantTypeUtility.GetVariantType((object?)null));
        Assert.Equal(VariantType.None, VariantTypeUtility.GetVariantType(new object()));

        Assert.Equal(typeof(object), VariantTypeUtility.GetClrType(VariantType.None));
        Assert.Equal(typeof(object), VariantTypeUtility.GetClrType(VariantType.Ptr));
        Assert.Equal(typeof(object), VariantTypeUtility.GetClrType(VariantType.Custom));
        Assert.Equal(typeof(string), VariantTypeUtility.GetClrType(VariantType.CString));
        Assert.True(VariantTypeUtility.IsInterpolatable(VariantType.Rect));
        Assert.True(VariantTypeUtility.IsInterpolatable(VariantType.Vector3i));
        Assert.False(VariantTypeUtility.IsInterpolatable(VariantType.None));
        Assert.False(VariantTypeUtility.IsInterpolatable(VariantType.CString));

        using var intVariant = new Variant(23);
        Assert.Equal("23", VariantTypeUtility.Serialize(intVariant));
        Assert.Equal("23", VariantTypeUtility.Serialize((object)intVariant, VariantType.Int));
        Assert.Equal(23, Assert.IsType<int>(VariantTypeUtility.Deserialize("23", VariantType.Int)));
        using var deserializedInt = VariantTypeUtility.DeserializeVariant("23", VariantType.Int);
        Assert.Equal(23, deserializedInt.GetInt());

        using var cstring = new CString("native");
        Assert.Equal("\"native\"", VariantTypeUtility.Serialize(cstring, VariantType.CString));
        using var deserializedCString = Assert.IsType<CString>(VariantTypeUtility.Deserialize("\"native\"", VariantType.CString));
        Assert.Equal("native", deserializedCString.ConvertToString());
        using var deserializedCStringVariant = VariantTypeUtility.DeserializeVariant("\"native\"", VariantType.CString);
        Assert.Equal("native", deserializedCStringVariant.GetCString().ConvertToString());

        Assert.Throws<NotSupportedException>(() => VariantTypeUtility.GetVariantType<Guid>());
    }

    [Fact]
    public void Context_subsystem_lookup_covers_cached_polymorphic_and_removal_paths()
    {
        using var context = new Context();
        var stateManager = new DerivedStateManager();
        context.RegisterSubsystem(stateManager, new StringHash("StateManager"));

        Assert.Same(stateManager, context.GetSubsystem<StateManager>());
        Assert.Same(stateManager, context.GetSubsystem(new StringHash("StateManager")));
        Assert.Same(stateManager, context.GetSubsystem<DerivedStateManager>());

        context.RemoveSubsystem(new StringHash("Missing"));
        context.RemoveSubsystem(new StringHash("StateManager"));
        Assert.Same(stateManager, context.GetSubsystem<StateManager>());
        Assert.True(context.RemoveSubsystem<DerivedStateManager>());
        Assert.False(context.RemoveSubsystem<DerivedStateManager>());
        Assert.Null(context.GetSubsystem<DerivedStateManager>());
    }

    [Fact]
    public void Context_event_filters_update_and_compact_subscriptions()
    {
        using var context = new Context();
        var senderA = new TestObject();
        var senderB = new TestObject();
        var receiverA = new TestObject();
        var receiverB = new TestObject();
        var generalCalls = 0;
        var senderACalls = 0;
        var senderBCalls = 0;

        context.SubscribeToEvent<PingEvent>(receiverA, (ref PingEvent _) => ++generalCalls);
        context.SubscribeToEvent<PingEvent>(receiverA, (ref PingEvent _) => generalCalls += 10);
        context.SubscribeToEvent<PingEvent>(receiverA, senderA, (ref PingEvent _) => ++senderACalls);
        context.SubscribeToEvent<PingEvent>(receiverB, senderB, (ref PingEvent _) => ++senderBCalls);

        var args = new PingEvent();
        context.SendEvent(senderA, ref args);
        Assert.Equal(0, generalCalls);
        Assert.Equal(1, senderACalls);
        Assert.Equal(0, senderBCalls);

        context.SendEvent(senderB, ref args);
        Assert.Equal(10, generalCalls);
        Assert.Equal(1, senderACalls);
        Assert.Equal(1, senderBCalls);

        receiverB.BlockEvents = true;
        context.SendEvent(senderB, ref args);
        receiverB.BlockEvents = false;
        senderB.BlockEvents = true;
        context.SendEvent(senderB, ref args);
        senderB.BlockEvents = false;
        Assert.Equal(20, generalCalls);
        Assert.Equal(1, senderBCalls);

        context.Unsubscribe(receiverA, senderB);
        context.Unsubscribe(receiverA, senderA, typeof(PingEvent));
        context.Unsubscribe(receiverA, typeof(PingEvent));
        context.Unsubscribe(receiverB, typeof(PingEvent));
        context.Unsubscribe(receiverA, typeof(PongEvent));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiverA));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiverB, senderB));
        context.SendEvent(senderA, ref args);
    }

    [Fact]
    public void Context_sender_and_event_type_exceptions_cover_keep_and_remove_filters()
    {
        using var context = new Context();
        var senderA = new TestObject();
        var senderB = new TestObject();
        var receiver = new TestObject();

        context.SubscribeToEvent<PingEvent>(receiver, (ref PingEvent _) => { });
        context.SubscribeToEvent<PingEvent>(receiver, senderA, (ref PingEvent _) => { });
        context.SubscribeToEvent<PingEvent>(receiver, senderB, (ref PingEvent _) => { });
        context.SubscribeToEvent<PongEvent>(receiver, (ref PongEvent _) => { });

        context.UnsubscribeAllExcept(receiver, new[] { senderA });
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver, senderA));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver, senderB));
        Assert.False(context.HasSubscribedToEvent<PongEvent>(receiver));

        context.UnsubscribeAllExcept(receiver, new[] { typeof(PingEvent) });
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver, senderA));
        context.UnsubscribeAllExcept(receiver, Array.Empty<Type>());
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver));
        Assert.False(context.HasSubscribedToEvent<PongEvent>(receiver));
        context.RemoveEventSender(senderA);
        context.RemoveEventSender(senderB);
    }

    [Fact]
    public void Context_event_dispatch_supports_mutation_and_reports_active_invocation()
    {
        using var context = new Context();
        var sender = new TestObject();
        var receiver = new TestObject();
        var otherReceiver = new TestObject();
        var calls = 0;

        context.SubscribeToEvent<PingEvent>(receiver, (ref PingEvent _) =>
        {
            Assert.Same(sender, context.GetEventSender());
            Assert.NotNull(context.GetEventHandler());
            ++calls;
            context.Unsubscribe(receiver);
        });
        context.SubscribeToEvent<PingEvent>(otherReceiver, (ref PingEvent _) => ++calls);

        var args = new PingEvent();
        context.SendEvent(sender, ref args);
        Assert.Equal(2, calls);
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver));
        Assert.True(context.HasSubscribedToEvent<PingEvent>(otherReceiver));

        context.Unsubscribe(otherReceiver);
        Assert.Null(context.GetEventSender());
        Assert.Null(context.GetEventHandler());
    }

    [Fact]
    public void Context_weak_receivers_are_removed_both_during_dispatch_and_cleanup()
    {
        using var context = new Context();
        var removedOutsideDispatch = SubscribeAndRelease(context);
        CollectGarbage();
        context.Unsubscribe(new TestObject());
        Assert.False(removedOutsideDispatch.TryGetTarget(out _));

        var removedDuringDispatch = SubscribeAndRelease(context);
        CollectGarbage();
        var args = new GcEvent();
        context.SendEvent<GcEvent>(null, ref args);
        Assert.False(removedDuringDispatch.TryGetTarget(out _));
        Assert.False(context.HasSubscribedToEvent<GcEvent>(new TestObject()));
    }

    [Fact]
    public void Context_reset_disposes_all_named_optional_subsystems()
    {
        using var context = new Context();
        var disposed = 0;

        RegisterDisposable(context, "VirtualReality", () => ++disposed);
        RegisterDisposable(context, "PluginManager", () => ++disposed);
        RegisterDisposable(context, "Audio", () => ++disposed);
        RegisterDisposable(context, "DearImGui", () => ++disposed);
        RegisterDisposable(context, "Input", () => ++disposed);
        RegisterDisposable(context, "Renderer", () => ++disposed);
        RegisterDisposable(context, "RenderDevice", () => ++disposed);
        RegisterDisposable(context, "Graphics", () => ++disposed);
        RegisterDisposable(context, "StateManager", () => ++disposed);
        RegisterDisposable(context, "Other", () => ++disposed);

        context.Reset();

        Assert.Equal(10, disposed);
        Assert.Empty(context.GetSubsystems().GetContainer());
    }

    [Fact]
    public void Context_sdl_reference_count_handles_nested_zero_flag_acquisitions()
    {
        using var context = new Context();
        Assert.True(context.RequireSdl(0));
        Assert.True(context.RequireSdl(0));
        context.ReleaseSdl();
        context.ReleaseSdl();
    }

    private static T ReadPrivateProperty<T>(string name)
    {
        var property = typeof(Variant).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<T>(property!.GetValue(default(Variant)));
    }

    private static WeakReference<TestObject> SubscribeAndRelease(Context context)
    {
        var receiver = new TestObject();
        context.SubscribeToEvent<GcEvent>(receiver, static (ref GcEvent _) => { });
        return new WeakReference<TestObject>(receiver);
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void RegisterDisposable(Context context, string name, Action onDispose)
    {
        context.RegisterSubsystem(new TrackedSubsystem(onDispose), new StringHash(name));
    }

    private enum TestEnum
    {
        Disabled,
        Enabled
    }

    private sealed class CloneableValue(int value) : ICloneable
    {
        public int Value { get; } = value;

        public object Clone() => new CloneableValue(Value);
    }

    private sealed class DerivedStateManager : StateManager
    {
    }

    private sealed class TrackedSubsystem(Action onDispose) : RedObject
    {
        public override void Dispose()
        {
            onDispose();
            base.Dispose();
        }
    }

    private sealed class TestObject : RedObject
    {
    }

    private struct PingEvent;

    private struct PongEvent;

    private struct GcEvent;
}
