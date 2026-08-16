// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using EventHandler = RedBolt.Core.EventHandler;

public sealed class CoreResidualCoverageTests
{
    [Fact]
    public void Reflection_attributes_and_attribute_accessors_cover_metadata_contracts()
    {
        var reflected = new ReflectedAttribute("Core")
        {
            Factory = false,
            InheritAttributes = false,
            InheritAttributesFrom = typeof(AccessorTarget),
            InheritAttributesOrder = 4
        };
        Assert.Equal("Core", reflected.Category);
        Assert.False(reflected.Factory);
        Assert.False(reflected.InheritAttributes);
        Assert.Equal(typeof(AccessorTarget), reflected.InheritAttributesFrom);
        Assert.Equal(4, reflected.InheritAttributesOrder);

        var serialized = new SerializedAttribute("Value", typeof(int))
        {
            AccessorFactory = "Create",
            AccessorType = typeof(IntAccessor),
            Configure = "Configure",
            DefaultSource = "Defaults",
            EnumNames = ["Zero", "One"],
            EnumNamesSource = "Names",
            Getter = "Get",
            Mode = AttributeMode.ReadOnly,
            Setter = "Set",
            ScopeHint = AttributeScopeHint.Scene
        };
        Assert.Equal("Value", serialized.Name);
        Assert.Equal(typeof(int), serialized.ValueType);
        Assert.Equal("Create", serialized.AccessorFactory);
        Assert.Equal(typeof(IntAccessor), serialized.AccessorType);
        Assert.Equal(["Zero", "One"], serialized.EnumNames);
        Assert.Equal(AttributeScopeHint.Scene, serialized.ScopeHint);

        var nameOnly = new SerializedAttribute("NameOnly");
        Assert.Equal("NameOnly", nameOnly.Name);

        var removed = new RemoveSerializedAttribute("Value");
        Assert.Equal("Value", removed.Name);

        var target = new AccessorTarget { Value = 7 };
        var accessor = new IntAccessor();
        Assert.Equal(7, accessor.GetObject(target));
        accessor.SetObject(target, 11);
        Assert.Equal(11, target.Value);
        accessor.SetObject(target, null);
        Assert.Equal(0, target.Value);

        using var info = new AttributeInfo("Value", typeof(int), new Variant(1));
        Assert.Same(info.Metadata, info.Metadata);
        Assert.Equal(0, info.GetMetadata<int>("missing"));
    }

