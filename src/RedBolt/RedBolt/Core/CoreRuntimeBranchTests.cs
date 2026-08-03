namespace RedBolt.Tests;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Platform;
using RedBolt.RenderAPI;
using RedBolt.Scene;

public sealed class CoreRuntimeBranchTests
{
    [Fact]
    public void Delegate_attribute_accessors_cover_getter_setter_and_numeric_conversions()
    {
        var target = new AccessorTarget();

        using var enumAttribute = new AttributeInfo(
            "Mode", typeof(TestMode), getter: static value => ((AccessorTarget)value).Mode,
            setter: static (value, source) => ((AccessorTarget)value).Mode = (TestMode)source!);
        using var uintAttribute = new AttributeInfo(
            "Unsigned", typeof(uint), setter: static (value, source) => ((AccessorTarget)value).Unsigned = (uint)source!);
        using var ulongAttribute = new AttributeInfo(
            "LargeUnsigned", typeof(ulong), setter: static (value, source) => ((AccessorTarget)value).LargeUnsigned = (ulong)source!);
        using var objectAttribute = new AttributeInfo(
            "Text", typeof(string), getter: static value => ((AccessorTarget)value).Text,
            setter: static (value, source) => ((AccessorTarget)value).Text = (string?)source);
        using var getterOnly = new AttributeInfo("GetterOnly", typeof(string), getter: static _ => "read-only");
        using var setterOnly = new AttributeInfo("SetterOnly", typeof(int), setter: static (value, source) =>
            ((AccessorTarget)value).Number = (int)source!);
        var enumAccessor = enumAttribute.Accessor!;
        var uintAccessor = uintAttribute.Accessor!;
        var ulongAccessor = ulongAttribute.Accessor!;
        var objectAccessor = objectAttribute.Accessor!;
        var getterAccessor = getterOnly.Accessor!;
        var setterAccessor = setterOnly.Accessor!;

        using var mode = new Variant(1);
        enumAccessor.Set(target, mode);
        using var unsigned = new Variant(7u);
        uintAccessor.Set(target, unsigned);
        using var largeUnsigned = new Variant(9ul);
        ulongAccessor.Set(target, largeUnsigned);
        using var number = new Variant(11);
        setterAccessor.Set(target, number);
        objectAccessor.SetObject(target, "changed");

        Assert.Equal(TestMode.Enabled, target.Mode);
        Assert.Equal(7u, target.Unsigned);
        Assert.Equal(9ul, target.LargeUnsigned);
        Assert.Equal(11, target.Number);
        Assert.Equal("changed", target.Text);
        Assert.Equal(TestMode.Enabled, enumAccessor.GetObject(target));
        Assert.Equal("changed", objectAccessor.GetObject(target));
        Assert.Equal("read-only", getterAccessor.GetObject(target));

        var destination = default(Variant);
        setterAccessor.Get(target, ref destination);
        Assert.True(destination.IsEmpty);
        destination.Dispose();
        getterAccessor.Set(target, number);
        getterAccessor.SetObject(target, "ignored");
    }

    [Fact]
    public void Object_reflection_attribute_index_wraps_and_handles_missing_entries()
    {
        using var reflection = new ObjectReflection(typeof(AccessorTarget));
        using var first = new AttributeInfo("First", typeof(int), 1);
        using var second = new AttributeInfo("Second", typeof(int), 2);
        reflection.AddAttribute(first);
        reflection.AddAttribute(second);

        Assert.Equal(0u, reflection.GetAttributeIndex("First"));
        Assert.Equal(1u, reflection.GetAttributeIndex("Second", 0));
        Assert.Equal(0u, reflection.GetAttributeIndex("First", 1));
        Assert.Equal(1u, reflection.GetAttributeIndex("Second", 99));
        Assert.Equal(uint.MaxValue, reflection.GetAttributeIndex("Missing", 1));
        Assert.Null(reflection.GetAttribute("Missing"));

        reflection.CopyAttributesFrom(null);
        reflection.CopyAttributesFrom(reflection);
        reflection.SetCategory(null!);
        Assert.Equal(string.Empty, reflection.Category);
        Assert.Null(reflection.CreateObject());

        reflection.SetObjectFactory(static () => new AccessorTarget());
        using var created = Assert.IsType<AccessorTarget>(reflection.CreateObject());
        Assert.True(reflection.HasObjectFactory());
    }

