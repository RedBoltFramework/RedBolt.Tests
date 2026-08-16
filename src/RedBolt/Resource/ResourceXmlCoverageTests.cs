// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Text;
using System.Xml.Linq;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class ResourceXmlCoverageTests
{
    [Fact]
    public void XMLFile_and_XMLElement_validate_in_memory_boundaries()
    {
        using var xml = new XMLFile { Name = "named.xml" };

        using (var empty = new MemoryBuffer(Array.Empty<byte>()))
        {
            empty.SetName("empty.xml");
            Assert.False(xml.BeginLoad(empty));
        }

        using (var shortSource = new ShortReadDeserializer(Encoding.UTF8.GetBytes("<root />")))
            Assert.False(xml.BeginLoad(shortSource));

        Assert.False(xml.FromString(string.Empty));
        Assert.False(xml.FromString("<root>"));
        Assert.True(xml.FromString(
            "<?xml version=\"1.0\"?><!DOCTYPE root><!--outside--><root flag=\"1\"><!--inside--><?pi data?><child>text</child><deep><inner><leaf>value</leaf></inner></deep></root>"));
        Assert.Null(xml.Document.Declaration);

        var root = xml.GetRoot("root");
        Assert.True(root.NotNull);
        Assert.True(xml.GetRoot("wrong").IsNull);
        Assert.Equal("text", root.GetChild("child").Value);
        Assert.Equal("value", root.GetChild("deep").Value);
        Assert.Equal("root", root.Name);
        root.Name = "renamed";
        Assert.Equal("renamed", root.Name);
        root.Name = "root";

        var child = root.GetChild("child");
        Assert.True(child.SetValue("changed"));
        Assert.Equal("changed", child.Value);
        var emptyChild = root.CreateChild("empty");
        Assert.True(emptyChild.SetValue("first"));
        Assert.True(emptyChild.SetValue("second"));
        Assert.Equal("second", emptyChild.Value);
        var cdataChild = root.CreateChild("cdata");
        ((XElement)cdataChild.Node!).Add(new XCData("raw"));
        Assert.True(cdataChild.SetValue("ignored-by-first-node"));
        Assert.Equal("raw", cdataChild.Value);

        root.SetAttribute("boolTrue", "true");
        root.SetAttribute("boolOne", "1");
        root.SetAttribute("boolBad", "not-bool");
        root.SetAttribute("buffer", "1 bad 255");
        root.SetAttribute("float", "bad");
        root.SetAttribute("double", "bad");
        root.SetAttribute("uint", "bad");
        root.SetAttribute("int", "bad");
        root.SetAttribute("uint64", "bad");
        root.SetAttribute("int64", "bad");
        root.SetAttribute("color", "1 2");
        root.SetAttribute("intRect", "1 2");
        root.SetAttribute("int2", "1");
        root.SetAttribute("int3", "1 2");
        root.SetAttribute("rect", "1 2");
        root.SetAttribute("quat", "1 2 3");
        root.SetAttribute("vec2", "1");
        root.SetAttribute("vec3", "1 2");
        root.SetAttribute("vec4", "1 2 3");
        root.SetAttribute("matrix3", "1 2 3");
        root.SetAttribute("matrix3x4", "1 2 3");
        root.SetAttribute("matrix4", "1 2 3");

        Assert.True(root.GetBool("boolTrue"));
        Assert.True(root.GetBool("boolOne"));
        Assert.False(root.GetBool("boolBad"));
        Assert.Equal(new byte[] { 1, 0, 255 }, root.GetBuffer("buffer"));
        Assert.Empty(root.GetBuffer("missing"));
        Span<byte> destination = stackalloc byte[3];
        Assert.True(root.GetBuffer("buffer", destination));
        Span<byte> tooSmall = stackalloc byte[2];
        Assert.False(root.GetBuffer("buffer", tooSmall));
        Assert.Equal(0.0f, root.GetFloat("float"));
        Assert.Equal(0.0, root.GetDouble("double"));
        Assert.Equal(0u, root.GetUInt("uint"));
        Assert.Equal(0, root.GetInt("int"));
        Assert.Equal(0ul, root.GetUInt64("uint64"));
        Assert.Equal(0L, root.GetInt64("int64"));
        Assert.Equal(Color.White, root.GetColor("color"));
        Assert.Equal(default, root.GetIntRect("intRect"));
        Assert.Equal(default, root.GetIntVector2("int2"));
        Assert.Equal(default, root.GetIntVector3("int3"));
        Assert.Equal(new Rect(), root.GetRect("rect"));
        Assert.Equal(Quaternion.IDENTITY, root.GetQuaternion("quat"));
        Assert.Equal(Vector2f.Zero, root.GetVector2("vec2"));
        Assert.Equal(Vector3f.Zero, root.GetVector3("vec3"));
        Assert.Equal(Vector4f.Zero, root.GetVector4("vec4"));
        Assert.Equal(Vector4f.Zero, root.GetVector("missing"));
        Assert.Equal(VariantType.None, root.GetVectorVariant("missing").Type);
        Assert.Equal(Matrix3.Identity, root.GetMatrix3("matrix3"));
        Assert.Equal(Matrix3x4.Identity, root.GetMatrix3x4("matrix3x4"));
        Assert.Equal(Matrix4.Identity, root.GetMatrix4("matrix4"));

        Assert.True(root.SetBool("setBool", false));
        Assert.True(root.SetFloat("setFloat", 1.5f));
        Assert.True(root.SetDouble("setDouble", 2.5));
        Assert.True(root.SetUInt("setUInt", 3));
        Assert.True(root.SetInt("setInt", -4));
        Assert.True(root.SetUInt64("setUInt64", 5));
        Assert.True(root.SetInt64("setInt64", -6));
        Assert.True(root.SetQuaternion("setQuat", Quaternion.IDENTITY));
        Assert.True(root.SetRect("setRect", new Rect(1, 2, 3, 4)));
        Assert.True(root.SetIntRect("setIntRect", new IntRect(1, 2, 3, 4)));
        Assert.True(root.SetIntVector2("setInt2", new Vector2i(1, 2)));
        Assert.True(root.SetIntVector3("setInt3", new Vector3i(1, 2, 3)));
        Assert.True(root.SetMatrix3("setMatrix3", Matrix3.Identity));
        Assert.True(root.SetMatrix3x4("setMatrix3x4", Matrix3x4.Identity));
        Assert.True(root.SetMatrix4("setMatrix4", Matrix4.Identity));
        Assert.True(root.SetColor("setColor", Color.White));
        Assert.True(root.SetVector2("setVec2", new Vector2f(1, 2)));
        Assert.True(root.SetVector3("setVec3", new Vector3f(1, 2, 3)));
        Assert.True(root.SetVector4("setVec4", new Vector4f(1, 2, 3, 4)));
        Assert.True(root.SetBoundingBox(new BoundingBox(Vector3f.Zero, Vector3f.One)));
        Assert.True(root.SetBuffer("setBuffer", new byte[] { 4, 5 }));
        Assert.True(root.SetAttribute("nullable", null!));
        Assert.Equal(string.Empty, root.GetAttribute("nullable"));

        using var supportedVector = new Variant(new Vector3f(1, 2, 3));
        using var unsupportedVector = new Variant(true);
        Assert.True(root.SetVectorVariant("supportedVector", supportedVector));
        Assert.False(root.SetVectorVariant("unsupportedVector", unsupportedVector));

        var source = new XMLFile();
        var sourceRoot = source.CreateRoot("source");
        var sourceElement = sourceRoot.CreateChild("element");
        ((XElement)sourceRoot.Node!).Add(
            new XCData("cdata"),
            new XText("text"),
            new XComment("comment"),
            new XProcessingInstruction("pi", "data"));
        Assert.True(root.AppendChild(sourceElement, asCopy: true));
        Assert.True(root.AppendChild(sourceElement));
        Assert.True(root.AppendChild(sourceRoot.GetChild(), asCopy: true));
        Assert.True(root.AppendChild(sourceRoot.GetChild().GetNext(), asCopy: true));
        Assert.True(root.AppendChild(sourceRoot.GetChild().GetNext().GetNext(), asCopy: true));
        Assert.True(root.AppendChild(sourceRoot.GetChild().GetNext().GetNext().GetNext(), asCopy: true));
        Assert.False(root.AppendChild(new XMLElement()));

        Assert.True(root.HasChild("element"));
        Assert.True(root.GetChild("element").GetNext("element").NotNull);
        Assert.True(root.GetChild("element").GetParent().NotNull);
        Assert.True(root.RemoveChild("element"));
        Assert.False(root.RemoveChild(new XMLElement()));
        Assert.True(root.RemoveChildren("cdata"));
        Assert.True(root.RemoveChildren());
        var removedNode = sourceRoot.GetChild();
        Assert.True(removedNode.Remove());
        Assert.False(removedNode.Remove());

        root.SetAttribute("attribute", "old");
        var attribute = root.SelectSingle("@attribute");
        Assert.Equal("attribute", attribute.Name);
        Assert.True(attribute.SetAttribute("new"));
        Assert.True(attribute.SetAttribute("ignored", "newer"));
        Assert.Equal("newer", root.GetAttribute("attribute"));
        Assert.True(attribute.RemoveAttribute());
        Assert.False(root.RemoveAttribute("missing"));
        Assert.False(root.SelectSingle("missing").NotNull);

        var mapElement = root.CreateChild("map");
        var unnamedVariant = mapElement.CreateChild("variant");
        unnamedVariant.SetVariant(new Variant(1));
        var namedVariant = mapElement.CreateChild("variant");
        namedVariant.SetString("name", "named");
        namedVariant.SetVariant(new Variant(2));
        var hashedVariant = mapElement.CreateChild("variant");
        hashedVariant.SetUInt("hash", new StringHash("hashed").Value);
        hashedVariant.SetVariant(new Variant(3));
        using var variantMap = mapElement.GetVariantMap();
        Assert.Equal(2, variantMap.Count);
        Assert.Equal(2, variantMap[new StringHash("named")].GetInt());
        Assert.Equal(3, variantMap[new StringHash("hashed")].GetInt());

        var nested = root.CreateChild("nested");
        nested.CreateChild("string").SetString("value", "one");
        nested.CreateChild("string").SetString("value", "two");
        var strings = nested.GetStringVector();
        Assert.Equal(["one", "two"], strings);
        Assert.True(nested.SetStringVector(new StringVector(["three"])));
        Assert.Equal("three", nested.GetStringVector()[0]);

        using var badCustom = Variant.MakeCustomValue(new object());
        Assert.False(nested.SetVariantValue(badCustom));
        using var failingCustom = Variant.MakeCustomValue<Serializable>(new FailingSerializable());
        Assert.False(nested.SetVariantValue(failingCustom));
        Assert.Equal(VariantType.None, nested.GetVariantValue(VariantType.Custom).Type);

        using var output = new VectorBuffer();
        Assert.True(xml.Save(output, "  "));
        Assert.Contains("<root", Encoding.UTF8.GetString(output.Buffer), StringComparison.Ordinal);
        Assert.False(xml.Save(new ShortWriteSerializer(), "  "));

        Assert.Equal("<?xml version=\"1.0\"?>\n", new XMLFile().ToString());
        Assert.True(xml.GetOrCreateRoot("root").NotNull);
        Assert.Equal("newRoot", xml.GetOrCreateRoot("newRoot").Name);
    }

    [Fact]
    public void XMLFile_patch_positions_and_attribute_validation_stay_in_memory()
    {
        using var xml = LoadXml("<root flag=\"old\"><before/><target>old</target><after/></root>");
        using var patch = LoadXml("""
            <patch>
              <add sel="/root" type="@added">yes</add>
              <add sel="/root" type="@empty" />
              <add sel="/root/@flag">ignored</add>
              <add sel="/root/@flag" type="@bad">ignored</add>
              <add sel="/root/target/text()" pos="after">ignored</add>
              <add sel="/root" pos="append">tail</add>
              <add sel="/root" pos="append">again</add>
              <add sel="/root" pos="prepend">head</add>
              <add sel="/root" pos="prepend">again</add>
              <add sel="/root/target" pos="before">before-text</add>
              <add sel="/root/target" pos="after">after-text</add>
              <add sel="/root/target" pos="invalid"><ignored /></add>
              <add sel="/root" pos="append"><added-node /></add>
              <add sel="/root" type="element"><ignored /></add>
              <replace sel="/root/@flag">new</replace>
              <replace sel="/root/target"><replacement /></replace>
              <replace sel="/root/after" />
              <remove sel="/root/before" />
              <remove sel="/root/@added" />
              <unknown sel="/root" />
              <replace sel="/root/missing"><ignored /></replace>
              <add><ignored /></add>
            </patch>
            """);

        xml.Patch(patch);

        var root = xml.GetRoot();
        Assert.Equal("new", root.GetAttribute("flag"));
        Assert.False(root.HasAttribute("added"));
        Assert.False(root.HasAttribute("empty"));
        Assert.False(root.HasChild("before"));
        Assert.False(root.HasChild("after"));
        Assert.True(root.HasChild("replacement"));
        Assert.True(root.HasChild("added-node"));
        Assert.Contains("tailagain", root.Node!.ToString(), StringComparison.Ordinal);

        using var cdataXml = LoadXml("<root><![CDATA[old]]><item /></root>");
        using var cdataPatch = LoadXml("""
            <patch>
              <add sel="/root/item" pos="before"><![CDATA[before]]></add>
              <add sel="/root" pos="prepend"><![CDATA[prepend]]></add>
            </patch>
            """);
        cdataXml.Patch(cdataPatch.GetRoot());
        Assert.Contains("prependoldbefore", cdataXml.Document.Root!.ToString(), StringComparison.Ordinal);

        new XMLFile().Patch(new XMLElement());
        using var emptyPatch = LoadXml("<patch><add sel=\"/root\"><x /></add></patch>");
        using var emptyXml = new XMLFile();
        emptyXml.Patch(emptyPatch);
        Assert.True(emptyXml.GetRoot().IsNull);
    }

    [Fact]
    public void XPathQuery_covers_scalar_node_set_and_variable_validation()
    {
        using var xml = LoadXml("<root><item id=\"1\"> 12.5 </item><item id=\"2\">text</item></root>");
        var root = xml.GetRoot();

        using var trueQuery = new XPathQuery("true()");
        using var falseQuery = new XPathQuery("false()");
        using var numberQuery = new XPathQuery("1");
        using var zeroQuery = new XPathQuery("0");
        using var nanQuery = new XPathQuery("number('NaN')");
        using var stringQuery = new XPathQuery("string('text')");
        using var emptyStringQuery = new XPathQuery("string('')");
        using var nodesQuery = new XPathQuery("item");
        using var emptyNodesQuery = new XPathQuery("missing");

        Assert.True(trueQuery.EvaluateToBool(root));
        Assert.False(falseQuery.EvaluateToBool(root));
        Assert.True(numberQuery.EvaluateToBool(root));
        Assert.False(zeroQuery.EvaluateToBool(root));
        Assert.False(nanQuery.EvaluateToBool(root));
        Assert.True(stringQuery.EvaluateToBool(root));
        Assert.False(emptyStringQuery.EvaluateToBool(root));
        Assert.True(nodesQuery.EvaluateToBool(root));
        Assert.False(emptyNodesQuery.EvaluateToBool(root));

        Assert.Equal("true", trueQuery.EvaluateToString(root));
        Assert.Equal("false", falseQuery.EvaluateToString(root));
        Assert.Equal("1", numberQuery.EvaluateToString(root));
        Assert.Equal("0", zeroQuery.EvaluateToString(root));
        Assert.Equal("NaN", nanQuery.EvaluateToString(root));
        Assert.Equal("text", stringQuery.EvaluateToString(root));
        Assert.Equal(" 12.5 ", nodesQuery.EvaluateToString(root));
        Assert.Equal(string.Empty, emptyNodesQuery.EvaluateToString(root));

        using var positiveInfinity = new XPathQuery("1 div 0");
        using var negativeInfinity = new XPathQuery("-1 div 0");
        using var negativeZero = new XPathQuery("0 div -1");
        Assert.Equal("Infinity", positiveInfinity.EvaluateToString(root));
        Assert.Equal("-Infinity", negativeInfinity.EvaluateToString(root));
        Assert.Equal("0", negativeZero.EvaluateToString(root));

        using var stringNumber = new XPathQuery("string('  -12.5  ')");
        using var plusNumber = new XPathQuery("string('+12.5')");
        using var badNumber = new XPathQuery("string('not-a-number')");
        using var emptyNumber = new XPathQuery("string('')");
        Assert.Equal(-12.5f, stringNumber.EvaluateToFloat(root));
        Assert.True(float.IsNaN(plusNumber.EvaluateToFloat(root)));
        Assert.True(float.IsNaN(badNumber.EvaluateToFloat(root)));
        Assert.True(float.IsNaN(emptyNumber.EvaluateToFloat(root)));
        Assert.Equal(12.5f, nodesQuery.EvaluateToFloat(root));
        Assert.True(float.IsNaN(emptyNodesQuery.EvaluateToFloat(root)));
        Assert.Equal(0.0f, new XPathQuery().EvaluateToFloat(root));

        using var resultSet = root.Select("item");
        using var resultSetQuery = new XPathQuery("$items[@id='2']", "items:ResultSet");
        Assert.True(resultSetQuery.SetVariable("items", resultSet));
        using var evaluatedResultSet = resultSetQuery.Evaluate(root);
        Assert.Single(evaluatedResultSet);
        Assert.Equal("2", evaluatedResultSet.FirstResult().GetAttribute("id"));
        Assert.Equal("2", root.SelectSinglePrepared(resultSetQuery).GetAttribute("id"));
        Assert.False(resultSetQuery.SetVariable("items", new XPathResultSet()));
        resultSet.Dispose();
        using var evaluatedAfterDispose = resultSetQuery.Evaluate(root);
        Assert.Single(evaluatedAfterDispose);

        using var query = new XPathQuery();
        Assert.True(query.SetQuery("item", "flag:Bool, weight:Float, text:String, items:ResultSet, malformed, :String", bind: false));
        Assert.True(query.SetVariable("flag", true));
        Assert.True(query.SetVariable("weight", 2.5f));
        Assert.True(query.SetVariable("text", "value"));
        Assert.True(query.SetVariable("items", root.Select("item")));
        Assert.False(query.SetVariable("flag", 2.5f));
        Assert.True(query.SetQuery("item", "same:Bool,same:Bool", bind: false));
        Assert.False(query.SetQuery("item", "same:Bool,same:String", bind: false));
        Assert.False(query.SetQuery("item", "unknown:Unknown", bind: false));
        Assert.True(query.SetQuery("item", "malformed", bind: false));
        query.Bind();
        Assert.Equal(2, query.Evaluate(root).Count);

        using var prepared = new XPathQuery("item[@id='1']");
        Assert.Equal("1", root.SelectSinglePrepared(prepared).GetAttribute("id"));
        Assert.True(root.SelectSinglePrepared(new XPathQuery("missing")).IsNull);
        Assert.True(new XMLElement().Select("item").Empty());
        Assert.True(new XMLElement().SelectSingle("item").IsNull);
        Assert.True(new XMLElement().SelectPrepared(new XPathQuery("item")).Empty());

        using var copied = Assert.IsType<XPathResultSet>(root.Select("item").Clone());
        Assert.Equal(2, copied.Count);
        Assert.Equal("1", copied.FirstResult().GetAttribute("id"));
        Assert.Equal("2", copied.FirstResult().NextResult().GetAttribute("id"));
        Assert.True(copied.FirstResult().NextResult().NextResult().IsNull);
        Assert.Equal(2, copied.ToArray().Length);
        Assert.True(copied[-1].IsNull);
        Assert.True(copied[99].IsNull);
        using var emptyResults = new XPathResultSet();
        Assert.True(emptyResults[0].IsNull);
        emptyResults.Dispose();
        Assert.True(emptyResults[0].IsNull);
    }

    [Fact]
    public void JSONValue_covers_type_mutation_and_resource_validation()
    {
        foreach (var type in Enum.GetValues<JSONValueType>())
            Assert.Equal(type, JSONValue.GetValueTypeFromName(JSONValue.GetValueTypeName(type)));
        foreach (var type in Enum.GetValues<JSONNumberType>())
            Assert.Equal(type, JSONValue.GetNumberTypeFromName(JSONValue.GetNumberTypeName(type)));
        Assert.Equal(JSONValueType.Null, JSONValue.GetValueTypeFromName(null));
        Assert.Equal(JSONValueType.Null, JSONValue.GetValueTypeFromName("missing"));
        Assert.Equal(JSONNumberType.NaN, JSONValue.GetNumberTypeFromName("missing"));

        using var nullValue = new JSONValue();
        using var boolValue = new JSONValue(true);
        using var intValue = new JSONValue(-7);
        using var uintValue = new JSONValue(7u);
        using var floatValue = new JSONValue(1.5f);
        using var doubleValue = new JSONValue(2.5);
        using var stringValue = new JSONValue("text");
        using var arrayValue = new JSONValue([new JSONValue(1)]);
        using var objectValue = new JSONValue(new Dictionary<string, JSONValue>
        {
            ["value"] = new JSONValue(1)
        });

        Assert.False(nullValue.Equals(nullValue));
        Assert.False(nullValue.Equals((JSONValue?)null));
        Assert.False(nullValue.Equals(intValue));
        Assert.True(boolValue.Equals(new JSONValue(true)));
        Assert.False(intValue.Equals(uintValue));
        Assert.True(floatValue.Equals(new JSONValue(1.5)));
        Assert.True(stringValue.Equals(new JSONValue("text")));
        Assert.True(arrayValue.Equals(new JSONValue([new JSONValue(1)])));
        Assert.True(objectValue.Equals(new JSONValue(new Dictionary<string, JSONValue>
        {
            ["value"] = new JSONValue(1)
        })));
        Assert.False(objectValue.Equals(new JSONValue(new Dictionary<string, JSONValue>
        {
            ["other"] = new JSONValue(1)
        })));
        Assert.NotEqual(0, objectValue.GetHashCode());
        Assert.Equal("text", stringValue.GetString());
        Assert.Equal("fallback", nullValue.GetString("fallback"));
        Assert.Equal(2.5, doubleValue.GetDouble());
        Assert.Equal(9, nullValue.GetInt(9));
        Assert.Empty(nullValue.GetArray());
        Assert.Empty(nullValue.GetObject());

        nullValue.Pop();
        nullValue.Insert(0, new JSONValue(1));
        nullValue.Erase(0);
        nullValue.Push(new JSONValue(1));
        nullValue.Resize(3);
        nullValue[1].Set("middle");
        nullValue[0] = new JSONValue(2);
        Assert.Equal(3, nullValue.Size());
        nullValue.Resize(1);
        nullValue.Erase(0);
        nullValue.Resize(0);
        Assert.Empty(nullValue.GetArray());
        Assert.False(nullValue.Contains("missing"));
        Assert.False(nullValue.Erase("missing"));
        nullValue["answer"].Set(42);
        nullValue["answer"] = new JSONValue(43);
        Assert.Equal(43, nullValue.Get("answer").GetInt());
        Assert.True(nullValue.Erase("answer"));
        Assert.False(nullValue.Erase("answer"));
        nullValue.Clear();

        using var array = new JSONValue(JSONValueType.Array);
        array.Push(new JSONValue("first"));
        array.Insert(0, new JSONValue("zero"));
        array.Set(array.GetArray());
        array[1] = array[1];
        Assert.Throws<ArgumentOutOfRangeException>(() => array[-1] = new JSONValue());
        Assert.Throws<ArgumentNullException>(() => array.Push(null!));
        Assert.Throws<ArgumentNullException>(() => array.Set((IEnumerable<JSONValue>)null!));
        Assert.Throws<ArgumentNullException>(() => array.Set((IReadOnlyDictionary<string, JSONValue>)null!));
        Assert.Throws<ArgumentNullException>(() => array.Set((JSONValue)null!));
        Assert.Throws<ArgumentNullException>(() => array.ReplaceRecursively(null!));

        using var recursive = new JSONValue();
        using var replacement = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"keep\":1,\"replace\":2}}", recursive));
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"replace\":3,\"add\":4}}", replacement));
        recursive.ReplaceRecursively(replacement);
        Assert.Equal(1, recursive.Get("outer").Get("keep").GetInt());
        Assert.Equal(3, recursive.Get("outer").Get("replace").GetInt());
        Assert.Equal(4, recursive.Get("outer").Get("add").GetInt());
        recursive.ReplaceRecursively(new JSONValue(5));
        Assert.Equal(5, recursive.GetInt());

        using var json = new JSONFile();
        Assert.True(json.FromString("{/* comment */\"bool\":true,\"int\":-1,\"uint\":2147483648,\"real\":1.25,\"text\":\"value\",\"array\":[null, false],}"));
        Assert.True(json.Root.Get("bool").GetBool());
        Assert.Equal(-1, json.Root.Get("int").GetInt());
        Assert.Equal(2147483648u, json.Root.Get("uint").GetUInt());
        Assert.Equal(1.25, json.Root.Get("real").GetDouble());
        Assert.True(json.Root.Get("array")[0].IsNull);
        Assert.False(json.Root.Get("array")[1].GetBool());
        Assert.Contains("2147483648", json.ToString(), StringComparison.Ordinal);
        using var invalidType = new JSONValue((JSONValueType)99);
        Assert.Equal("null", invalidType.ToString());

        using var context = new Context();
        using var resourceRefValue = new JSONValue();
        using var resourceRef = new Variant(ResourceRef.Parse("Material;Materials/Test"));
        resourceRefValue.SetVariantValue(resourceRef, context);
        Assert.Equal(";Materials/Test", resourceRefValue.GetString());
        using var resourceRefResult = resourceRefValue.GetVariantValue(VariantType.ResourceRef);
        Assert.Equal("Materials/Test", resourceRefResult.GetResourceRef().Name);
        using var resourceRefListValue = new JSONValue();
        using var resourceRefList = new Variant(ResourceRefList.Parse("Material;A;B"));
        resourceRefListValue.SetVariantValue(resourceRefList, context);
        Assert.Equal("A", resourceRefListValue.GetVariantValue(VariantType.ResourceRefList).GetResourceRefList().Names[0]);
        using var missingContext = new JSONValue();
        missingContext.SetVariantValue(resourceRef);
        Assert.True(missingContext.IsNull);

        using var variantVectorValue = new JSONValue();
        using var variantVector = new Variant(new VariantVector { new(7), new("text") });
        variantVectorValue.SetVariantValue(variantVector);
        Assert.Equal(7, variantVectorValue.GetVariantVector()[0].GetInt());
        using var stringVectorValue = new JSONValue();
        using var stringVector = new Variant(new StringVector(["a", "b"]));
        stringVectorValue.SetVariantValue(stringVector);
        Assert.Equal(["a", "b"], stringVectorValue.GetVariantValue(VariantType.StringVector).GetStringVector());

        using var mapValue = new JSONValue();
        using var variantMap = new Variant(new VariantMap { [new StringHash("named")] = new Variant(3) });
        mapValue.SetVariantValue(variantMap);
        Assert.Equal(3, mapValue.GetVariantMap()[new StringHash("named")].GetInt());
        using var stringMapValue = new JSONValue();
        using var stringMap = new Variant(new StringVariantMap { ["named"] = new Variant(4) });
        stringMapValue.SetVariantValue(stringMap);
        Assert.Equal(4, stringMapValue.GetStringVariantMap()["named"].GetInt());

        using var primitive = new JSONValue();
        using var boolVariant = new Variant(true);
        using var intVariant = new Variant(3);
        using var int64Variant = new Variant(4L);
        using var floatVariant = new Variant(5.0f);
        using var doubleVariant = new Variant(6.0);
        using var textVariant = new Variant("text");
        primitive.SetVariantValue(boolVariant);
        Assert.True(primitive.GetVariantValue(VariantType.Bool).GetBool());
        primitive.SetVariantValue(intVariant);
        Assert.Equal(3, primitive.GetVariantValue(VariantType.Int).GetInt());
        primitive.SetVariantValue(int64Variant);
        Assert.Equal(4L, primitive.GetVariantValue(VariantType.Int64).GetInt64());
        primitive.SetVariantValue(floatVariant);
        Assert.Equal(5.0f, primitive.GetVariantValue(VariantType.Float).GetFloat());
        primitive.SetVariantValue(doubleVariant);
        Assert.Equal(6.0, primitive.GetVariantValue(VariantType.Double).GetDouble());
        primitive.SetVariantValue(textVariant);
        Assert.Equal("text", primitive.GetVariantValue(VariantType.String).GetString());
        primitive.Set("1 2 3 4");
        Assert.Equal(VariantType.Color, primitive.GetVariantValue(VariantType.Color).Type);

        Assert.Empty(new JSONValue(1).GetVariantVector());
        Assert.Empty(new JSONValue(1).GetVariantMap());
        Assert.Empty(new JSONValue(1).GetStringVariantMap());
        Assert.Equal(VariantType.None, new JSONValue(1).GetVariantValue(VariantType.Custom).Type);
        using var customObject = new JSONValue(JSONValueType.Object);
        Assert.Equal(VariantType.None, customObject.GetVariantValue(VariantType.Custom, context).Type);
        customObject["type"].Set("MissingSerializable");
        Assert.Equal(VariantType.None, customObject.GetVariantValue(VariantType.Custom, context).Type);
        using var unsupportedCustom = Variant.MakeCustomValue(new object());
        using var customTarget = new JSONValue();
        customTarget.SetVariantValue(unsupportedCustom);
        Assert.True(customTarget.IsNull);
        using var failingTarget = new JSONValue();
        using var failingVariant = Variant.MakeCustomValue<Serializable>(new FailingSerializable());
        failingTarget.SetVariantValue(failingVariant);
        Assert.True(failingTarget.IsNull);
    }

    private static XMLFile LoadXml(string source)
    {
        var result = new XMLFile();
        Assert.True(result.FromString(source));
        return result;
    }

    private sealed class ShortWriteSerializer : Serializer
    {
        public override uint Write(ReadOnlySpan<byte> data) => 0;
    }

    private sealed class ShortReadDeserializer(byte[] data) : Deserializer((uint)data.Length)
    {
        public override uint Read(Span<byte> destination) => 0;

        public override uint Seek(uint position)
        {
            Position = global::System.Math.Min(position, Size);
            return Position;
        }

        public override uint Write(ReadOnlySpan<byte> data) => 0;
    }

    private sealed class FailingSerializable : Serializable
    {
        public override bool SaveXML(XMLElement destination) => false;

        public override bool SaveJSON(JSONValue destination) => false;
    }
}
