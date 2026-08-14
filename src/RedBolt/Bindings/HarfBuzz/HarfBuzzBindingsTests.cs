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
        var blob = HarfBuzz.HbBlobCreateFromFileOrFail(fontPath);
        var face = HarfBuzz.HbFaceCreateOrFail(blob, 0);
        var font = HarfBuzz.HbFontCreate(face);
        var draw = HarfBuzz.HbGpuDrawCreateOrFail();
        try
        {
            Assert.False(blob.IsNull);
            Assert.False(face.IsNull);
            Assert.False(font.IsNull);
            Assert.False(draw.IsNull);
            HarfBuzz.HbOtFontSetFuncs(font);
            var upem = checked((int)HarfBuzz.HbFaceGetUpem(face));
            HarfBuzz.HbFontSetScale(font, upem, upem);
            Assert.NotEqual(0, HarfBuzz.HbFontGetGlyph(font, 'A', 0, out var glyph));
            Assert.NotEqual(0, HarfBuzz.HbGpuDrawGlyphOrFail(draw, font, glyph));

            using var extents = new HbGlyphExtentsT();
            var encoded = HarfBuzz.HbGpuDrawEncode(draw, extents);
            Assert.False(encoded.IsNull);
            try
            {
                var length = HarfBuzz.HbBlobGetLength(encoded);
                Assert.NotEqual(0u, length);
                Assert.Equal(0u, length % 8u);
                Assert.NotEqual(IntPtr.Zero, HarfBuzz.HbBlobGetData(encoded, out var dataLength));
                Assert.Equal(length, dataLength);
                Assert.True(extents.Width > 0);
                Assert.True(extents.Height < 0);
            }
            finally
            {
                HarfBuzz.HbGpuDrawRecycleBlob(draw, encoded);
                encoded.Dispose();
            }

            HarfBuzz.HbGpuDrawClear(draw);
        }
        finally
        {
            if (!draw.IsNull)
                HarfBuzz.HbGpuDrawDestroy(draw);
            draw.Dispose();
            if (!font.IsNull)
                HarfBuzz.HbFontDestroy(font);
            font.Dispose();
            if (!face.IsNull)
                HarfBuzz.HbFaceDestroy(face);
            face.Dispose();
            if (!blob.IsNull)
                HarfBuzz.HbBlobDestroy(blob);
            blob.Dispose();
        }
    }

    [Fact]
    public void Raster_draw_produces_a8_glyph_image()
    {
        var fontPath = Path.Combine(RbfxTestData.Data, "Fonts", "Anonymous Pro.ttf");
        Assert.True(File.Exists(fontPath));

        var blob = HarfBuzz.HbBlobCreateFromFileOrFail(fontPath);
        Assert.False(blob.IsNull);
        try
        {
            var face = HarfBuzz.HbFaceCreateOrFail(blob, 0);
            Assert.False(face.IsNull);
            try
            {
                var font = HarfBuzz.HbFontCreate(face);
                Assert.False(font.IsNull);
                try
                {
                    HarfBuzz.HbOtFontSetFuncs(font);
                    HarfBuzz.HbFontSetScale(font, 32 * 64, 32 * 64);
                    Assert.NotEqual(0, HarfBuzz.HbFontGetGlyph(font, 'A', 0, out var glyph));

                    using var glyphExtents = new HbGlyphExtentsT();
                    Assert.NotEqual(0, HarfBuzz.HbFontGetGlyphExtents(font, glyph, glyphExtents));

                    var draw = HarfBuzz.HbRasterDrawCreateOrFail();
                    Assert.False(draw.IsNull);
                    try
                    {
                        Assert.NotEqual(0, HarfBuzz.HbRasterDrawSetGlyphExtents(draw, glyphExtents));
                        Assert.NotEqual(0, HarfBuzz.HbRasterDrawGlyphOrFail(draw, font, glyph));

                        var image = HarfBuzz.HbRasterDrawRender(draw);
                        Assert.False(image.IsNull);
                        try
                        {
                            using var rasterExtents = new HbRasterExtentsT();
                            HarfBuzz.HbRasterImageGetExtents(image, rasterExtents);
                            Assert.Equal(HbRasterFormatT.HbRasterFormatA8,
                                HarfBuzz.HbRasterImageGetFormat(image));
                            Assert.NotEqual(0u, rasterExtents.Width);
                            Assert.NotEqual(0u, rasterExtents.Height);
                            Assert.NotEqual(IntPtr.Zero,
                                HarfBuzz.HbRasterImageGetBuffer(image));
                        }
                        finally
                        {
                            HarfBuzz.HbRasterImageDestroy(image);
                            image.Dispose();
                        }
                    }
                    finally
                    {
                        HarfBuzz.HbRasterDrawDestroy(draw);
                        draw.Dispose();
                    }
                }
                finally
                {
                    HarfBuzz.HbFontDestroy(font);
                    font.Dispose();
                }
            }
            finally
            {
                HarfBuzz.HbFaceDestroy(face);
                face.Dispose();
            }
        }
        finally
        {
            HarfBuzz.HbBlobDestroy(blob);
            blob.Dispose();
        }
    }
}
