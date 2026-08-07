namespace RedBolt.Tests;

using System;
using System.Collections.Generic;

using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ProjectTypesTests
{
    [Fact]
    public void Project_flags_combine_and_test_independently()
    {
        var flags = ProjectFlag.None;
        flags |= ProjectFlag.ReadOnly;

        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.ReadOnly);
        Assert.Equal(ProjectFlag.None, flags & ProjectFlag.SingleProcess);

        flags |= ProjectFlag.SingleProcess;

        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.ReadOnly);
        Assert.NotEqual(ProjectFlag.None, flags & ProjectFlag.SingleProcess);
    }

    [Fact]
    public void Analyze_file_context_matches_xml_roots_case_insensitively()
    {
        var context = new AnalyzeFileContext { XmlFile = new XMLFile() };
        using (context.XmlFile.CreateRoot("Scene"))
        {
        }

        Assert.True(context.HasXMLRoot("scene"));
        Assert.True(context.HasXMLRoot(new[] { "material", "SCENE" }));
        Assert.False(context.HasXMLRoot("material"));
        Assert.False(context.HasXMLRoot(new[] { "material", "animation" }));

        context.Dispose();
        Assert.False(context.HasXMLRoot("scene"));
    }

    [Fact]
    public void Analyze_file_context_disposes_owned_files_in_reverse_order()
    {
        var order = new List<string>();
        var context = new AnalyzeFileContext
        {
            BinaryFile = new TrackingMemoryBuffer(order),
            XmlFile = new TrackingXmlFile(order),
            JsonFile = new TrackingJsonFile(order)
        };

        context.Dispose();

        Assert.Equal(new[] { "json", "xml", "binary" }, order);
        Assert.Null(context.BinaryFile);
        Assert.Null(context.XmlFile);
        Assert.Null(context.JsonFile);
    }

    private sealed class TrackingMemoryBuffer : MemoryBuffer
    {
        private readonly List<string> _order;

        public TrackingMemoryBuffer(List<string> order)
            : base(Array.Empty<byte>())
        {
            _order = order;
        }

        public override void Dispose()
        {
            _order.Add("binary");
            base.Dispose();
        }
    }

    private sealed class TrackingXmlFile : XMLFile
    {
        private readonly List<string> _order;

        public TrackingXmlFile(List<string> order)
        {
            _order = order;
        }

        public override void Dispose()
        {
            _order.Add("xml");
            base.Dispose();
        }
    }

    private sealed class TrackingJsonFile : JSONFile
    {
        private readonly List<string> _order;

        public TrackingJsonFile(List<string> order)
        {
            _order = order;
        }

        public override void Dispose()
        {
            _order.Add("json");
            base.Dispose();
        }
    }
}
