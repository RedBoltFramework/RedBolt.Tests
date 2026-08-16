// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.RenderAPI;
using RedBolt.Resource;

public sealed class ImageDdsCoverageTests
{
    [Fact]
    public void Pick_texture_format_covers_dxgi_and_legacy_fourcc_families()
    {
        var dxgiFormats = new (uint Value, TextureFormat Expected)[]
        {
            (2, TextureFormat.TexFormatRgba32Float),
            (10, TextureFormat.TexFormatRgba16Float),
            (11, TextureFormat.TexFormatRgba16Unorm),
            (24, TextureFormat.TexFormatRgb10a2Unorm),
            (28, TextureFormat.TexFormatRgba8Unorm),
            (26, TextureFormat.TexFormatRgba8UnormSrgb),
            (67, TextureFormat.TexFormatRgb9e5Sharedexp),
            (71, TextureFormat.TexFormatBc1Unorm),
            (72, TextureFormat.TexFormatBc1UnormSrgb),
            (74, TextureFormat.TexFormatBc2Unorm),
            (75, TextureFormat.TexFormatBc2UnormSrgb),
            (77, TextureFormat.TexFormatBc3Unorm),
            (78, TextureFormat.TexFormatBc3UnormSrgb),
            (85, TextureFormat.TexFormatB5g6r5Unorm),
            (86, TextureFormat.TexFormatB5g5r5a1Unorm)
        };

        foreach (var (value, expected) in dxgiFormats)
            Assert.Equal(expected, ImageDds.PickTextureFormat(default, value));

        var legacyFormats = new (uint FourCc, TextureFormat Expected)[]
        {
            (ImageDds.MakeFourCC('D', 'X', 'T', '1'), TextureFormat.TexFormatBc1Unorm),
            (ImageDds.MakeFourCC('D', 'X', 'T', '3'), TextureFormat.TexFormatBc2Unorm),
            (ImageDds.MakeFourCC('D', 'X', 'T', '5'), TextureFormat.TexFormatBc3Unorm),
            (ImageDds.MakeFourCC('E', 'T', 'C', '1'), TextureFormat.TexFormatEtc2Rgb8Unorm),
            (ImageDds.MakeFourCC('E', 'T', 'C', '2'), TextureFormat.TexFormatEtc2Rgb8Unorm),
            (ImageDds.MakeFourCC('E', 'T', '2', 'A'), TextureFormat.TexFormatEtc2Rgba8Unorm),
            (ImageDds.MakeFourCC('P', 'T', 'C', '2'), EmulatedTextureFormat.PvrtcRgba2Bpp),
            (ImageDds.MakeFourCC('P', 'T', 'C', '4'), EmulatedTextureFormat.PvrtcRgba4Bpp),
            (36, TextureFormat.TexFormatRgba16Unorm),
            (113, TextureFormat.TexFormatRgba16Float),
            (116, TextureFormat.TexFormatRgba32Float)
        };

        foreach (var (fourCc, expected) in legacyFormats)
            Assert.Equal(expected, ImageDds.PickTextureFormat(new DDPixelFormat { FourCc = fourCc }, 0));

        var rgba = new DDPixelFormat
        {
            RgbBitCount = 32,
            RBitMask = 0x000000ff,
            GBitMask = 0x0000ff00,
            BBitMask = 0x00ff0000,
            RgbAlphaBitMask = 0xff000000
        };
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, ImageDds.PickTextureFormat(rgba, 0));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, ImageDds.PickTextureFormat(new DDPixelFormat { RgbBitCount = 32 }, 0));
        Assert.Equal(TextureFormat.TexFormatUnknown, ImageDds.PickTextureFormat(new DDPixelFormat { RgbBitCount = 24 }, 0));
    }

    [Fact]
    public void Component_matching_and_compressed_level_sizes_cover_supported_formats()
    {
        var rgba = new DDPixelFormat
        {
            RgbBitCount = 32,
            RBitMask = 0x000000ff,
            GBitMask = 0x0000ff00,
            BBitMask = 0x00ff0000,
            RgbAlphaBitMask = 0xff000000
        };
        var rgb565 = new DDPixelFormat
        {
            RgbBitCount = 16,
            RBitMask = 0x0000f800,
            GBitMask = 0x000007e0,
            BBitMask = 0x0000001f
        };
        var rgb5551 = new DDPixelFormat
        {
            RgbBitCount = 16,
            RBitMask = 0x00007c00,
            GBitMask = 0x000003e0,
            BBitMask = 0x0000001f,
            RgbAlphaBitMask = 0x00008000
        };

        Assert.True(ImageDds.AreTextureComponentsMatching(rgba, TextureFormat.TexFormatRgba8Unorm));
        Assert.True(ImageDds.AreTextureComponentsMatching(rgb565, TextureFormat.TexFormatB5g6r5Unorm));
        Assert.True(ImageDds.AreTextureComponentsMatching(rgb5551, TextureFormat.TexFormatB5g5r5a1Unorm));
        Assert.False(ImageDds.AreTextureComponentsMatching(rgba, TextureFormat.TexFormatUnknown));
        Assert.False(ImageDds.AreTextureComponentsMatching(default, TextureFormat.TexFormatRgba8Unorm));

        var getCompressedLevelSize = typeof(ImageDds).GetMethod(
            "GetCompressedLevelSize",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(getCompressedLevelSize);
        Assert.Equal(8, getCompressedLevelSize!.Invoke(null, [TextureFormat.TexFormatBc1Unorm, 4, 4, 1]));
        Assert.Equal(16, getCompressedLevelSize.Invoke(null, [TextureFormat.TexFormatBc3Unorm, 4, 4, 1]));
        Assert.True((int)getCompressedLevelSize.Invoke(null, [EmulatedTextureFormat.PvrtcRgb2Bpp, 1, 1, 1])! > 0);
        Assert.True((int)getCompressedLevelSize.Invoke(null, [EmulatedTextureFormat.PvrtcRgba4Bpp, 3, 5, 1])! > 0);
    }

    [Fact]
    public void Compressed_level_decompression_covers_copy_and_native_emulation_paths()
    {
        var empty = new CompressedLevel
        {
            Format = TextureFormat.TexFormatRgba8Unorm,
            Width = 2,
            Height = 2,
            Depth = 1
        };
        Assert.False(empty.Decompress(new byte[16]));

        var shortLevel = new CompressedLevel
        {
            Data = new byte[15],
            Format = TextureFormat.TexFormatRgba8Unorm,
            Width = 2,
            Height = 2,
            Depth = 1
        };
        Assert.False(shortLevel.Decompress(new byte[15]));

        var rgbaData = Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray();
        var rgba = new CompressedLevel
        {
            Data = rgbaData,
            Format = TextureFormat.TexFormatRgba8Unorm,
            Width = 2,
            Height = 2,
            Depth = 1
        };
        var rgbaOutput = new byte[16];
        Assert.True(rgba.Decompress(rgbaOutput));
        Assert.Equal(rgbaData, rgbaOutput);

        foreach (var format in new[]
        {
            TextureFormat.TexFormatBc1Unorm,
            TextureFormat.TexFormatBc2Unorm,
            TextureFormat.TexFormatBc3Unorm,
            TextureFormat.TexFormatEtc2Rgb8Unorm,
            TextureFormat.TexFormatEtc2Rgba8Unorm
        })
        {
            var level = new CompressedLevel
            {
                Data = new byte[64],
                Format = format,
                Width = 4,
                Height = 4,
                Depth = 1
            };
            Assert.True(level.Decompress(new byte[4 * 4 * 4]));
        }

        foreach (var format in new[]
        {
            EmulatedTextureFormat.PvrtcRgb2Bpp,
            EmulatedTextureFormat.PvrtcRgba2Bpp,
            EmulatedTextureFormat.PvrtcRgb4Bpp,
            EmulatedTextureFormat.PvrtcRgba4Bpp
        })
        {
            var level = new CompressedLevel
            {
                Data = new byte[256],
                Format = format,
                Width = 8,
                Height = 8,
                Depth = 1
            };
            Assert.True(level.Decompress(new byte[8 * 8 * 4]));
        }

        var unsupported = new CompressedLevel
        {
            Data = new byte[16],
            Format = TextureFormat.TexFormatUnknown,
            Width = 2,
            Height = 2,
            Depth = 1
        };
        Assert.False(unsupported.Decompress(new byte[16]));
    }
}
