namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Bindings.XAtlas;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

public sealed class LightmapUVGeneratorTests
{
    [NativeBindingFact]
    public void GenerateLightmapUV_generates_second_uv_channel_and_metadata()
    {
        using var modelView = new ModelView();
        var geometryLodView = new GeometryLODView
        {
            PrimitiveType = PrimitiveType.TriangleList
        };
        geometryLodView.VertexFormat.Position = VertexElementType.Vector3;
        geometryLodView.VertexFormat.Normal = VertexElementType.Vector3;
        geometryLodView.Vertices.Add(CreateVertex(new Vector3f(-1.0f, 0.0f, -1.0f)));
        geometryLodView.Vertices.Add(CreateVertex(new Vector3f(1.0f, 0.0f, -1.0f)));
        geometryLodView.Vertices.Add(CreateVertex(new Vector3f(1.0f, 0.0f, 1.0f)));
        geometryLodView.Vertices.Add(CreateVertex(new Vector3f(-1.0f, 0.0f, 1.0f)));
        geometryLodView.Indices.AddRange([0, 1, 2, 0, 2, 3]);
        var geometryView = new GeometryView();
        geometryView.Lods.Add(geometryLodView);
        modelView.SetGeometries([geometryView]);
        var settings = new LightmapUVGenerationSettings
        {
            TexelPerUnit = 4.0f,
            UvChannel = 1
        };

        Assert.True(LightmapUVGenerator.GenerateLightmapUV(modelView, settings));

        var result = modelView.GetGeometries()[0].Lods[0];
        Assert.Equal(VertexElementType.Vector2, result.VertexFormat.Uv[1]);
        Assert.Equal(6, result.Indices.Count);
        Assert.NotEmpty(result.Vertices);
        foreach (var vertex in result.Vertices)
        {
            Assert.InRange(vertex.Uv[1].X, 0.0f, 1.0f);
            Assert.InRange(vertex.Uv[1].Y, 0.0f, 1.0f);
        }

        var lightmapSize = modelView.GetMetadata(LightmapUVGenerationSettings.LightmapSizeKey).GetIntVector2();
        Assert.True(lightmapSize.X > 0);
        Assert.True(lightmapSize.Y > 0);
        Assert.Equal(4.0f, modelView.GetMetadata(LightmapUVGenerationSettings.LightmapDensityKey).GetFloat());
        Assert.False(modelView.GetMetadata(LightmapUVGenerationSettings.LightmapSharedUV).GetBool());
    }

    [NativeBindingFact]
    public void GenerateLightmapUV_rejects_indices_outside_the_vertex_array()
    {
        using var modelView = new ModelView();
        var lod = new GeometryLODView { PrimitiveType = PrimitiveType.TriangleList };
        lod.VertexFormat.Position = VertexElementType.Vector3;
        lod.VertexFormat.Normal = VertexElementType.Vector3;
        lod.Vertices.Add(CreateVertex(Vector3f.Zero));
        lod.Vertices.Add(CreateVertex(Vector3f.Right));
        lod.Vertices.Add(CreateVertex(Vector3f.Up));
        lod.Indices.AddRange([0, 1, 3]);
        var geometry = new GeometryView();
        geometry.Lods.Add(lod);
        modelView.SetGeometries([geometry]);

        Assert.False(LightmapUVGenerator.GenerateLightmapUV(modelView, new LightmapUVGenerationSettings()));
    }

    [NativeBindingFact]
    public void GenerateLightmapUV_reports_a_forced_native_mesh_add_failure()
    {
        using var modelView = new ModelView();
        var lod = new GeometryLODView { PrimitiveType = PrimitiveType.TriangleList };
        lod.VertexFormat.Position = VertexElementType.Vector3;
        lod.VertexFormat.Normal = VertexElementType.Vector3;
        lod.Vertices.Add(CreateVertex(Vector3f.Zero));
        lod.Vertices.Add(CreateVertex(Vector3f.Right));
        lod.Vertices.Add(CreateVertex(Vector3f.Up));
        lod.Indices.AddRange([0, 1, 2]);
        var geometry = new GeometryView();
        geometry.Lods.Add(lod);
        modelView.SetGeometries([geometry]);

        var method = typeof(LightmapUVGenerator).GetMethod(
            "GenerateLightmapUV", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, [typeof(ModelView), typeof(LightmapUVGenerationSettings), typeof(bool)], modifiers: null)!;
        Assert.False((bool)method.Invoke(null, [modelView, new LightmapUVGenerationSettings(), true])!);
    }

    [Fact]
    public void Lightmap_mesh_add_failure_is_reported()
    {
        var method = typeof(LightmapUVGenerator).GetMethod(
            "TryAddMesh", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var meshDecl = new MeshDecl();

        Assert.False((bool)method.Invoke(null, [null, meshDecl, true])!);
    }

    private static ModelVertex CreateVertex(Vector3f position)
    {
        var vertex = new ModelVertex();
        vertex.SetPosition(position);
        vertex.SetNormal(Vector3f.Up);
        return vertex;
    }

}