    [Fact]
    public void Base_custom_variant_value_covers_empty_cloneable_and_disposable_paths()
    {
        using var empty = new CustomVariantValue();
        using var emptyClone = Assert.IsType<CustomVariantValue>(empty.Clone());
        Assert.Equal(typeof(void), empty.Type);
        Assert.Null(empty.Value);
        Assert.False(empty.IsZero());
        Assert.False(empty.TryGetValue<int>(out _));
        Assert.False(empty.Equals(null));
        Assert.Equal(string.Empty, empty.ToString());

        using var zero = new CustomVariantValue(0);
        Assert.True(zero.IsZero());

        using var text = new CustomVariantValue("text");
        using var textClone = Assert.IsType<CustomVariantValue>(text.Clone());
        Assert.Equal("text", textClone.Value);
        Assert.Equal(text, textClone);
        Assert.False(text.IsZero());
        Assert.True(text.TryGetValue<string>(out var textValue));
        Assert.Equal("text", textValue);

        var cloneableSource = new CloneableOnly(3);
        using var cloneable = new CustomVariantValue(cloneableSource);
        Assert.True(cloneable.TryGetValue<CloneableOnly>(out var cloneableCopy));
        Assert.NotSame(cloneableSource, cloneableCopy);
        Assert.Equal(3, cloneableCopy.Value);

        using var disposable = new CustomVariantValue(new CloneableDisposable(5));
        Assert.True(disposable.TryGetValue<CloneableDisposable>(out var storedDisposable));
        disposable.Dispose();
        Assert.True(storedDisposable.IsDisposed);
    }

    [Fact]
    public void Variant_parsing_and_generic_get_cover_invalid_scalar_and_custom_curve_paths()
    {
        var value = default(Variant);
        value.FromString(VariantType.Bool, string.Empty);
        Assert.False(value.GetBool());
        value.FromString(VariantType.Int64, "invalid");
        Assert.Equal(0L, value.GetInt64());
        value.FromString(VariantType.Double, "invalid");
        Assert.Equal(0.0, value.GetDouble());
        value.FromString(VariantType.Buffer, "1 invalid 3");
        Assert.Equal(new byte[] { 1, 0, 3 }, value.GetBuffer());
        value.Dispose();

        using var custom = Variant.MakeCustomValue(new CustomValue(8));
        Assert.Equal(new CustomValue(8), custom.Get<CustomValue>());

        using var sourceCurve = new VariantCurve();
        using var curveVariant = new Variant(sourceCurve);
        using var curve = curveVariant.Get<VariantCurve>();
        Assert.Empty(curve.KeyFrames);
    }

    [Fact]
    public void Runtime_services_returns_null_for_missing_backend_api()
    {
        var backend = new TestGraphicsBackend(RenderBackend.OpenGL);
        var services = new RuntimeServices(new TestPlatformRuntime(), [backend]);

        Assert.Same(backend, services.GetGraphicsApi<IGraphicsBackend>(RenderBackend.OpenGL));
        Assert.Null(services.GetGraphicsApi<IGraphicsBackend>(RenderBackend.Vulkan));
        Assert.Same(services.Platform, services.GetPlatformApi<TestPlatformRuntime>());
    }

    private enum TestMode
    {
        Disabled,
        Enabled
    }

    private readonly record struct CustomValue(int Value);

    private sealed class AccessorTarget : Serializable
    {
        public TestMode Mode { get; set; }

        public uint Unsigned { get; set; }

        public ulong LargeUnsigned { get; set; }

        public int Number { get; set; }

        public string? Text { get; set; }
    }

    private sealed class CloneableOnly(int value) : ICloneable
    {
        public int Value { get; } = value;

        public object Clone() => new CloneableOnly(Value);
    }

    private sealed class CloneableDisposable(int value) : ICloneable, IDisposable
    {
        public int Value { get; } = value;

        public bool IsDisposed { get; private set; }

        public object Clone() => new CloneableDisposable(Value);

        public void Dispose() => IsDisposed = true;
    }

    private sealed class TestPlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => true;

        public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

        public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) => default;
    }

    private sealed class TestGraphicsBackend(RenderBackend backend) : IGraphicsBackend
    {
        public RenderBackend Backend => backend;

        public bool UsesOpenGLWindow => false;

        public void ConfigureWindow()
        {
        }

        public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
            WindowSettings windowSettings, SdlWindow window, IPlatformRuntime platform) =>
            throw new NotSupportedException();
    }
}
