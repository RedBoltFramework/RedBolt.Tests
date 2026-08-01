namespace RedBolt.Tests;

using System.Buffers.Binary;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class ResourceCoverageGapTests
{
    [Fact]
    public void Image_storage_sampling_and_flip_boundaries_are_deterministic()
    {
        using var empty = new Image();
        Assert.False(empty.SetSize(0, 1, 1));
        Assert.False(empty.SetSize(1, 0, 1));
        Assert.False(empty.SetSize(1, 1, 0, 1));
        Assert.False(empty.SetSize(1, 1, 5));
        Assert.False(empty.Resize(1, 1));
        Assert.False(empty.FlipHorizontal());
        Assert.False(empty.FlipVertical());
        Assert.Null(empty.GetSubimage(new IntRect(0, 0, 1, 1)));
        Assert.Null(empty.ConvertToRGBA());
        Assert.Null(empty.GetNextLevel());
        Assert.Equal(0xff000000u, empty.GetPixelInt(0, 0));

        for (var components = 1; components <= 4; ++components)
        {
            using var image = new Image();
            Assert.True(image.SetSize(2, 2, components));
            image.SetData(Enumerable.Range(1, 2 * 2 * components)
                .Select(static value => (byte)value)
                .ToArray());

            image.SetPixelInt(0, 0, 0x44332211u);
            image.SetPixelInt(-1, 0, 0x99887766u);
            image.SetPixelInt(0, 0, 3, 0x99887766u);
            Assert.Equal(components switch
            {
                1 => 0xff111111u,
                2 => 0xff002211u,
                3 => 0xff332211u,
                _ => 0x44332211u
            }, image.GetPixelInt(0, 0));

            _ = image.GetPixel(-10, 10);
            image.ClearInt(0x88776655u);
            Assert.NotEmpty(image.GetData());
            image.SetData(null);
            Assert.All(image.GetData(), static value => Assert.Equal(0, value));

            using var rgba = image.ConvertToRGBA();
            Assert.NotNull(rgba);
            Assert.Equal(4, rgba!.Components);
            Assert.Equal(image.Width * image.Height * 4, rgba.GetData().Length);
        }

        using var flipped = new Image();
        Assert.True(flipped.SetSize(3, 2, 1));
        flipped.SetData([1, 2, 3, 4, 5, 6]);
        Assert.True(flipped.FlipHorizontal());
        Assert.Equal([3, 2, 1, 6, 5, 4], flipped.GetData());
        Assert.True(flipped.FlipVertical());
        Assert.Equal([6, 5, 4, 3, 2, 1], flipped.GetData());

        using var volume = new Image();
        Assert.True(volume.SetSize(2, 2, 2, 1));
        volume.SetData([0, 1, 2, 3, 4, 5, 6, 7]);
        Assert.False(volume.FlipHorizontal());
        Assert.False(volume.FlipVertical());
        Assert.Equal(7u, volume.GetPixelInt(1, 1, 1) & 0xffu);
    }

    [Fact]
    public void Image_mips_subimages_resize_and_rgba_conversion_cover_edge_paths()
    {
        using var horizontal = new Image();
        Assert.True(horizontal.SetSize(4, 1, 1));
        horizontal.SetData([0, 10, 20, 30]);
        using var horizontalMip = horizontal.GetNextLevel();
        Assert.Equal([5, 25], horizontalMip!.GetData());

        using var vertical = new Image();
        Assert.True(vertical.SetSize(1, 4, 1));
        vertical.SetData([0, 10, 20, 30]);
        using var verticalMip = vertical.GetNextLevel();
        Assert.Equal([5, 25], verticalMip!.GetData());

        using var volume = new Image();
        Assert.True(volume.SetSize(2, 2, 2, 1));
        volume.SetData([0, 1, 2, 3, 4, 5, 6, 7]);
        using var volumeMip = volume.GetNextLevel();
        Assert.Equal([3], volumeMip!.GetData());

        using var one = new Image();
        Assert.True(one.SetSize(1, 1, 1));
        Assert.Null(one.GetNextLevel());

        var conversionData = new byte[][]
        {
            [7],
            [7, 9],
            [7, 8, 9],
            [7, 8, 9, 10]
        };
        var conversionExpected = new byte[][]
        {
            [7, 7, 7, 255],
            [7, 7, 7, 9],
            [7, 8, 9, 255],
            [7, 8, 9, 10]
        };
        for (var i = 0; i < conversionData.Length; ++i)
        {
            using var source = new Image();
            Assert.True(source.SetSize(1, 1, i + 1));
            source.SetData(conversionData[i]);
            using var converted = source.ConvertToRGBA();
            Assert.Equal(conversionExpected[i], converted!.GetData());
        }

        using var sourceImage = new Image();
        Assert.True(sourceImage.SetSize(2, 2, 4));
        sourceImage.SetData([
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16
        ]);

        using var destination = new Image();
        Assert.True(destination.SetSize(4, 4, 4));
        Assert.True(destination.SetSubimage(sourceImage, new IntRect(1, 1, 3, 3)));
        Assert.Equal(sourceImage.GetPixelInt(0, 0), destination.GetPixelInt(1, 1));
        Assert.False(destination.SetSubimage(sourceImage, new IntRect(-1, 0, 1, 2)));

        using var resizedSource = new Image();
        Assert.True(resizedSource.SetSize(1, 1, 4));
        resizedSource.SetData([20, 30, 40, 50]);
        Assert.True(destination.SetSubimage(resizedSource, new IntRect(0, 0, 2, 2)));
        Assert.Equal(resizedSource.GetPixelInt(0, 0), destination.GetPixelInt(0, 0));

        using var mismatched = new Image();
        Assert.True(mismatched.SetSize(1, 1, 3));
        Assert.False(destination.SetSubimage(mismatched, new IntRect(0, 0, 1, 1)));
        Assert.False(destination.Resize(0, 2));
        Assert.True(destination.Resize(2, 1));
        Assert.Equal(new Vector3i(2, 1, 1), destination.GetSize());

        destination.PrecalculateLevels();
        var levels = new List<Image>();
        destination.GetLevels(levels);
        Assert.NotEmpty(levels);
        destination.CleanupLevels();
        destination.GetLevels(levels);
        Assert.Single(levels);
    }

    [Fact]
    public void Image_compressed_header_failures_and_valid_level_edges_are_covered()
    {
        Assert.False(LoadImage(CreateKtx(endianness: 0)));
        Assert.False(LoadImage(CreateKtx(type: 1)));
        Assert.False(LoadImage(CreateKtx(depth: 2)));
        Assert.False(LoadImage(CreateKtx(faces: 2)));
        Assert.False(LoadImage(CreateKtx(mipmaps: 0)));
        Assert.False(LoadImage(CreateKtx(internalFormat: 0x1234)));
        Assert.False(LoadImage(CreateKtx(levelSize: 100)));

        Assert.False(LoadImage(CreatePvr(pixelFormat: 0xdeadbeef)));
        Assert.False(LoadImage(CreatePvr(depth: 2)));
        Assert.False(LoadImage(CreatePvr(faces: 2)));
        Assert.False(LoadImage(CreatePvr(mipmaps: 0)));

        using var truncatedRiff = new MemoryBuffer([(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W']);
        using var riffImage = new Image();
        Assert.False(riffImage.BeginLoad(truncatedRiff));

        using var invalidRiff = new MemoryBuffer([
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'N', (byte)'O', (byte)'P', (byte)'E'
        ]);
        Assert.False(riffImage.BeginLoad(invalidRiff));

        using var image = new Image();
        Assert.True(image.BeginLoad(new MemoryBuffer(CreateKtx())));
        Assert.True(image.IsCompressed());
        Assert.Equal(TextureFormat.TexFormatBc1Unorm, image.GetGPUFormat());
        Assert.Equal(default, image.GetCompressedLevel(-1));
        Assert.Equal(default, image.GetCompressedLevel(99));
        Assert.Null(image.GetDecompressedImageLevel(99));
        using var subimage = image.GetSubimage(new IntRect(0, 0, 4, 4));
        Assert.Null(subimage);
    }

    [Fact]
    public void JSONValue_type_mutation_and_variant_boundaries_are_covered()
    {
        foreach (var type in Enum.GetValues<JSONValueType>())
            Assert.Equal(type, JSONValue.GetValueTypeFromName(JSONValue.GetValueTypeName(type).ToLowerInvariant()));
        foreach (var type in Enum.GetValues<JSONNumberType>())
            Assert.Equal(type, JSONValue.GetNumberTypeFromName(JSONValue.GetNumberTypeName(type).ToUpperInvariant()));
        Assert.Equal(JSONValueType.Null, JSONValue.GetValueTypeFromName(null));
        Assert.Equal(JSONValueType.Null, JSONValue.GetValueTypeFromName("missing"));
        Assert.Equal(JSONNumberType.NaN, JSONValue.GetNumberTypeFromName("missing"));

        using var value = new JSONValue();
        Assert.Equal(12, value.GetInt(12));
        Assert.Equal(13u, value.GetUInt(13));
        Assert.Equal(1.5f, value.GetFloat(1.5f));
        Assert.Equal(2.5, value.GetDouble(2.5));
        Assert.Equal("fallback", value.GetString("fallback"));
        Assert.False(value.GetBool());
        Assert.Empty(value.GetArray());
        Assert.Empty(value.GetObject());
        using var fallback = new CString("fallback");
        Assert.Same(fallback, value.GetCString(fallback));

        value.Pop();
        value.Insert(0, new JSONValue(1));
        value.Erase(0);
        value.Push(new JSONValue(1));
        value.Resize(3);
        value[1].Set("middle");
        value[0] = new JSONValue(2);
        Assert.Equal(3, value.Size());
        Assert.Same(JSONValue.Empty, value.Get(-1));
        Assert.Same(JSONValue.Empty, value.Get(99));
        value.Erase(0, 2);
        value.Pop();
        Assert.Empty(value.GetArray());

        Assert.False(value.Contains("missing"));
        Assert.False(value.Erase("missing"));
        value["answer"].Set(42);
        Assert.True(value.Contains("answer"));
        Assert.Equal(42, value.Get("answer").GetInt());
        Assert.True(value.Erase("answer"));
        Assert.False(value.Erase("answer"));

        using var nullValue = new JSONValue();
        using var boolValue = new JSONValue(true);
        using var numberValue = new JSONValue(3.5);
        using var stringValue = new JSONValue("text");
        using var arrayValue = new JSONValue([new JSONValue(1)]);
        using var objectValue = new JSONValue(new Dictionary<string, JSONValue>
        {
            ["value"] = new JSONValue(1)
        });
        Assert.False(nullValue.Equals(new JSONValue()));
        Assert.True(boolValue.Equals(new JSONValue(true)));
        Assert.True(numberValue.Equals(new JSONValue(3.5)));
        Assert.True(stringValue.Equals(new JSONValue("text")));
        Assert.True(arrayValue.Equals(new JSONValue([new JSONValue(1)])));
        Assert.True(objectValue.Equals(new JSONValue(new Dictionary<string, JSONValue>
        {
            ["value"] = new JSONValue(1)
        })));
        Assert.NotEqual(0, objectValue.GetHashCode());

        using var parsed = new JSONValue();
        Assert.True(JSONFile.ParseJSON("[true, 2147483648, 3.5, \"text\", null]", parsed));
        Assert.True(parsed[0].GetBool());
        Assert.Equal(2147483648u, parsed[1].GetUInt());
        Assert.Equal(3.5, parsed[2].GetDouble());
        Assert.True(parsed[4].IsNull);

        using var variantHolder = new JSONValue();
        variantHolder.SetVariant(new Variant(new VariantVector { new(7), new("text") }));
        var variant = variantHolder.GetVariant();
        Assert.Equal(VariantType.VariantVector, variant.Type);
        Assert.Equal(7, variant.GetVariantVector()[0].GetInt());
        variant.Dispose();

        using var stringVectorHolder = new JSONValue();
        stringVectorHolder.SetVariant(new Variant(new StringVector(["a", "b"])));
        var stringVector = stringVectorHolder.GetVariant();
        Assert.Equal(["a", "b"], stringVector.GetStringVector());
        stringVector.Dispose();

        using var refHolder = new JSONValue();
        var context = new Context();
        refHolder.SetVariant(new Variant(ResourceRef.Parse("Material;Materials/Test")), context);
        Assert.Equal("Materials/Test", refHolder.GetVariant().GetResourceRef().Name);
        context.Dispose();

        using var wrongType = new JSONValue(1);
        Assert.Empty(wrongType.GetVariantVector());
        Assert.Empty(wrongType.GetVariantMap());
        Assert.Empty(wrongType.GetStringVariantMap());
        Assert.Equal(default, wrongType.GetVariantValue(VariantType.Custom));
    }

    [Fact]
    public void PList_values_and_parser_failures_cover_all_managed_kinds()
    {
        using var none = new PListValue();
        using var integer = new PListValue(7);
        using var boolean = new PListValue(true);
        using var real = new PListValue(2.5f);
        using var text = new PListValue("text");
        Assert.True((bool)integer);
        Assert.False((bool)none);
        Assert.False((bool)(PListValue?)null);
        Assert.NotEqual(none.GetHashCode(), integer.GetHashCode());
        Assert.Equal(integer, new PListValue(7));
        Assert.Equal(boolean, new PListValue(true));
        Assert.Equal(real, new PListValue(2.5f));
        Assert.Equal(text, new PListValue("text"));

        using var map = new PListValue(new Dictionary<string, PListValue>
        {
            ["one"] = new PListValue(1),
            ["two"] = new PListValue("two")
        });
        using var vector = new PListValue([new PListValue(1), new PListValue(false)]);
        Assert.NotEqual(0, map.GetHashCode());
        Assert.NotEqual(0, vector.GetHashCode());
        Assert.Equal(2, map.ValueMap.Count);
        Assert.Equal(2, vector.ValueVector.Count);

        var mapValues = map.ConvertToValueMap();
        map.SetValueMap(mapValues);
        var vectorValues = vector.ConvertToValueVector();
        vector.SetValueVector(vectorValues);

        using var signedGeometry = new PListValue("{{-1,+2},{3,4}}");
        Assert.Equal(new IntRect(-1, 2, 2, 6), signedGeometry.IntRect);
        Assert.Equal(new Vector2i(-1, 2), new PListValue("{-1,+2}").IntVector2);
        Assert.Equal(new Vector3i(-1, 2, 3), new PListValue("{-1,+2,3}").IntVector3);

        using var malformed = new PListValue("not numbers");
        Assert.Equal(default, malformed.IntRect);
        Assert.Equal(default, malformed.IntVector2);
        Assert.Equal(default, malformed.IntVector3);

        Assert.True(LoadPList("<plist version=\"1.0\"><dict><key>value</key><string>ok</string><key>value</key><integer>9</integer><key>items</key><array><true/><false/><real>1.25</real></array></dict></plist>", out var plist));
        using (plist)
        {
            Assert.Equal(9, plist.Root["value"].Int);
            Assert.Equal(3, plist.Root["items"].ValueVector.Count);
            Assert.True(plist.Root["items"].ValueVector[0].Bool);
            Assert.False(plist.Root["items"].ValueVector[1].Bool);
        }

        Assert.False(LoadPList("<root />", out var wrongRoot));
        wrongRoot.Dispose();
        Assert.False(LoadPList("<plist><dict><key>missing</key></dict></plist>", out var missingValue));
        missingValue.Dispose();
        Assert.False(LoadPList("<plist><dict><key>unknown</key><date>today</date></dict></plist>", out var unknownType));
        unknownType.Dispose();
        Assert.False(LoadPList("<plist><dict>", out var malformedXml));
        malformedXml.Dispose();
    }

    [Fact]
    public void XML_elements_cover_null_typed_attribute_and_copy_paths()
    {
        var nullElement = new XMLElement();
        Assert.False(nullElement.Remove());
        Assert.False(nullElement.RemoveChildren());
        Assert.False(nullElement.RemoveAttribute());
        Assert.False(nullElement.SetAttribute("name", "value"));
        Assert.False(nullElement.SetAttribute("value"));
        Assert.False(nullElement.AppendChild(nullElement));
        Assert.True(nullElement.GetChild().IsNull);
        Assert.True(nullElement.GetNext().IsNull);
        Assert.True(nullElement.GetParent().IsNull);
        Assert.Empty(nullElement.GetAttributeNames());

        var xml = new XMLFile();
        Assert.True(xml.FromString("<root><nested>text</nested><!--comment--></root>"));
        var root = xml.GetRoot();
        Assert.Equal("text", root.Value);
        var nested = root.GetChild("nested");
        Assert.True(nested.SetValue("changed"));
        Assert.Equal("changed", nested.Value);
        Assert.True(root.GetOrCreateChild("created").NotNull);
        Assert.Same(root.GetChild("nested").Node, root.GetOrCreateChild("nested").Node);

        root.SetAttribute("mixed", "MiXeD");
        root.SetAttribute("flag", "1");
        root.SetAttribute("badFlag", "not-bool");
        root.SetAttribute("buffer", "1 bad 255");
        root.SetAttribute("vec1", "1");
        root.SetAttribute("vec2", "1 2");
        root.SetAttribute("vec3", "1 2 3");
        root.SetAttribute("vec4", "1 2 3 4");
        root.SetAttribute("color3", "0.1 0.2 0.3");
        root.SetAttribute("color4", "0.1 0.2 0.3 0.4");
        root.SetAttribute("rect", "1 2 3 4");
        root.SetAttribute("intRect", "1 2 3 4");
        root.SetAttribute("int2", "1 2");
        root.SetAttribute("int3", "1 2 3");
        root.SetAttribute("quat", "0 0 0 1");
        root.SetAttribute("matrix3", "1 2 3 4 5 6 7 8 9");
        root.SetAttribute("matrix3x4", "1 2 3 4 5 6 7 8 9 10 11 12");
        root.SetAttribute("matrix4", "1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16");

        Assert.Equal("mixed", root.GetAttributeLower("mixed"));
        Assert.Equal("MIXED", root.GetAttributeUpper("mixed"));
        using var cString = root.GetAttributeCString("mixed");
        Assert.Equal("MiXeD", cString.ConvertToString());
        Assert.True(root.GetBool("flag"));
        Assert.False(root.GetBool("badFlag"));
        Assert.Equal([1, 0, 255], root.GetBuffer("buffer"));
        Assert.Equal(new Vector4f(1, 0, 0, 0), root.GetVector("vec1"));
        Assert.Equal(new Vector4f(1, 2, 0, 0), root.GetVector("vec2"));
        Assert.Equal(new Vector4f(1, 2, 3, 0), root.GetVector("vec3"));
        Assert.Equal(new Vector4f(1, 2, 3, 4), root.GetVector("vec4"));
        Assert.Equal(new IntRect(1, 2, 3, 4), root.GetIntRect("intRect"));
        Assert.Equal(new Vector2i(1, 2), root.GetIntVector2("int2"));
        Assert.Equal(new Vector3i(1, 2, 3), root.GetIntVector3("int3"));
        Assert.Equal(new Rect(1, 2, 3, 4), root.GetRect("rect"));
        Assert.Equal(new Quaternion(0, 0, 0, 1), root.GetQuaternion("quat"));
        _ = root.GetColor("color3");
        _ = root.GetColor("color4");
        _ = root.GetMatrix3("matrix3");
        _ = root.GetMatrix3x4("matrix3x4");
        _ = root.GetMatrix4("matrix4");

        Assert.Equal(VariantType.Float, root.GetVectorVariant("vec1").Type);
        Assert.Equal(VariantType.Vector2, root.GetVectorVariant("vec2").Type);
        Assert.Equal(VariantType.Vector3, root.GetVectorVariant("vec3").Type);
        Assert.Equal(VariantType.Vector4, root.GetVectorVariant("vec4").Type);
        Assert.Equal(VariantType.None, root.GetVectorVariant("missing").Type);
        Assert.True(root.SetVectorVariant("float", new Variant(1.0f)));
        Assert.True(root.SetVectorVariant("vector2", new Variant(new Vector2f(1, 2))));
        Assert.True(root.SetVectorVariant("vector3", new Variant(new Vector3f(1, 2, 3))));
        Assert.True(root.SetVectorVariant("vector4", new Variant(new Vector4f(1, 2, 3, 4))));
        Assert.True(root.SetVectorVariant("matrix3", new Variant(new Matrix3())));
        Assert.True(root.SetVectorVariant("matrix3x4", new Variant(new Matrix3x4())));
        Assert.True(root.SetVectorVariant("matrix4", new Variant(new Matrix4())));
        Assert.False(root.SetVectorVariant("bool", new Variant(true)));

        var child = root.CreateChild("variant");
        child.SetString("name", "named");
        child.SetVariant(new Variant(42));
        var hashChild = root.CreateChild("variant");
        hashChild.SetUInt("hash", new StringHash("hashed").Value);
        hashChild.SetVariant(new Variant(7));
        root.CreateChild("variant").SetVariant(new Variant(9));
        Assert.Equal(42, root.GetVariantMap()[new StringHash("named")].GetInt());
        Assert.Equal(7, root.GetVariantMap()[new StringHash("hashed")].GetInt());

        var source = new XMLFile();
        Assert.True(source.FromString("<source><item value=\"1\"/></source>"));
        Assert.True(root.AppendChild(source.GetRoot().GetChild("item"), asCopy: true));
        Assert.True(root.AppendChild(source.GetRoot().GetChild("item")));
        Assert.True(root.RemoveChild("item"));
        Assert.True(root.RemoveChildren("variant"));

        var attribute = root.SelectSingle("@mixed");
        Assert.True(attribute.SetAttribute("updated"));
        Assert.Equal("updated", root.GetAttribute("mixed"));
        Assert.True(attribute.RemoveAttribute());
        Assert.False(root.HasAttribute("mixed"));

        Assert.False(root.RemoveAttribute("missing"));
    }

    [Fact]
    public void Resource_reference_and_cube_projection_edges_are_covered()
    {
        var context = new Context();
        var resourceRef = ResourceRef.Parse("Material;Materials/Test");
        Assert.True(resourceRef == resourceRef.Clone());
        Assert.False(resourceRef != resourceRef.Clone());
        Assert.Equal("Materials/Test", resourceRef.ToString(context).Split(';')[1]);
        Assert.NotEqual(resourceRef, ResourceRef.Empty);
        Assert.Equal(ResourceRef.Empty, ResourceRef.Parse(string.Empty));

        var list = ResourceRefList.Parse("Material;A;B");
        Assert.True(list == list.Clone());
        Assert.False(list != list.Clone());
        Assert.NotEqual(list, ResourceRefList.Empty);
        Assert.Equal("A", list.Names[0]);
        Assert.Equal(";A;B", list.ToString(context));
        context.Dispose();

        var uv = new Vector2f(0.25f, 0.75f);
        foreach (var face in Enum.GetValues<CubeMapFace>())
        {
            if (face == CubeMapFace.MaxCubeMapFaces)
                continue;
            Assert.NotEqual(Vector3f.Zero, ImageCube.ProjectUVOnCube(face, uv));
        }
        Assert.Equal(Vector3f.Zero, ImageCube.ProjectUVOnCube(CubeMapFace.MaxCubeMapFaces, uv));

        var directions = new (Vector3f Direction, CubeMapFace Face)[]
        {
            (new Vector3f(1, 0, 0), CubeMapFace.PositiveX),
            (new Vector3f(-1, 0, 0), CubeMapFace.NegativeX),
            (new Vector3f(0, 1, 0), CubeMapFace.PositiveY),
            (new Vector3f(0, -1, 0), CubeMapFace.NegativeY),
            (new Vector3f(0.1f, 0.2f, 1), CubeMapFace.PositiveZ),
            (new Vector3f(-0.1f, -0.2f, -1), CubeMapFace.NegativeZ)
        };
        foreach (var (direction, face) in directions)
        {
            var projection = ImageCube.ProjectDirectionOnFace(direction);
            Assert.Equal(face, projection.Face);
            Assert.InRange(projection.Uv.X, 0.0f, 1.0f);
            Assert.InRange(projection.Uv.Y, 0.0f, 1.0f);
        }
    }

    private static bool LoadImage(byte[] data)
    {
        using var source = new MemoryBuffer(data);
        using var image = new Image();
        return image.BeginLoad(source);
    }

    private static byte[] CreateKtx(
        uint endianness = 0x04030201,
        uint type = 0,
        uint format = 0,
        uint internalFormat = 0x83f1,
        uint depth = 1,
        uint faces = 1,
        uint mipmaps = 1,
        uint levelSize = 8)
    {
        var data = new byte[76];
        new byte[]
        {
            0xab, (byte)'K', (byte)'T', (byte)'X', (byte)' ', (byte)'1', (byte)'1', 0xbb,
            0x0d, 0x0a, 0x1a, 0x0a
        }.CopyTo(data, 0);
        WriteUInt32(data, 12, endianness);
        WriteUInt32(data, 16, type);
        WriteUInt32(data, 24, format);
        WriteUInt32(data, 28, internalFormat);
        WriteUInt32(data, 36, 4);
        WriteUInt32(data, 40, 4);
        WriteUInt32(data, 44, depth);
        WriteUInt32(data, 52, faces);
        WriteUInt32(data, 56, mipmaps);
        WriteUInt32(data, 64, levelSize);
        for (var i = 68; i < data.Length; ++i)
            data[i] = (byte)(i - 67);
        return data;
    }

    private static byte[] CreatePvr(
        uint pixelFormat = 7,
        uint depth = 1,
        uint faces = 1,
        uint mipmaps = 1)
    {
        var data = new byte[60];
        data[0] = (byte)'P';
        data[1] = (byte)'V';
        data[2] = (byte)'R';
        data[3] = 3;
        WriteUInt32(data, 8, pixelFormat);
        WriteUInt32(data, 24, 4);
        WriteUInt32(data, 28, 4);
        WriteUInt32(data, 32, depth);
        WriteUInt32(data, 40, faces);
        WriteUInt32(data, 44, mipmaps);
        for (var i = 52; i < data.Length; ++i)
            data[i] = (byte)(i - 51);
        return data;
    }

    private static void WriteUInt32(byte[] data, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);

    private static bool LoadPList(string source, out PListFile plist)
    {
        plist = new PListFile();
        using var buffer = new MemoryBuffer(source);
        return plist.BeginLoad(buffer);
    }
}
