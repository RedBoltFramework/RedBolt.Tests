// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;
using Tg = RedBolt.Bindings.TinyGltf;

using RedBoltNative;

[Collection(nameof(RmlUiBindingsCollection))]
public sealed class RmlUiBindingsTests
{
    [Fact]
    public void Shared_string_shape_works_across_binding_modules()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(RmlUi).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(Tg.TinyGLTF).Module.ModuleHandle);

        using var value = new Std.String("data:application/octet-stream;base64,cmVkYm9sdA==");

        Assert.True(Tg.TinyGltf.IsDataURI(value));
        Assert.Equal("data:application/octet-stream;base64,cmVkYm9sdA==", value.ToString());
    }

    [Fact]
    public void Variant_round_trips_vector_value()
    {
        using var variant = new Variant();
        var value = new Vector3f(1.0f, 2.0f, 3.0f);

        Assert.Equal(value, variant.Get(value));
        variant.Assign(value);
        Assert.Equal(value, variant.Get<Vector3f>());
    }

    [Fact]
    public void System_interface_round_trips_managed_director()
    {
        var original = RmlUi.GetSystemInterface();
        using var systemInterface = new TestSystemInterface();
        try
        {
            RmlUi.SetSystemInterface(systemInterface);

            var retrieved = RmlUi.GetSystemInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(systemInterface.Handle, retrieved.Handle);
            Assert.Equal(42.5, retrieved.GetElapsedTime());
        }
        finally
        {
            RmlUi.SetSystemInterface(original);
        }
    }

    [Fact]
    public void File_interface_round_trips_pointer_width_file_handle()
    {
        var original = RmlUi.GetFileInterface();
        using var fileInterface = new TestFileInterface();
        using var path = new Std.String();
        try
        {
            RmlUi.SetFileInterface(fileInterface);

            var retrieved = RmlUi.GetFileInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(fileInterface.Handle, retrieved.Handle);
            Assert.Equal(ExpectedFileHandle, retrieved.Open(path));
        }
        finally
        {
            RmlUi.SetFileInterface(original);
        }
    }

    [Fact]
    public void Render_interface_compatibility_round_trips_adapted_pointer()
    {
        var original = RmlUi.GetRenderInterface();
        using var renderInterface = new TestRenderInterface();
        var adaptedInterface = renderInterface.GetAdaptedInterface();
        try
        {
            RmlUi.SetRenderInterface(adaptedInterface);

            Assert.NotEqual(nint.Zero, adaptedInterface);
            Assert.Equal(adaptedInterface, RmlUi.GetRenderInterface());
            Assert.Equal(8, Marshal.SizeOf<RmlUiLegacyVector2f>());
            Assert.Equal(8, Marshal.SizeOf<RmlUiLegacyVector2i>());
            Assert.Equal(20, Marshal.SizeOf<RmlUiLegacyVertex>());
            Assert.Equal(64, Marshal.SizeOf<RmlUiLegacyMatrix4f>());
        }
        finally
        {
            RmlUi.SetRenderInterface(original);
        }
    }

    [Fact]
    public void Event_listener_constructs_managed_director()
    {
        using var eventListener = new TestEventListener();

        Assert.NotEqual(nint.Zero, eventListener.Handle);
        Assert.Same(eventListener, EventListener.CreateManagedWrapper(eventListener.Handle, false));
    }

    [Fact]
    public void Event_listener_instancer_dispatches_managed_director()
    {
        using var eventListener = new TestEventListener();
        using var eventListenerInstancer = new TestEventListenerInstancer(eventListener);
        using var value = new Std.String();
        value.Assign("event:test");
        try
        {
            Factory.RegisterEventListenerInstancer(eventListenerInstancer);

            var instancedListener = Factory.InstanceEventListener(value, null!);

            Assert.Same(eventListener, instancedListener);
            Assert.Equal("event:test", eventListenerInstancer.Value);
        }
        finally
        {
            Factory.RegisterEventListenerInstancer(null);
        }
    }

    [Fact]
    public void Context_instancer_dispatches_managed_lifecycle()
    {
        var originalFileInterface = RmlUi.GetFileInterface();
        var originalFontEngineInterface = RmlUi.GetFontEngineInterface();
        var originalRenderInterface = RmlUi.GetRenderInterface();
        var originalSystemInterface = RmlUi.GetSystemInterface();
        using var fileInterface = new TestFileInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        using var renderInterface = new TestRenderInterface();
        using var systemInterface = new TestSystemInterface();
        using var contextInstancer = new TestContextInstancer();
        using var name = new Std.String();
        var initialized = false;
        Context? context = null;
        name.Assign("managed-instancer");
        try
        {
            RmlUi.SetFileInterface(fileInterface);
            RmlUi.SetFontEngineInterface(fontEngineInterface);
            RmlUi.SetRenderInterface(renderInterface.GetAdaptedInterface());
            RmlUi.SetSystemInterface(systemInterface);
            Assert.True(RmlUi.Initialise());
            initialized = true;
            Factory.RegisterContextInstancer(contextInstancer);

            context = RmlUi.CreateContext(
                name,
                new Vector2i(320, 200),
                renderInterface.GetAdaptedInterface(),
                0);

            Assert.NotNull(context);
            Assert.Equal("managed-instancer", contextInstancer.Name);
            Assert.NotEqual(nint.Zero, contextInstancer.RenderManager);
            Assert.NotEqual(nint.Zero, contextInstancer.TextInputHandler);
            Assert.Same(contextInstancer.Context, context);
            Assert.True(RmlUi.RemoveContext(name));
            context = null;
            Assert.True(contextInstancer.WasReleased);
        }
        finally
        {
            Factory.RegisterContextInstancer(null!);
            if (context is not null)
                RmlUi.RemoveContext(name);
            if (initialized)
                RmlUi.Shutdown();
            RmlUi.SetFileInterface(originalFileInterface);
            RmlUi.SetFontEngineInterface(originalFontEngineInterface);
            RmlUi.SetRenderInterface(originalRenderInterface);
            RmlUi.SetSystemInterface(originalSystemInterface);
        }
    }

    [Fact]
    public void Plugin_dispatches_managed_lifecycle()
    {
        var originalFileInterface = RmlUi.GetFileInterface();
        var originalFontEngineInterface = RmlUi.GetFontEngineInterface();
        var originalRenderInterface = RmlUi.GetRenderInterface();
        var originalSystemInterface = RmlUi.GetSystemInterface();
        using var fileInterface = new TestFileInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        using var renderInterface = new TestRenderInterface();
        using var systemInterface = new TestSystemInterface();
        using var plugin = new TestPlugin();
        var initialized = false;
        var registered = false;
        try
        {
            RmlUi.SetFileInterface(fileInterface);
            RmlUi.SetFontEngineInterface(fontEngineInterface);
            RmlUi.SetRenderInterface(renderInterface.GetAdaptedInterface());
            RmlUi.SetSystemInterface(systemInterface);
            Assert.True(RmlUi.Initialise());
            initialized = true;

            RmlUi.RegisterPlugin(plugin);
            registered = true;

            Assert.True(plugin.IsInitialized);
            Assert.False(plugin.IsShutdown);

            RmlUi.UnregisterPlugin(plugin);
            registered = false;

            Assert.True(plugin.IsShutdown);
        }
        finally
        {
            if (registered)
                RmlUi.UnregisterPlugin(plugin);
            if (initialized)
                RmlUi.Shutdown();
            RmlUi.SetFileInterface(originalFileInterface);
            RmlUi.SetFontEngineInterface(originalFontEngineInterface);
            RmlUi.SetRenderInterface(originalRenderInterface);
            RmlUi.SetSystemInterface(originalSystemInterface);
        }
    }

    [Fact]
    public void Font_engine_interface_round_trips_managed_director()
    {
        var original = RmlUi.GetFontEngineInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        try
        {
            RmlUi.SetFontEngineInterface(fontEngineInterface);

            var retrieved = RmlUi.GetFontEngineInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(fontEngineInterface.Handle, retrieved.Handle);
        }
        finally
        {
            RmlUi.SetFontEngineInterface(original);
        }
    }

    [Fact]
    public void HarfBuzz_font_engine_encodes_gpu_textured_rml_document()
    {
        var originalFileInterface = RmlUi.GetFileInterface();
        var originalFontEngineInterface = RmlUi.GetFontEngineInterface();
        var originalRenderInterface = RmlUi.GetRenderInterface();
        using var fileInterface = new TestDiskFileInterface();
        using var fontEngineInterface = new RmlHarfBuzzFontEngine();
        using var renderInterface = new TestRenderInterface();
        using var fontPath = new Std.String();
        using var contextName = new Std.String();
        using var documentSource = new Std.String();
        using var sourceUrl = new Std.String();
        using var documentId = new Std.String();
        using var leftName = new Std.String();
        using var leftValue = new Std.String();
        using var topName = new Std.String();
        using var topValue = new Std.String();
        using var mousePassthroughName = new Std.String();
        using var componentPointerName = new Std.String();
        fontPath.Assign(Path.Combine(RbfxTestData.Data, "Fonts", "Anonymous Pro.ttf"));
        contextName.Assign("RedBolt.Tests.RmlUiBindings.HarfBuzz");
        documentSource.Assign("""
            <rml>
            <head>
                <style>
                    body { font-family: "Anonymous Pro"; font-size: 32px; color: white; }
                </style>
            </head>
            <body id="harfbuzz-document">HarfBuzz GPU</body>
            </rml>
            """);
        sourceUrl.Assign("memory://harfbuzz.rml");
        documentId.Assign("harfbuzz-document");
        leftName.Assign("left");
        leftValue.Assign("12px");
        topName.Assign("top");
        topValue.Assign("8px");
        mousePassthroughName.Assign("mouse-passthrough");
        componentPointerName.Assign("__RmlUIComponentPtr__");
        Context? context = null;
        ElementDocument? document = null;
        var initialized = false;
        try
        {
            RmlUi.SetFileInterface(fileInterface);
            RmlUi.SetFontEngineInterface(fontEngineInterface);
            RmlUi.SetRenderInterface(renderInterface.GetAdaptedInterface());
            Assert.True(RmlUi.Initialise());
            initialized = true;

            Assert.True(RmlUi.LoadFontFace(fontPath));
            context = RmlUi.CreateContext(
                contextName, new Vector2i(640, 480));
            Assert.NotNull(context);
            Assert.True(RmlUi.Initialise(context));
            RmlUi.SetVisible(true);
            Assert.True(RmlUi.IsVisible());
            RmlUi.SetVisible(false);
            Assert.True(context.Update());
            Assert.False(RmlUi.IsVisible());
            Factory.ClearStyleSheetCache();
            Factory.ClearTemplateCache();
            RmlUi.ReleaseTextures();
            var initialDocumentCount = context.GetNumDocuments();
            document = context.LoadDocumentFromMemory(documentSource, sourceUrl);
            Assert.NotNull(document);
            Assert.Equal(initialDocumentCount + 1, context.GetNumDocuments());
            var indexedDocument = context.GetDocument(initialDocumentCount);
            Assert.NotNull(indexedDocument);
            Assert.Equal(document.Handle, indexedDocument.Handle);
            var namedDocument = context.GetDocument(documentId);
            Assert.NotNull(namedDocument);
            Assert.Equal(document.Handle, namedDocument.Handle);
            var documentContext = document.GetContext();
            Assert.Equal(context.Handle, documentContext.Handle);
            var loadedSourceUrl = document.GetSourceURL();
            Assert.Equal(sourceUrl.ToString(), loadedSourceUrl.ToString());
            document.SetAttribute(mousePassthroughName, true);
            Assert.True(document.GetAttribute(mousePassthroughName, false));
            document.SetAttribute(componentPointerName, (nint)0x1234);
            Assert.Equal((nint)0x1234, document.GetAttribute(componentPointerName, (nint)0));

            Assert.True(document.SetProperty(leftName, leftValue));
            Assert.True(document.SetProperty(topName, topValue));
            var leftProperty = document.GetProperty(PropertyId.Left);
            Assert.NotNull(leftProperty);
            using (var leftCopy = leftProperty.Clone())
                Assert.True(document.SetProperty(PropertyId.Left, leftCopy));
            document.UpdateDocument();
            var absoluteOffset = document.GetAbsoluteOffset(BoxArea.Border);
            Assert.Equal(12.0f, absoluteOffset.X);
            Assert.Equal(8.0f, absoluteOffset.Y);

            document.Show(ModalFlag.Modal, FocusFlag.None);
            Assert.True(document.IsVisible());
            Assert.True(document.IsModal());
            Assert.True(context.Update());
            Assert.True(context.Render());
            Assert.True(renderInterface.GeneratedTexture);
            Assert.True(renderInterface.RenderedTexturedGeometry);
            Assert.True(renderInterface.HasGpuAtlasSignature);
            Assert.True(renderInterface.RenderedPackedUvs);
            Assert.Equal(2048, renderInterface.TextureDimensions.X);
            Assert.InRange(renderInterface.TextureDimensions.Y, 8, 512);
            Assert.Equal(0, renderInterface.TextureDimensions.Y & (renderInterface.TextureDimensions.Y - 1));

            var initialAtlasVersion = renderInterface.TextureGenerationCount;
            using var addedGlyphSource = new Std.String();
            using var addedGlyphUrl = new Std.String();
            addedGlyphSource.Assign("""
                <rml><head><style>body { font-family: "Anonymous Pro"; font-size: 32px; }</style></head>
                <body>Previously unused glyph: Ω</body></rml>
                """);
            addedGlyphUrl.Assign("memory://harfbuzz-added-glyph.rml");
            using var addedGlyphDocument = context.LoadDocumentFromMemory(addedGlyphSource, addedGlyphUrl);
            Assert.NotNull(addedGlyphDocument);
            addedGlyphDocument.Show();
            Assert.True(context.Update());
            Assert.True(context.Render());
            Assert.True(renderInterface.TextureGenerationCount > initialAtlasVersion);
            addedGlyphDocument.Close();

            document.Close();
            document.Dispose();
            document = null;
            Assert.True(context.Update());
            context.Dispose();
            context = null;
            Assert.True(RmlUi.RemoveContext(contextName));
            RmlUi.Shutdown();
            initialized = false;
        }
        finally
        {
            if (document is not null)
            {
                document.Close();
                document.Dispose();
                context?.Update();
            }
            context?.Dispose();
            if (context is not null)
                RmlUi.RemoveContext(contextName);
            if (initialized)
                RmlUi.Shutdown();
            RmlUi.SetRenderInterface(originalRenderInterface);
            RmlUi.SetFontEngineInterface(originalFontEngineInterface);
            RmlUi.SetFileInterface(originalFileInterface);
        }
    }

    [Fact]
    public void Core_lifecycle_creates_and_updates_context()
    {
        var originalFontEngineInterface = RmlUi.GetFontEngineInterface();
        var originalRenderInterface = RmlUi.GetRenderInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        using var renderInterface = new TestRenderInterface();
        using var name = new Std.String();
        using var textInput = new Std.String();
        name.Assign("RedBolt.Tests.RmlUiBindings");
        textInput.Assign("text");
        Context? context = null;
        var initialized = false;
        try
        {
            RmlUi.SetFontEngineInterface(fontEngineInterface);
            RmlUi.SetRenderInterface(renderInterface.GetAdaptedInterface());
            Assert.True(RmlUi.Initialise());
            initialized = true;
            Assert.True(fontEngineInterface.IsInitialized);

            context = RmlUi.CreateContext(
                name, new Vector2i(320, 200));

            Assert.NotNull(context);
            Assert.Equal(320, context.GetDimensions().X);
            Assert.Equal(200, context.GetDimensions().Y);
            using var contextName = context.GetName();
            Assert.Equal("RedBolt.Tests.RmlUiBindings", contextName.ToString());

            context.SetDimensions(new Vector2i(640, 480));
            context.SetDensityIndependentPixelRatio(1.5f);

            Assert.Equal(640, context.GetDimensions().X);
            Assert.Equal(480, context.GetDimensions().Y);
            Assert.Equal(1.5f, context.GetDensityIndependentPixelRatio());
            Assert.Null(context.GetHoverElement());
            using var focusElement = context.GetFocusElement();
            Assert.NotNull(focusElement);
            using (var root = context.GetRootElement())
            {
                Assert.Equal(root.Handle, focusElement.Handle);
                Assert.Null(root.GetOwnerDocument());
                using var tagName = root.GetTagName();
                Assert.Equal("#root", tagName.ToString());
                root.Blur();
            }
            Assert.True(context.ProcessKeyDown(Rml.Input.KeyIdentifier.KiReturn, 0));
            Assert.True(context.ProcessKeyUp(Rml.Input.KeyIdentifier.KiReturn, 0));
            Assert.True(context.ProcessTextInput((sbyte)'\n'));
            Assert.True(context.ProcessTextInput(textInput));
            Assert.True(context.ProcessMouseMove(10, 10, 0));
            Assert.True(context.ProcessMouseButtonDown(0, 0));
            Assert.True(context.ProcessMouseButtonUp(0, 0));
            Assert.True(context.ProcessMouseWheel(1.0f, 0));
            Assert.True(context.Update());
            Assert.True(context.Render());

            context.Dispose();
            context = null;
            Assert.True(RmlUi.RemoveContext(name));
            RmlUi.Shutdown();
            initialized = false;
            Assert.True(fontEngineInterface.IsShutdown);
        }
        finally
        {
            context?.Dispose();
            if (initialized)
            {
                RmlUi.RemoveContext(name);
                RmlUi.Shutdown();
            }
            RmlUi.SetFontEngineInterface(originalFontEngineInterface);
            RmlUi.SetRenderInterface(originalRenderInterface);
        }
    }

    private sealed class TestSystemInterface : SystemInterface
    {
        public override double GetElapsedTime()
        {
            return 42.5;
        }
    }

    private sealed class TestFileInterface : FileInterface
    {
        public override nuint Open(Std.String path)
        {
            return ExpectedFileHandle;
        }

        public override void Close(nuint file)
        {
        }

        public override nuint Read(nint buffer, nuint size, nuint file)
        {
            return 0;
        }

        public override bool Seek(nuint file, nint offset, int origin)
        {
            return false;
        }

        public override nuint Tell(nuint file)
        {
            return 0;
        }
    }

    private sealed class TestRenderInterface : RenderInterfaceCompatibility
    {
        public bool GeneratedTexture { get; private set; }

        public bool RenderedTexturedGeometry { get; private set; }

        public bool RenderedPackedUvs { get; private set; }

        public bool HasGpuAtlasSignature { get; private set; }

        public int TextureGenerationCount { get; private set; }

        public RmlUiLegacyVector2i TextureDimensions { get; private set; }

        public override unsafe void RenderGeometry(
            nint vertices, int numVertices, nint indices, int numIndices, nuint texture, nint translation)
        {
            if (vertices != 0 && numVertices > 0 && indices != 0 && numIndices > 0 && texture != 0)
            {
                RenderedTexturedGeometry = true;
                var sourceVertices = new ReadOnlySpan<RmlUiLegacyVertex>((void*)vertices, numVertices);
                foreach (var vertex in sourceVertices)
                {
                    if (MathF.Floor(vertex.TexCoord.X) >= 2.0f && MathF.Floor(vertex.TexCoord.Y) >= 2.0f)
                    {
                        RenderedPackedUvs = true;
                        break;
                    }
                }
            }
        }

        public override void EnableScissorRegion(bool enable)
        {
        }

        public override void SetScissorRegion(int x, int y, int width, int height)
        {
        }

        public override unsafe bool GenerateTexture(nint textureHandle, nint source, nint sourceDimensions)
        {
            if (textureHandle == 0 || source == 0 || sourceDimensions == 0)
                return false;
            TextureDimensions = *(RmlUiLegacyVector2i*)sourceDimensions;
            var sourceLength = checked(TextureDimensions.X * TextureDimensions.Y * 4);
            var sourceData = new ReadOnlySpan<byte>((void*)source, sourceLength);
            ReadOnlySpan<byte> transportSignature =
                [(byte)'R', (byte)'B', (byte)'H', 0xff, (byte)'B', (byte)'G', (byte)'P', 0xff,
                    (byte)'U', (byte)'1', 0, 0xff];
            if (TextureDimensions.X == 2048 && sourceData.StartsWith(transportSignature))
            {
                Span<byte> decodedSignature = stackalloc byte[8];
                for (var index = 0; index < decodedSignature.Length; ++index)
                    decodedSignature[index] = sourceData[transportSignature.Length + (index / 3) * 4 + index % 3];
                HasGpuAtlasSignature |= decodedSignature.SequenceEqual("RBHBGPU1"u8);
            }
            *(nuint*)textureHandle = 1;
            GeneratedTexture = true;
            ++TextureGenerationCount;
            return true;
        }
    }

    private sealed class TestDiskFileInterface : FileInterface
    {
        public override nuint Open(Std.String path)
        {
            if (!System.IO.File.Exists(path.ToString()))
                return 0;
            return (nuint)GCHandle.ToIntPtr(GCHandle.Alloc(System.IO.File.OpenRead(path.ToString())));
        }

        public override void Close(nuint file)
        {
            var handle = GCHandle.FromIntPtr((nint)file);
            ((FileStream)handle.Target!).Dispose();
            handle.Free();
        }

        public override unsafe nuint Read(nint buffer, nuint size, nuint file)
        {
            var handle = GCHandle.FromIntPtr((nint)file);
            return (nuint)((FileStream)handle.Target!).Read(new Span<byte>((void*)buffer, checked((int)size)));
        }

        public override bool Seek(nuint file, nint offset, int origin)
        {
            var handle = GCHandle.FromIntPtr((nint)file);
            var stream = (FileStream)handle.Target!;
            stream.Seek(offset, (SeekOrigin)origin);
            return true;
        }

        public override nuint Tell(nuint file)
        {
            var handle = GCHandle.FromIntPtr((nint)file);
            return (nuint)((FileStream)handle.Target!).Position;
        }

        public override nuint Length(nuint file)
        {
            var handle = GCHandle.FromIntPtr((nint)file);
            return (nuint)((FileStream)handle.Target!).Length;
        }
    }

    private sealed class TestFontEngineInterface : FontEngineInterface
    {
        public bool IsInitialized { get; private set; }

        public bool IsShutdown { get; private set; }

        public override void Initialize()
        {
            IsInitialized = true;
        }

        public override void Shutdown()
        {
            IsShutdown = true;
        }
    }

    private sealed class TestEventListener : EventListener
    {
        public override void ProcessEvent(Event @event)
        {
        }
    }

    private sealed class TestEventListenerInstancer(EventListener eventListener) : EventListenerInstancer
    {
        public string? Value { get; private set; }

        public override EventListener InstanceEventListener(Std.String value, Element element)
        {
            Value = value.ToString();
            return eventListener;
        }
    }

    private sealed class TestContextInstancer : ContextInstancer
    {
        public Context? Context { get; private set; }

        public string? Name { get; private set; }

        public nint RenderManager { get; private set; }

        public nint TextInputHandler { get; private set; }

        public bool WasReleased { get; private set; }

        public override Context InstanceContext(Std.String name, nint renderManager, nint textInputHandler)
        {
            Name = name.ToString();
            RenderManager = renderManager;
            TextInputHandler = textInputHandler;
            Context = new Context(name, renderManager, textInputHandler);
            return Context;
        }

        public override void ReleaseContext(Context context)
        {
            WasReleased = true;
            context.Dispose();
        }

        protected override void Release()
        {
        }
    }

    private sealed class TestPlugin : Plugin
    {
        public bool IsInitialized { get; private set; }

        public bool IsShutdown { get; private set; }

        public override int GetEventClasses()
        {
            return 2;
        }

        public override void OnInitialise()
        {
            IsInitialized = true;
        }

        public override void OnShutdown()
        {
            IsShutdown = true;
        }
    }

    private static nuint ExpectedFileHandle =>
        nuint.Size > sizeof(uint) ? unchecked((nuint)uint.MaxValue + 1) : uint.MaxValue;
}

[CollectionDefinition(nameof(RmlUiBindingsCollection), DisableParallelization = true)]
public sealed class RmlUiBindingsCollection;
