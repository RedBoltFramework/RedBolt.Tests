// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneRendererToTextureTests
{
    public SceneRendererToTextureTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Custom_backbuffer_tracks_size_activity_and_texture_parameters()
    {
        using var backbuffer = new CustomBackbufferTexture();
        Assert.Equal(Vector2i.Zero, backbuffer.TextureSize);
        Assert.False(backbuffer.IsActive);

        backbuffer.SetTextureSize(Vector2i.Zero);
        Assert.Equal(Vector2i.Zero, backbuffer.TextureSize);

        backbuffer.SetTextureSize(new Vector2i(64, 32));
        backbuffer.SetActive(true);
        backbuffer.Update();

        Assert.Equal(new Vector2i(64, 32), backbuffer.TextureSize);
        Assert.Equal(new Vector2i(64, 32), backbuffer.Texture.GetSize());
        Assert.True(backbuffer.Texture.IsRenderTarget());
        Assert.True(backbuffer.IsActive);

        backbuffer.SetActive(false);
        Assert.False(backbuffer.IsActive);
    }

    [Fact]
    public void Scene_renderer_owns_camera_and_connects_its_viewport()
    {
        using var scene = new Scene();
        using var renderer = new SceneRendererToTexture(scene);

        Assert.Same(scene, renderer.Scene);
        Assert.NotNull(renderer.Camera);
        Assert.NotNull(renderer.CameraNode);

        var position = new Vector3f(1.0f, 2.0f, 3.0f);
        var rotation = new Quaternion(10.0f, 20.0f, 30.0f);
        renderer.CameraNode!.SetWorldTransform(position, rotation);
        Assert.Equal(position, renderer.CameraPosition);
        Assert.True(renderer.CameraRotation.Equivalent(rotation));

        using var renderSurface = new RenderSurface(renderer.Texture, 0);
        var setupViewport = typeof(SceneRendererToTexture).GetMethod(
            "SetupViewport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setupViewport);
        setupViewport.Invoke(renderer, [renderer, renderSurface]);

        var viewport = Assert.IsType<Viewport>(renderSurface.GetViewport(0));
        Assert.Same(scene, viewport.GetScene());
        Assert.Same(renderer.Camera, viewport.GetCamera());
    }

    private readonly HeadlessEngineFixture _fixture;
}
