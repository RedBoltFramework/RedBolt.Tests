namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Bindings.HarfBuzz;

public sealed class HarfBuzzBindingsTests
{
    [Fact]
    public unsafe void Gpu_draw_encodes_and_recycles_an_aligned_glyph_blob()
    {
        var fontPath = Path.Combine(RbfxTestData.Data, "Fonts", "Anonymous Pro.ttf");
        var blob = HarfBuzzNativeBindings.HbBlobCreateFromFileOrFail(fontPath);
        var face = HarfBuzzNativeBindings.HbFaceCreateOrFail(blob, 0);
        var font = HarfBuzzNativeBindings.HbFontCreate(face);
        var draw = HarfBuzzNativeBindings.HbGpuDrawCreateOrFail();
        try
        {
            Assert.False(blob.IsNull);
            Assert.False(face.IsNull);
            Assert.False(font.IsNull);
            Assert.False(draw.IsNull);
            HarfBuzzNativeBindings.HbOtFontSetFuncs(font);
            var upem = checked((int)HarfBuzzNativeBindings.HbFaceGetUpem(face));
            HarfBuzzNativeBindings.HbFontSetScale(font, upem, upem);
            Assert.NotEqual(0, HarfBuzzNativeBindings.HbFontGetGlyph(font, 'A', 0, out var glyph));
            Assert.NotEqual(0, HarfBuzzNativeBindings.HbGpuDrawGlyphOrFail(draw, font, glyph));

            using var extents = new HbGlyphExtentsT();
            var encoded = HarfBuzzNativeBindings.HbGpuDrawEncode(draw, extents);
            Assert.False(encoded.IsNull);
            try
            {
                var length = HarfBuzzNativeBindings.HbBlobGetLength(encoded);
                Assert.NotEqual(0u, length);
                Assert.Equal(0u, length % 8u);
                Assert.NotEqual(IntPtr.Zero, HarfBuzzNativeBindings.HbBlobGetData(encoded, out var dataLength));
                Assert.Equal(length, dataLength);
                Assert.True(extents.Width > 0);
                Assert.True(extents.Height < 0);
            }
            finally
            {
                HarfBuzzNativeBindings.HbGpuDrawRecycleBlob(draw, encoded);
                encoded.Dispose();
            }

            HarfBuzzNativeBindings.HbGpuDrawClear(draw);
        }
        finally
        {
            if (!draw.IsNull)
                HarfBuzzNativeBindings.HbGpuDrawDestroy(draw);
            draw.Dispose();
            if (!font.IsNull)
                HarfBuzzNativeBindings.HbFontDestroy(font);
            font.Dispose();
            if (!face.IsNull)
                HarfBuzzNativeBindings.HbFaceDestroy(face);
            face.Dispose();
            if (!blob.IsNull)
                HarfBuzzNativeBindings.HbBlobDestroy(blob);
            blob.Dispose();
        }
    }

    [Fact]
    public void Raster_draw_produces_a8_glyph_image()
    {
        var fontPath = Path.Combine(RbfxTestData.Data, "Fonts", "Anonymous Pro.ttf");
        Assert.True(File.Exists(fontPath));

        var blob = HarfBuzzNativeBindings.HbBlobCreateFromFileOrFail(fontPath);
        Assert.False(blob.IsNull);
        try
        {
            var face = HarfBuzzNativeBindings.HbFaceCreateOrFail(blob, 0);
            Assert.False(face.IsNull);
            try
            {
                var font = HarfBuzzNativeBindings.HbFontCreate(face);
                Assert.False(font.IsNull);
                try
                {
                    HarfBuzzNativeBindings.HbOtFontSetFuncs(font);
                    HarfBuzzNativeBindings.HbFontSetScale(font, 32 * 64, 32 * 64);
                    Assert.NotEqual(0, HarfBuzzNativeBindings.HbFontGetGlyph(font, 'A', 0, out var glyph));

                    using var glyphExtents = new HbGlyphExtentsT();
                    Assert.NotEqual(0, HarfBuzzNativeBindings.HbFontGetGlyphExtents(font, glyph, glyphExtents));

                    var draw = HarfBuzzNativeBindings.HbRasterDrawCreateOrFail();
                    Assert.False(draw.IsNull);
                    try
                    {
                        Assert.NotEqual(0, HarfBuzzNativeBindings.HbRasterDrawSetGlyphExtents(draw, glyphExtents));
                        Assert.NotEqual(0, HarfBuzzNativeBindings.HbRasterDrawGlyphOrFail(draw, font, glyph));

                        var image = HarfBuzzNativeBindings.HbRasterDrawRender(draw);
                        Assert.False(image.IsNull);
                        try
                        {
                            using var rasterExtents = new HbRasterExtentsT();
                            HarfBuzzNativeBindings.HbRasterImageGetExtents(image, rasterExtents);
                            Assert.Equal(HbRasterFormatT.HbRasterFormatA8,
                                HarfBuzzNativeBindings.HbRasterImageGetFormat(image));
                            Assert.NotEqual(0u, rasterExtents.Width);
                            Assert.NotEqual(0u, rasterExtents.Height);
                            Assert.NotEqual(IntPtr.Zero,
                                HarfBuzzNativeBindings.HbRasterImageGetBuffer(image));
                        }
                        finally
                        {
                            HarfBuzzNativeBindings.HbRasterImageDestroy(image);
                            image.Dispose();
                        }
                    }
                    finally
                    {
                        HarfBuzzNativeBindings.HbRasterDrawDestroy(draw);
                        draw.Dispose();
                    }
                }
                finally
                {
                    HarfBuzzNativeBindings.HbFontDestroy(font);
                    font.Dispose();
                }
            }
            finally
            {
                HarfBuzzNativeBindings.HbFaceDestroy(face);
                face.Dispose();
            }
        }
        finally
        {
            HarfBuzzNativeBindings.HbBlobDestroy(blob);
            blob.Dispose();
        }
    }
}