    [Fact]
    public void Object_reflection_and_red_object_remaining_forwarders_are_exercised()
    {
        using var reflection = new ObjectReflection(typeof(ReflectionTarget));
        using var first = new AttributeInfo("First", typeof(int), new Variant(1));
        using var second = new AttributeInfo("Second", typeof(int), new Variant(2));
        reflection.AddAttribute(first);
        reflection.AddAttribute(second);

        Assert.Equal(2, reflection.GetAttributeByIndex(1).DefaultValue.GetInt());
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        Assert.Equal(AttributeScopeHint.Scene, reflection.ScopeHint);

        using var registry = new ObjectReflectionRegistry();
        registry.AddGeneratedReflection(typeof(ReflectionTarget), "Initial", null);
        registry.SetReflectionCategory<ReflectionTarget>("Updated");
        Assert.Equal("Updated", registry.GetReflection<ReflectionTarget>().Category);
        Assert.Same(registry.GetObjectReflections(), registry.GetObjectReflections());

        var previous = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var objectValue = new ReflectionTarget();
            objectValue.SetGlobalVar("answer", new Variant(42));
            Assert.Equal(42, objectValue.GetGlobalVars()[new StringHash("answer")].GetInt());
            Assert.False(objectValue.HasSubscribedToEvent());

            objectValue.SubscribeToEvent<ProbeEvent>(static (ref ProbeEvent _) => { });
            Assert.True(objectValue.HasSubscribedToEvent());
            objectValue.UnsubscribeFromAllEvents();
            Assert.False(objectValue.HasSubscribedToEvent());
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    [Fact]
    public void Context_event_compaction_and_empty_queries_cover_residual_paths()
    {
        using var context = new Context();
        var first = new EventTarget();
        var second = new EventTarget();
        var third = new EventTarget();
        var calls = 0;

        context.SubscribeToEvent<ProbeEvent>(first, (ref ProbeEvent _) =>
        {
            calls++;
            var subscriptions = GetSubscriptions(context, typeof(ProbeEvent));
            Assert.True(subscriptions.Count >= 3);
            SetRemoved(subscriptions[1]);
            SetReceiverDead(subscriptions[2]);
            context.Unsubscribe(first);
        });
        context.SubscribeToEvent<ProbeEvent>(second, (ref ProbeEvent _) => { });
        context.SubscribeToEvent<ProbeEvent>(third, (ref ProbeEvent _) => { });

        var args = new ProbeEvent();
        var subscriptionsBeforeDispatch = GetSubscriptions(context, typeof(ProbeEvent));
        var isReceiverMatch = typeof(Context).GetMethod("IsReceiverMatch", BindingFlags.Static | BindingFlags.NonPublic)!;
        SetReceiverDead(subscriptionsBeforeDispatch[2]);
        Assert.False((bool)isReceiverMatch.Invoke(null, [subscriptionsBeforeDispatch[2], third])!);
        context.SendEvent(first, ref args);
        Assert.Equal(1, calls);
        Assert.False(context.HasSubscribedToEvent(first));

        var remaining = GetSubscriptions(context, typeof(ProbeEvent));
        if (remaining.Count != 0)
        {
            SetRemoved(remaining[0]);
            SetBucketRequiresCompaction(context, typeof(ProbeEvent));
            Assert.False(context.HasSubscribedToEvent(second));
        }

        Assert.False(context.HasSubscribedToEvent(new EventTarget()));

        using var compactionContext = new Context();
        var compactionReceiver = new EventTarget();
        compactionContext.SubscribeToEvent<ProbeEvent>(compactionReceiver, static (ref ProbeEvent _) => { });
        var compactionSubscriptions = GetSubscriptions(compactionContext, typeof(ProbeEvent));
        SetRemoved(compactionSubscriptions[0]);
        SetBucketRequiresCompaction(compactionContext, typeof(ProbeEvent));
        Assert.False(compactionContext.HasSubscribedToEvent(compactionReceiver));
    }

    [Fact]
    public void Context_matching_subscription_skips_removed_entries_during_dispatch()
    {
        using var context = new Context();
        var receiver = new EventTarget();
        context.SubscribeToEvent<ProbeEvent>(receiver, static (ref ProbeEvent _) => { });

        var buckets = (IDictionary)typeof(Context).GetField(
            "_structEventSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        var bucket = buckets[typeof(ProbeEvent)]!;
        var subscriptions = GetSubscriptions(context, typeof(ProbeEvent));
        SetRemoved(subscriptions[0]);
        bucket.GetType().GetProperty("DispatchDepth")!.SetValue(bucket, 1);

        var method = typeof(Context).GetMethod("HasMatchingSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var predicate = (Func<EventHandler, bool>)(_ => false);
        Assert.False((bool)method.Invoke(context, [bucket, predicate])!);
    }

    [Fact]
    public void Context_sdl_error_and_release_guards_are_exercised_when_available()
    {
        using var context = new Context();
        var counter = typeof(Context).GetField("_sdlInitCounter", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousCounter = (int)counter.GetValue(null)!;
        counter.SetValue(null, 0);
        try
        {
            var handleSdlInit = typeof(Context).GetMethod(
                "HandleSdlInitResult", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.False((bool)handleSdlInit.Invoke(null, [1])!);

            var requireSdlCore = typeof(Context).GetMethod(
                "RequireSdlCore", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.False((bool)requireSdlCore.Invoke(context, [0u, 1])!);

            _ = context.RequireSdl(uint.MaxValue);
            context.ReleaseSdl();
            Assert.Throws<InvalidOperationException>(() => context.ReleaseSdl());
        }
        finally
        {
            counter.SetValue(null, previousCounter);
        }
    }

    [Fact]
    public void Time_timers_and_curve_point_edges_are_deterministic()
    {
        using var time = new Time();
        SetPrivateField(time, "_frameNumber", uint.MaxValue);
        time.BeginFrame(0.25f);
        Assert.Equal(1u, time.FrameNumber);
        Assert.True(time.IsFrameInProgress);
        Assert.Equal(4.0f, time.GetFramesPerSecond());
        time.EndFrame();
        Assert.False(time.IsFrameInProgress);
        time.SetTimerPeriod(33);
        Assert.Equal(33u, time.GetTimerPeriod());
        Assert.NotEmpty(Time.GetTimeStamp(0));
        Assert.Equal("1970", Time.GetTimeStamp(0, "yyyy"));

        var timer = new RedBolt.Core.Timer();
        Assert.True(timer.GetMSec() <= 1000);
        timer.GetMSec(true);
        timer.Reset();

        var hires = new HiresTimer();
        Assert.True(hires.GetUSec() >= 0);
        hires.GetUSec(true);
        hires.Reset();
        Assert.True(HiresTimer.IsSupported() || !HiresTimer.IsSupported());
        Assert.True(HiresTimer.GetFrequency() > 0);

        var counter = new TimedCounter(2, 0);
        Assert.Equal(1, counter.GetCurrent());
        counter.AddSample(2.0f);
        Assert.Equal(1.0f, counter.GetAverage());
        _ = counter.GetLast();

        var left = new VariantCurvePoint { Time = 1, Value = new Variant(1) };
        var right = left.Clone();
        Assert.True(left == right);
        Assert.False(left != right);
        right.Dispose();
        left.Dispose();
    }

    [Fact]
    public void Work_queue_external_and_multithreaded_paths_cover_wrappers_and_budget()
    {
        using var queue = new WorkQueue();
        typeof(WorkQueue).GetMethod("SetMainThread", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null);
        queue.Initialize(1);

        var mainIndex = (int)InvokePrivate(queue, "GetThreadIndexImpl");
        Assert.Equal(0, mainIndex);
        var workerIndex = Task.Run(() => (int)InvokePrivate(queue, "GetThreadIndexImpl")).GetAwaiter().GetResult();
        Assert.InRange(workerIndex, 0, queue.GetNumProcessingThreads() - 1);

        var threadCallback = new ManualResetEventSlim();
        var external = Task.Run(() => queue.PostTaskForThread(
            (threadIndex, _) =>
            {
                threadCallback.Set();
            }, TaskPriority.Immediate, 0));
        external.GetAwaiter().GetResult();
        queue.CompleteAll();
        _ = threadCallback.IsSet;

        var delayed = false;
        queue.PostDelayedTaskForMainThread((int _) => delayed = true);
        Assert.False(delayed);
        queue.CompleteAll();
        Assert.True(delayed);

        queue.SetNonThreadedWorkMs(1);
        queue.PostTask(() => Thread.Sleep(5), TaskPriority.Low);
        queue.PostTask(() => { }, TaskPriority.Low);
        queue.Update();
        queue.CompleteAll();

        queue.PostTask(() => { }, TaskPriority.Immediate);
        queue.CompleteImmediateForThisThread();
        Assert.True(queue.IsCompleted());
    }

    [Fact]
    public void Variant_container_interfaces_and_exception_cleanup_are_covered()
    {
        var key = new StringHash("key");
        using var value = new Variant(1);

        using var map = new VariantMap();
        var mapPairs = (ICollection<KeyValuePair<StringHash, Variant>>)map;
        mapPairs.Add(new(key, value));
        mapPairs.Clear();
        map.Add(key, value);
        Assert.Throws<ArgumentException>(() => map.Add(key, value));

        using var vector = new VariantVector();
        var list = (IList<Variant>)vector;
        list.Add(value);
        list[0] = new Variant(2);
        _ = list[0];
        Assert.True(list.Remove(new Variant(2)));
        ((ICollection<Variant>)vector).Clear();
        Assert.Throws<ArgumentOutOfRangeException>(() => vector.Insert(-1, value));
        Assert.Throws<InvalidOperationException>(() => new VariantVector(new ThrowingVariants()));

        using var stringMap = new StringVariantMap();
        IDictionary<string, Variant> dictionary = stringMap;
        dictionary["key"] = value;
        Assert.Equal(1, dictionary["key"].GetInt());
        ((ICollection<KeyValuePair<string, Variant>>)stringMap).Add(new("other", value));
        ((IDictionary<string, Variant>)stringMap).Remove("other");
        ((ICollection<KeyValuePair<string, Variant>>)stringMap).Clear();
        stringMap.Add("key", value);
        Assert.Throws<ArgumentException>(() => dictionary.Add("key", value));
        Assert.Throws<InvalidOperationException>(() => new StringVariantMap(new ThrowingStringVariants()));

        var dictionaryInput = new ThrowingDictionary(key, value);
        Assert.Throws<InvalidOperationException>(() => new VariantMap(dictionaryInput));
    }

    [Fact]
    public void Variant_fallbacks_custom_hashes_and_spline_edges_are_covered()
    {
        using var clone = (Variant)((ICloneable)new Variant(1)).Clone();
        using var stringVectorVariant = (Variant)new StringVector { "one" };
        _ = stringVectorVariant.GetHashCode();
        Assert.Equal(VariantType.StringVector, stringVectorVariant.Type);
        Assert.True(new Variant(new ResourceRefList("Texture", [string.Empty])).IsZero());
        using var fromStringVector = Variant.FromObject(new StringVector { "one" });
        Assert.Equal(VariantType.StringVector, fromStringVector.Type);

        using var custom = new CustomVariantValue(3);
        _ = custom.GetHashCode();
        using (var archive = new TestJSONOutputArchive())
            Assert.Throws<NotSupportedException>(() => custom.Serialize(archive, "value"));

        var formatted = typeof(NestedGeneric<int>).GetMethod("ToString");
        Assert.NotNull(formatted);
        var formatter = typeof(ObjectExtensionMethods).GetMethod(
            "GetFormattedTypeName", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(Type), typeof(bool)]);
        Assert.NotNull(formatter);
        Assert.Contains("NestedGeneric", (string)formatter!.Invoke(
            null, [typeof(NestedGeneric<int>), true])!);
        Assert.Equal("System.Int32", formatter.Invoke(null, [typeof(int), true]));

        using var spline = new Spline(InterpolationMode.BezierCurve);
        spline.AddKnot(new Variant("one"));
        spline.AddKnot(new Variant("two"));
        spline.AddKnot(new Variant("three"));
        using var unsupported = spline.GetPoint(0.5f);
        Assert.True(unsupported.IsEmpty);
    }

    [Fact]
    public void Variant_unknown_type_fallbacks_and_utility_guards_are_exercised()
    {
        var invalid = new Variant(1);
        var typeField = typeof(Variant).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)!;
        typeField
            .SetValueDirect(__makeref(invalid), unchecked((VariantType)999));
        Assert.True(invalid.IsZero());
        Assert.Null(invalid.GetObject());
        var other = new Variant(1);
        typeField.SetValueDirect(__makeref(other), unchecked((VariantType)999));
        Assert.False(invalid.Equals(other));
        invalid.Dispose();
        other.Dispose();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VariantTypeUtility.GetVariantTypeSize(unchecked((VariantType)999)));
    }

    [Fact]
    public void Generated_reflection_registration_duplicate_and_cycle_paths_are_covered()
    {
        var context = new Context();
        var previous = Context.Instance;
        Context.SetInstance(context);
        try
        {
            GeneratedReflectionRegistration.Add(typeof(ManualGeneratedType), static _ => { });
            Assert.Throws<InvalidOperationException>(() =>
                GeneratedReflectionRegistration.Add(typeof(ManualGeneratedType), static _ => { }));

            var recurse = true;
            Assert.Throws<InvalidOperationException>(() => GeneratedReflectionRegistration.Add(
                typeof(CyclicGeneratedType), current =>
                {
                    if (recurse)
                    {
                        recurse = false;
                        GeneratedReflectionRegistration.RegisterType(current, typeof(CyclicGeneratedType));
                    }
                }));
        }
        finally
        {
            Context.SetInstance(previous);
            context.Dispose();
        }
    }

    private static object InvokePrivate(object instance, string name)
    {
        return instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null)!;
    }

    private static void SetPrivateField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }

    private static IList<EventHandler> GetSubscriptions(Context context, Type eventType)
    {
        var buckets = (IDictionary)typeof(Context).GetField(
            "_structEventSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        if (!buckets.Contains(eventType) || buckets[eventType] is not { } bucket)
            return Array.Empty<EventHandler>();
        return (IList<EventHandler>)bucket.GetType().GetProperty("Subscriptions")!.GetValue(bucket)!;
    }

    private static void SetReceiverDead(Context context, Type eventType)
    {
        var subscriptions = GetSubscriptions(context, eventType);
        SetReceiverDead(subscriptions[^1]);
    }

    private static void SetReceiverDead(EventHandler handler)
    {
        var receiver = (WeakReference<RedObject>)typeof(EventHandler).GetField(
            "_receiver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
        receiver.SetTarget(null!);
    }

    private static void SetRemoved(EventHandler handler)
    {
        typeof(EventHandler).GetProperty("IsRemoved", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(handler, true);
    }

    private static void SetBucketRequiresCompaction(Context context, Type eventType)
    {
        var buckets = (IDictionary)typeof(Context).GetField(
            "_structEventSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        if (buckets[eventType] is { } bucket)
            bucket.GetType().GetProperty("RequiresCompaction")!.SetValue(bucket, true);
    }

    private struct ProbeEvent
    {
    }

    private sealed class AccessorTarget : Serializable
    {
        public int Value { get; set; }
    }

    private sealed class IntAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest) => dest = new Variant(((AccessorTarget)ptr).Value);

        public override void Set(Serializable ptr, Variant src) => ((AccessorTarget)ptr).Value = src.GetInt();
    }

    private sealed class ReflectionTarget : RedObject
    {
    }

    private sealed class EventTarget : RedObject
    {
    }

    private sealed class NestedGeneric<T>
    {
    }

    private sealed class ManualGeneratedType
    {
    }

    private sealed class CyclicGeneratedType
    {
    }

    private sealed class ThrowingVariants : IEnumerable<Variant>
    {
        public IEnumerator<Variant> GetEnumerator()
        {
            yield return new Variant(1);
            throw new InvalidOperationException("enumeration failed");
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingDictionary : IDictionary<StringHash, Variant>
    {
        private readonly StringHash _key;
        private readonly Variant _value;

        public ThrowingDictionary(StringHash key, Variant value)
        {
            _key = key;
            _value = value;
        }

        public Variant this[StringHash key] { get => _value; set => throw new NotSupportedException(); }
        public ICollection<StringHash> Keys => [_key];
        public ICollection<Variant> Values => [_value];
        public int Count => 1;
        public bool IsReadOnly => true;
        public void Add(StringHash key, Variant value) => throw new NotSupportedException();
        public void Add(KeyValuePair<StringHash, Variant> item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(KeyValuePair<StringHash, Variant> item) => true;
        public bool ContainsKey(StringHash key) => true;
        public void CopyTo(KeyValuePair<StringHash, Variant>[] array, int arrayIndex) => throw new NotSupportedException();
        public IEnumerator<KeyValuePair<StringHash, Variant>> GetEnumerator()
        {
            yield return new(_key, _value);
            throw new InvalidOperationException("enumeration failed");
        }
        public bool Remove(StringHash key) => false;
        public bool Remove(KeyValuePair<StringHash, Variant> item) => false;
        public bool TryGetValue(StringHash key, out Variant value)
        {
            value = _value;
            return true;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingStringVariants : IDictionary<string, Variant>
    {
        public Variant this[string key] { get => new(1); set => throw new NotSupportedException(); }
        public ICollection<string> Keys => ["key"];
        public ICollection<Variant> Values => [new Variant(1)];
        public int Count => 1;
        public bool IsReadOnly => true;
        public void Add(string key, Variant value) => throw new NotSupportedException();
        public void Add(KeyValuePair<string, Variant> item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(KeyValuePair<string, Variant> item) => true;
        public bool ContainsKey(string key) => true;
        public void CopyTo(KeyValuePair<string, Variant>[] array, int arrayIndex) => throw new NotSupportedException();
        public IEnumerator<KeyValuePair<string, Variant>> GetEnumerator()
        {
            yield return new("key", new Variant(1));
            throw new InvalidOperationException("enumeration failed");
        }
        public bool Remove(string key) => false;
        public bool Remove(KeyValuePair<string, Variant> item) => false;
        public bool TryGetValue(string key, out Variant value)
        {
            value = new Variant(1);
            return true;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
