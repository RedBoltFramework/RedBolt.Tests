namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.RenderAPI;
using DiligentComponentType = global::RedBolt.Bindings.Diligent.ComponentType;
using DiligentTextureFormat = global::RedBolt.Bindings.Diligent.TextureFormat;
using DiligentShaderType = global::RedBolt.Bindings.Diligent.ShaderType;

public sealed class RenderAPICoverageGapTests
{
    [Fact]
    public void Srgb_helpers_cover_every_linear_and_srgb_mapping()
    {
        var pairs = new (DiligentTextureFormat Linear, DiligentTextureFormat SRgb)[]
        {
            (DiligentTextureFormat.TexFormatRgba8Unorm, DiligentTextureFormat.TexFormatRgba8UnormSrgb),
            (DiligentTextureFormat.TexFormatBgra8Unorm, DiligentTextureFormat.TexFormatBgra8UnormSrgb),
            (DiligentTextureFormat.TexFormatBgrx8Unorm, DiligentTextureFormat.TexFormatBgrx8UnormSrgb),
            (DiligentTextureFormat.TexFormatBc1Unorm, DiligentTextureFormat.TexFormatBc1UnormSrgb),
            (DiligentTextureFormat.TexFormatBc2Unorm, DiligentTextureFormat.TexFormatBc2UnormSrgb),
            (DiligentTextureFormat.TexFormatBc3Unorm, DiligentTextureFormat.TexFormatBc3UnormSrgb),
            (DiligentTextureFormat.TexFormatBc7Unorm, DiligentTextureFormat.TexFormatBc7UnormSrgb),
            (DiligentTextureFormat.TexFormatEtc2Rgb8Unorm, DiligentTextureFormat.TexFormatEtc2Rgb8UnormSrgb),
            (DiligentTextureFormat.TexFormatEtc2Rgb8a1Unorm, DiligentTextureFormat.TexFormatEtc2Rgb8a1UnormSrgb),
            (DiligentTextureFormat.TexFormatEtc2Rgba8Unorm, DiligentTextureFormat.TexFormatEtc2Rgba8UnormSrgb)
        };

        foreach (var (linear, sRgb) in pairs)
        {
            Assert.False(RenderAPIUtils.IsTextureFormatSRGB(linear));
            Assert.True(RenderAPIUtils.IsTextureFormatSRGB(sRgb));
            Assert.Equal(sRgb, RenderAPIUtils.SetTextureFormatSRGB(linear));
            Assert.Equal(linear, RenderAPIUtils.SetTextureFormatSRGB(sRgb, false));
            Assert.Equal(sRgb, RenderAPIUtils.SetTextureFormatSRGB(sRgb));
            Assert.Equal(linear, RenderAPIUtils.SetTextureFormatSRGB(linear, false));
        }

        Assert.False(RenderAPIUtils.IsTextureFormatSRGB(DiligentTextureFormat.TexFormatUnknown));
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.SetTextureFormatSRGB(DiligentTextureFormat.TexFormatUnknown));
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.SetTextureFormatSRGB(DiligentTextureFormat.TexFormatUnknown, false));
    }

    [Fact]
    public void Texture_format_metadata_covers_native_emulated_and_unknown_formats()
    {
        var native = RenderAPIUtils.GetTextureFormatInfo(DiligentTextureFormat.TexFormatRgba8Unorm);
        Assert.Equal("TEX_FORMAT_RGBA8_UNORM", native.Name);
        Assert.Equal(DiligentTextureFormat.TexFormatRgba8Unorm, native.Format);
        Assert.Equal(4, native.GetElementSize());
        Assert.Equal(DiligentComponentType.ComponentTypeUnorm, native.ComponentType);

        var compressed = RenderAPIUtils.GetTextureFormatInfo(DiligentTextureFormat.TexFormatBc1Unorm);
        Assert.Equal(DiligentComponentType.ComponentTypeCompressed, compressed.ComponentType);
        Assert.Equal(8, compressed.GetElementSize());
        Assert.Equal((byte)4, compressed.BlockWidth);
        Assert.Equal((byte)4, compressed.BlockHeight);

        var emulatedFormats = new (DiligentTextureFormat Format, string Name, byte Components, byte Width, byte Height)[]
        {
            (EmulatedTextureFormat.PvrtcRgb2Bpp, "TEX_FORMAT_PVRTC_RGB_2BPP", 3, 8, 4),
            (EmulatedTextureFormat.PvrtcRgba2Bpp, "TEX_FORMAT_PVRTC_RGBA_2BPP", 4, 8, 4),
            (EmulatedTextureFormat.PvrtcRgb4Bpp, "TEX_FORMAT_PVRTC_RGB_4BPP", 3, 4, 4),
            (EmulatedTextureFormat.PvrtcRgba4Bpp, "TEX_FORMAT_PVRTC_RGBA_4BPP", 4, 4, 4)
        };

        foreach (var (format, name, components, width, height) in emulatedFormats)
        {
            var info = RenderAPIUtils.GetTextureFormatInfo(format);
            Assert.Equal(name, info.Name);
            Assert.Equal(format, info.Format);
            Assert.Equal((byte)8, info.ComponentSize);
            Assert.Equal(components, info.NumComponents);
            Assert.Equal(DiligentComponentType.ComponentTypeCompressed, info.ComponentType);
            Assert.Equal(width, info.BlockWidth);
            Assert.Equal(height, info.BlockHeight);
            Assert.Equal(8, info.GetElementSize());
        }

        var unknownValue = (DiligentTextureFormat)((int)DiligentTextureFormat.TexFormatNumFormats + 100);
        var unknown = RenderAPIUtils.GetTextureFormatInfo(unknownValue);
        Assert.Equal("TEX_FORMAT_UNKNOWN", unknown.Name);
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown, unknown.Format);
        Assert.Equal(DiligentComponentType.ComponentTypeUndefined, unknown.ComponentType);

        Assert.True(RenderAPIUtils.IsColorTextureFormat(DiligentTextureFormat.TexFormatRgba8Unorm));
        Assert.True(RenderAPIUtils.IsColorTextureFormat(DiligentTextureFormat.TexFormatBc1Unorm));
        Assert.True(RenderAPIUtils.IsDepthTextureFormat(DiligentTextureFormat.TexFormatD16Unorm));
        Assert.True(RenderAPIUtils.IsDepthTextureFormat(DiligentTextureFormat.TexFormatD32Float));
        Assert.True(RenderAPIUtils.IsDepthStencilTextureFormat(DiligentTextureFormat.TexFormatD24UnormS8Uint));
        Assert.True(RenderAPIUtils.IsDepthStencilTextureFormat(DiligentTextureFormat.TexFormatD32FloatS8x24Uint));
        Assert.False(RenderAPIUtils.IsDepthStencilTextureFormat(DiligentTextureFormat.TexFormatD32Float));
        Assert.False(RenderAPIUtils.IsColorTextureFormat(DiligentTextureFormat.TexFormatUnknown));
        Assert.False(RenderAPIUtils.IsColorTextureFormat(DiligentTextureFormat.TexFormatD24UnormS8Uint));
    }

    [Fact]
    public void Mip_helpers_cover_unit_dimensions_clamping_and_block_rounding()
    {
        Assert.Equal(1, RenderAPIUtils.GetMipLevelCount(new Vector3i(0, 0, 0)));
        Assert.Equal(1, RenderAPIUtils.GetMipLevelCount(Vector3i.One));
        Assert.Equal(4, RenderAPIUtils.GetMipLevelCount(new Vector3i(9, 5, 3)));

        var size = new Vector3i(9, 5, 3);
        Assert.Equal(new Vector3i(9, 5, 3), RenderAPIUtils.GetMipLevelSize(size, 0));
        Assert.Equal(new Vector3i(4, 2, 1), RenderAPIUtils.GetMipLevelSize(size, 1));
        Assert.Equal(new Vector3i(2, 1, 1), RenderAPIUtils.GetMipLevelSize(size, 2));
        Assert.Equal(Vector3i.One, RenderAPIUtils.GetMipLevelSize(size, 8));

        Assert.Equal(48, RenderAPIUtils.GetMipLevelSizeInBytes(
            DiligentTextureFormat.TexFormatRgba8Unorm, new Vector3i(3, 2, 2), 0));
        Assert.Equal(4, RenderAPIUtils.GetMipLevelSizeInBytes(
            DiligentTextureFormat.TexFormatRgba8Unorm, new Vector3i(3, 2, 2), 4));
        Assert.Equal(96, RenderAPIUtils.GetMipLevelSizeInBytes(
            DiligentTextureFormat.TexFormatBc1Unorm, new Vector3i(9, 5, 2), 0));
        Assert.Equal(8, RenderAPIUtils.GetMipLevelSizeInBytes(
            DiligentTextureFormat.TexFormatBc1Unorm, new Vector3i(9, 5, 2), 1));
        Assert.Equal(64, RenderAPIUtils.GetMipLevelSizeInBytes(
            EmulatedTextureFormat.PvrtcRgba2Bpp, new Vector3i(9, 5, 2), 0));
        Assert.Equal(32, RenderAPIUtils.GetMipLevelSizeInBytes(
            EmulatedTextureFormat.PvrtcRgb4Bpp, new Vector3i(5, 5, 1), 0));
    }

    [Fact]
    public void Vertex_and_shader_tables_cover_all_entries_and_index_forms()
    {
        var semanticNames = new[]
        {
            "iPos",
            "iNormal",
            "iBinormal",
            "iTangent",
            "iTexCoord",
            "iColor",
            "iBlendWeights",
            "iBlendIndices",
            ""
        };

        for (var index = 0; index < semanticNames.Length; ++index)
        {
            var semantic = (VertexElementSemantic)index;
            Assert.Equal(semanticNames[index], RenderAPIUtils.ToShaderInputName(semantic));

            if (semanticNames[index].Length == 0)
                continue;

            var parsed = RenderAPIUtils.ParseVertexAttribute($"input_{semanticNames[index]}12_suffix");
            Assert.Equal(new VertexShaderAttribute(semantic, 12, 0), parsed);
        }

        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 0),
            RenderAPIUtils.ParseVertexAttribute("iPos"));
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.Position, 7, 0),
            RenderAPIUtils.ParseVertexAttribute("input_iPos7_suffix"));
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.ObjectIndex, 42, 0),
            RenderAPIUtils.ParseVertexAttribute("42arbitrary"));

        // ObjectIndex intentionally has an empty lookup name, so an unmatched name falls back to index zero.
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.ObjectIndex, 0, 0),
            RenderAPIUtils.ParseVertexAttribute("unmatched"));

        var expectedShaderTypes = new[]
        {
            DiligentShaderType.ShaderTypeVertex,
            DiligentShaderType.ShaderTypePixel,
            DiligentShaderType.ShaderTypeGeometry,
            DiligentShaderType.ShaderTypeHull,
            DiligentShaderType.ShaderTypeDomain,
            DiligentShaderType.ShaderTypeCompute
        };
        foreach (var shaderType in Enum.GetValues<ShaderType>().Where(type => type != ShaderType.MaxShaderTypes))
        {
            Assert.Equal(expectedShaderTypes[(int)shaderType], RenderAPIUtils.ToInternalShaderType(shaderType));
            Assert.NotEmpty(RenderAPIUtils.ToString(shaderType));
        }

        foreach (var backend in Enum.GetValues<RenderBackend>().Where(backend => backend != RenderBackend.Count))
            Assert.NotEmpty(RenderAPIUtils.ToString(backend));
    }

    [Fact]
    public void Shader_policy_and_internal_format_tables_cover_defaults_overrides_and_fallbacks()
    {
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.D3D11));
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.D3D12));
        Assert.Equal(ShaderTranslationPolicy.Verbatim,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.OpenGL));
        Assert.Equal(ShaderTranslationPolicy.Optimize,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.Vulkan));
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.Count));

        Assert.Equal(ShaderTranslationPolicy.Verbatim,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.OpenGL, ShaderTranslationPolicy.Verbatim));
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.OpenGL, ShaderTranslationPolicy.Translate));
        Assert.Equal(ShaderTranslationPolicy.Optimize,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.OpenGL, ShaderTranslationPolicy.Optimize));
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.D3D11, ShaderTranslationPolicy.Verbatim));
        Assert.Equal(ShaderTranslationPolicy.Optimize,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.D3D11, ShaderTranslationPolicy.Optimize));
        Assert.Equal(ShaderTranslationPolicy.Translate,
            RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.D3D11, (ShaderTranslationPolicy)99));

        var direct3DFormats = new (uint Internal, DiligentTextureFormat Format)[]
        {
            (28, DiligentTextureFormat.TexFormatRgba8Unorm),
            (29, DiligentTextureFormat.TexFormatRgba8UnormSrgb),
            (55, DiligentTextureFormat.TexFormatD16Unorm),
            (45, DiligentTextureFormat.TexFormatD24UnormS8Uint),
            (40, DiligentTextureFormat.TexFormatD32Float),
            (20, DiligentTextureFormat.TexFormatD32FloatS8x24Uint)
        };
        foreach (var backend in new[] { RenderBackend.D3D11, RenderBackend.D3D12 })
        {
            foreach (var (internalFormat, format) in direct3DFormats)
                Assert.Equal(format, RenderAPIUtils.GetTextureFormatFromInternal(backend, internalFormat));

            Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
                RenderAPIUtils.GetTextureFormatFromInternal(backend, 0));
        }

        var vulkanFormats = new (uint Internal, DiligentTextureFormat Format)[]
        {
            (37, DiligentTextureFormat.TexFormatRgba8Unorm),
            (43, DiligentTextureFormat.TexFormatRgba8UnormSrgb),
            (124, DiligentTextureFormat.TexFormatD16Unorm),
            (129, DiligentTextureFormat.TexFormatD24UnormS8Uint),
            (126, DiligentTextureFormat.TexFormatD32Float),
            (130, DiligentTextureFormat.TexFormatD32FloatS8x24Uint)
        };
        foreach (var (internalFormat, format) in vulkanFormats)
            Assert.Equal(format, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, internalFormat));
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 0));

        var openGlFormats = new (uint Internal, DiligentTextureFormat Format)[]
        {
            (0x8051, DiligentTextureFormat.TexFormatRgba8Unorm),
            (0x8058, DiligentTextureFormat.TexFormatRgba8Unorm),
            (0x8C41, DiligentTextureFormat.TexFormatRgba8UnormSrgb),
            (0x8C43, DiligentTextureFormat.TexFormatRgba8UnormSrgb),
            (0x81A5, DiligentTextureFormat.TexFormatD16Unorm),
            (0x81A6, DiligentTextureFormat.TexFormatD24UnormS8Uint),
            (0x88F0, DiligentTextureFormat.TexFormatD24UnormS8Uint),
            (0x8CAC, DiligentTextureFormat.TexFormatD32Float),
            (0x8CAD, DiligentTextureFormat.TexFormatD32FloatS8x24Uint)
        };
        foreach (var (internalFormat, format) in openGlFormats)
            Assert.Equal(format, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, internalFormat));
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0));
        Assert.Equal(DiligentTextureFormat.TexFormatUnknown,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Count, 0));
    }

    [Fact]
    public void Backend_selection_covers_supported_and_fallback_paths()
    {
        var supportedBackends = Context.Instance.RuntimeServices.GraphicsBackends;
        Assert.NotEmpty(supportedBackends);

        foreach (var supportedBackend in supportedBackends)
            Assert.Equal(supportedBackend.Backend, RenderAPIUtils.SelectRenderBackend(supportedBackend.Backend));

        var firstBackend = supportedBackends.First().Backend;
        Assert.Equal(firstBackend, RenderAPIUtils.SelectRenderBackend(RenderBackend.Count));
    }

    [Fact]
    public void Vulkan_settings_and_descriptor_pool_serializers_round_trip_all_fields()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var settings = new RenderDeviceSettingsVulkan
        {
            MainDescriptorPoolSize = MakeDescriptorPoolSize(10),
            DynamicDescriptorPoolSize = MakeDescriptorPoolSize(20),
            DeviceLocalMemoryPageSize = 30,
            HostVisibleMemoryPageSize = 40,
            DeviceLocalMemoryReserveSize = 50,
            HostVisibleMemoryReserveSize = 60,
            UploadHeapPageSize = 70,
            DynamicHeapSize = 80,
            DynamicHeapPageSize = 90
        };
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion] = 101;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion] = 102;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp] = 103;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics] = 104;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration] = 105;
        var expected = settings;

        using (var output = new BinaryOutputArchive(context, buffer))
        {
            using (output.OpenUnorderedBlock("Root"))
                RenderAPIUtils.SerializeValue(output, "settings", ref settings);
            output.Flush();
        }

        buffer.Seek(0);
        var loaded = new RenderDeviceSettingsVulkan();
        using (var input = new BinaryInputArchive(context, buffer))
        {
            using (input.OpenUnorderedBlock("Root"))
                RenderAPIUtils.SerializeValue(input, "settings", ref loaded);
            input.Flush();
        }

        Assert.Equal(expected.MainDescriptorPoolSize, loaded.MainDescriptorPoolSize);
        Assert.Equal(expected.DynamicDescriptorPoolSize, loaded.DynamicDescriptorPoolSize);
        Assert.Equal(expected.DeviceLocalMemoryPageSize, loaded.DeviceLocalMemoryPageSize);
        Assert.Equal(expected.HostVisibleMemoryPageSize, loaded.HostVisibleMemoryPageSize);
        Assert.Equal(expected.DeviceLocalMemoryReserveSize, loaded.DeviceLocalMemoryReserveSize);
        Assert.Equal(expected.HostVisibleMemoryReserveSize, loaded.HostVisibleMemoryReserveSize);
        Assert.Equal(expected.UploadHeapPageSize, loaded.UploadHeapPageSize);
        Assert.Equal(expected.DynamicHeapSize, loaded.DynamicHeapSize);
        Assert.Equal(expected.DynamicHeapPageSize, loaded.DynamicHeapPageSize);
        Assert.Equal(101, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion]);
        Assert.Equal(102, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion]);
        Assert.Equal(103, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp]);
        Assert.Equal(104, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics]);
        Assert.Equal(105, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration]);
    }

    [Fact]
    public void D3D12_settings_serializer_round_trips_all_optional_slots()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var settings = new RenderDeviceSettingsD3D12();
        settings.CpuDescriptorHeapAllocationSize[0] = 11;
        settings.CpuDescriptorHeapAllocationSize[1] = 12;
        settings.CpuDescriptorHeapAllocationSize[2] = 13;
        settings.CpuDescriptorHeapAllocationSize[3] = 14;
        settings.GpuDescriptorHeapSize[0] = 21;
        settings.GpuDescriptorHeapSize[1] = 22;
        settings.GpuDescriptorHeapDynamicSize[0] = 31;
        settings.GpuDescriptorHeapDynamicSize[1] = 32;
        settings.DynamicDescriptorAllocationChunkSize[0] = 41;
        settings.DynamicDescriptorAllocationChunkSize[1] = 42;
        settings.DynamicHeapPageSize = 51;
        settings.NumDynamicHeapPagesToReserve = 52;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion] = 61;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion] = 62;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp] = 63;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics] = 64;
        settings.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration] = 65;
        var expected = settings;

        using (var output = new BinaryOutputArchive(context, buffer))
        {
            using (output.OpenUnorderedBlock("Root"))
                RenderAPIUtils.SerializeValue(output, "settings", ref settings);
            output.Flush();
        }

        buffer.Seek(0);
        var loaded = new RenderDeviceSettingsD3D12();
        using (var input = new BinaryInputArchive(context, buffer))
        {
            using (input.OpenUnorderedBlock("Root"))
                RenderAPIUtils.SerializeValue(input, "settings", ref loaded);
            input.Flush();
        }

        for (var index = 0; index < 4; ++index)
            Assert.Equal(expected.CpuDescriptorHeapAllocationSize[index], loaded.CpuDescriptorHeapAllocationSize[index]);
        for (var index = 0; index < 2; ++index)
        {
            Assert.Equal(expected.GpuDescriptorHeapSize[index], loaded.GpuDescriptorHeapSize[index]);
            Assert.Equal(expected.GpuDescriptorHeapDynamicSize[index], loaded.GpuDescriptorHeapDynamicSize[index]);
            Assert.Equal(expected.DynamicDescriptorAllocationChunkSize[index], loaded.DynamicDescriptorAllocationChunkSize[index]);
        }

        Assert.Equal(expected.DynamicHeapPageSize, loaded.DynamicHeapPageSize);
        Assert.Equal(expected.NumDynamicHeapPagesToReserve, loaded.NumDynamicHeapPagesToReserve);
        Assert.Equal(61, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion]);
        Assert.Equal(62, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion]);
        Assert.Equal(63, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp]);
        Assert.Equal(64, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics]);
        Assert.Equal(65, loaded.QueryPoolSizes[(int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration]);
    }

    [Fact]
    public void Serialization_helpers_guard_null_archive_and_name()
    {
        var vulkanSettings = new RenderDeviceSettingsVulkan();
        var d3d12Settings = new RenderDeviceSettingsD3D12();
        var descriptorPool = new VulkanDescriptorPoolSize();

        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(null!, "settings", ref vulkanSettings));
        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(null!, "settings", ref d3d12Settings));
        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(null!, "pool", ref descriptorPool));

        var context = new Context();
        using var buffer = new VectorBuffer();
        using var output = new BinaryOutputArchive(context, buffer);
        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(output, null!, ref vulkanSettings));
        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(output, null!, ref d3d12Settings));
        Assert.Throws<ArgumentNullException>(() =>
            RenderAPIUtils.SerializeValue(output, null!, ref descriptorPool));
    }

    [Fact]
    public void Serialization_helpers_initialize_missing_optional_settings_on_input()
    {
        using var vulkanInput = new TestJSONInputArchive(
            "{\"settings\":{\"mainDescriptorPoolSize\":{\"maxDescriptorSets\":0," +
            "\"numSeparateSamplerDescriptors\":0,\"numCombinedSamplerDescriptors\":0," +
            "\"numSampledImageDescriptors\":0,\"numStorageImageDescriptors\":0," +
            "\"numUniformBufferDescriptors\":0,\"numStorageBufferDescriptors\":0," +
            "\"numUniformTexelBufferDescriptors\":0,\"numStorageTexelBufferDescriptors\":0," +
            "\"numInputAttachmentDescriptors\":0,\"numAccelStructDescriptors\":0}," +
            "\"dynamicDescriptorPoolSize\":{\"maxDescriptorSets\":0," +
            "\"numSeparateSamplerDescriptors\":0,\"numCombinedSamplerDescriptors\":0," +
            "\"numSampledImageDescriptors\":0,\"numStorageImageDescriptors\":0," +
            "\"numUniformBufferDescriptors\":0,\"numStorageBufferDescriptors\":0," +
            "\"numUniformTexelBufferDescriptors\":0,\"numStorageTexelBufferDescriptors\":0," +
            "\"numInputAttachmentDescriptors\":0,\"numAccelStructDescriptors\":0}," +
            "\"deviceLocalMemoryPageSize\":0,\"hostVisibleMemoryPageSize\":0," +
            "\"deviceLocalMemoryReserveSize\":0,\"hostVisibleMemoryReserveSize\":0," +
            "\"uploadHeapPageSize\":0,\"dynamicHeapSize\":0,\"dynamicHeapPageSize\":0," +
            "\"queryPoolSize_occlusion\":0,\"queryPoolSize_binaryOcclusion\":0," +
            "\"queryPoolSize_timestamp\":0,\"queryPoolSize_pipelineStatistics\":0," +
            "\"queryPoolSize_duration\":0}}");
        var vulkan = new RenderDeviceSettingsVulkan();
        using (vulkanInput.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(vulkanInput, "settings", ref vulkan);

        using var d3dInput = new TestJSONInputArchive(
            "{\"settings\":{" +
            "\"mainDescriptorPoolSize_cvb_srv_uav\":0,\"mainDescriptorPoolSize_sampler\":0," +
            "\"mainDescriptorPoolSize_rtv\":0,\"mainDescriptorPoolSize_dsv\":0," +
            "\"gpuDescriptorHeapSize_cvb_srv_uav\":0,\"gpuDescriptorHeapSize_sampler\":0," +
            "\"gpuDescriptorHeapDynamicSize_cvb_srv_uav\":0,\"gpuDescriptorHeapDynamicSize_sampler\":0," +
            "\"dynamicDescriptorAllocationChunkSize_cvb_srv_uav\":0," +
            "\"dynamicDescriptorAllocationChunkSize_sampler\":0,\"dynamicHeapPageSize\":0," +
            "\"numDynamicHeapPagesToReserve\":0,\"queryPoolSize_occlusion\":0," +
            "\"queryPoolSize_binaryOcclusion\":0,\"queryPoolSize_timestamp\":0," +
            "\"queryPoolSize_pipelineStatistics\":0,\"queryPoolSize_duration\":0}}");
        var d3d = new RenderDeviceSettingsD3D12();
        using (d3dInput.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(d3dInput, "settings", ref d3d);

        Assert.NotNull(vulkan.MainDescriptorPoolSize);
        Assert.NotNull(vulkan.DynamicDescriptorPoolSize);
        var queryIndices = new[]
        {
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration
        };
        foreach (var index in queryIndices)
            Assert.Equal(0, vulkan.QueryPoolSizes[index]);
        for (var index = 0; index < 4; ++index)
            Assert.Equal(0, d3d.CpuDescriptorHeapAllocationSize[index]);
        foreach (var index in queryIndices)
            Assert.Equal(0, d3d.QueryPoolSizes[index]);
    }

    private static VulkanDescriptorPoolSize MakeDescriptorPoolSize(int value)
    {
        return new VulkanDescriptorPoolSize
        {
            MaxDescriptorSets = value,
            NumSeparateSamplerDescriptors = value + 1,
            NumCombinedSamplerDescriptors = value + 2,
            NumSampledImageDescriptors = value + 3,
            NumStorageImageDescriptors = value + 4,
            NumUniformBufferDescriptors = value + 5,
            NumStorageBufferDescriptors = value + 6,
            NumUniformTexelBufferDescriptors = value + 7,
            NumStorageTexelBufferDescriptors = value + 8,
            NumInputAttachmentDescriptors = value + 9,
            NumAccelStructDescriptors = value + 10
        };
    }
}
