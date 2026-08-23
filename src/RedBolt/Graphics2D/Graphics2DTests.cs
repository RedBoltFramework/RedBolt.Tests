// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene2D;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Graphics2DTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Graphics2DTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
        Scene2D.RegisterUrho2DLibrary(fixture.Context);
        RbfxTestData.AddResourceDirs(fixture.Resources);
        fixture.Resources.AddResourceDir(Path.Combine(RbfxTestData.RepositoryRoot, "RedBolt.2D", "bin", "CoreData.2D"));
        fixture.Resources.AddResourceDir(Path.Combine(RbfxTestData.RepositoryRoot, "RedBolt.2D", "bin", "Data.2D"));
    }

    [Fact]
    public void Module_registration_matches_unified_2d_surface()
    {
        Assert.NotNull(_fixture.Context.GetReflection<Renderer2D>());
        Assert.NotNull(_fixture.Context.GetReflection<Sprite2D>());
        Assert.NotNull(_fixture.Context.GetReflection<StaticSprite2D>());
        Assert.NotNull(_fixture.Context.GetReflection<TileMap2D>());
    }

    [Fact]
    public void Sprite_and_sprite_sheet_resources_load()
    {
        var sheet = Assert.IsType<SpriteSheet2D>(_fixture.Resources.Get<SpriteSheet2D>("Urho2D/Fish/spritesheet.xml"));
        var sprite = Assert.IsType<Sprite2D>(sheet.GetSprite("fish_blue"));
        Assert.True(sprite.GetDrawRectangle(out var drawRectangle), "Sprite draw rectangle was empty.");
        Assert.True(drawRectangle.Size.X > 0.0f, $"Sprite draw width was {drawRectangle.Size.X}.");
        Assert.True(sprite.GetTextureRectangle(out var textureRectangle), "Sprite texture rectangle was empty.");
        Assert.InRange(textureRectangle.Min.X, 0.0f, 1.0f);

        Assert.NotNull(sheet.Texture);
        Assert.True(sheet.SpriteMapping.Count >= 30, $"Sprite sheet contained {sheet.SpriteMapping.Count} sprites.");
        Assert.Same(sheet, sprite.SpriteSheet);
    }

    [Fact]
    public void Dungeon_tileset_loads_all_images_without_collision_metadata()
    {
        var tmx = Assert.IsType<TmxFile2D>(
            _fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/IsometricDemo/dungeon.tmx"));

        Assert.All(Enumerable.Range(1, 288), gid => Assert.NotNull(tmx.GetTileSprite((uint)gid)));
        Assert.All(Enumerable.Range(1, 288), gid => Assert.Null(tmx.GetTilePropertySet((uint)gid)));
    }

    [Fact]
    public void Tmx_and_pex_resources_load_complete_data()
    {
        var tmx = Assert.IsType<TmxFile2D>(
            _fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/IsometricDemo/dungeon.tmx"));
        Assert.Equal(Orientation2D.Isometric, tmx.Info.Orientation);
        Assert.Equal(20, tmx.Info.Width);
        Assert.True(tmx.NumLayers >= 6);
        Assert.NotNull(tmx.GetTileSprite(13));

        foreach (var path in new[] { "Urho2D/Smoke/blackSmoke.pex", "Urho2D/Smoke/whitePuff.pex" })
        {
            var effect = Assert.IsType<ParticleEffect2D>(_fixture.Resources.Get<ParticleEffect2D>(path));
            Assert.NotNull(effect.Sprite);
            Assert.True(effect.MaxParticles > 0);
            Assert.True(effect.ParticleLifeSpan > 0.0f);
        }

        var imp = Assert.IsType<AnimationSet2D>(_fixture.Resources.Get<AnimationSet2D>("Urho2D/imp/imp.scml"));
        Assert.True(imp.NumAnimations > 0);
        Assert.True(imp.HasAnimation("idle"));
        Assert.True(imp.HasAnimation("run"));

        foreach (var path in new[]
        {
            "Urho2D/Characters/Male/Male_0_Idle0.png",
            "Urho2D/Characters/Male/Male_0_Run0.png",
            "Urho2D/Characters/Male/Male_7_Run9.png",
        })
            Assert.NotNull(_fixture.Resources.Get<Sprite2D>(path));

        var platformer = Assert.IsType<TmxFile2D>(
            _fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/Platformer/platformer.tmx"));
        Assert.Equal(Orientation2D.Orthogonal, platformer.Info.Orientation);
        Assert.Equal(80, platformer.Info.Width);
        Assert.Equal(20, platformer.Info.Height);
        Assert.Equal(0.70f, platformer.Info.TileWidth);
        Assert.All(Enumerable.Range(1, 98), gid => Assert.NotNull(platformer.GetTileSprite((uint)gid)));
        Assert.Equal(21u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Physics")).NumObjects);
        Assert.Equal(11u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Hazards")).NumObjects);
        Assert.Equal(25u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Coins")).NumObjects);
        Assert.Equal(4u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Checkpoints")).NumObjects);
        Assert.Equal(1u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Goal")).NumObjects);
        Assert.Equal(5u, Assert.IsType<TmxObjectGroup2D>(platformer.GetLayer("Story")).NumObjects);
        Assert.NotNull(platformer.GetLayer("Scenery"));
        Assert.NotNull(platformer.GetLayer("Structures"));
        Assert.NotNull(platformer.GetLayer("Details"));
        Assert.NotNull(platformer.GetLayer("Signals"));
        Assert.NotNull(platformer.GetLayer("HazardArt"));

        var dungeonPhysics = tmx;
        Assert.Equal(20, dungeonPhysics.Info.Width);
        Assert.Equal(20, dungeonPhysics.Info.Height);
        Assert.Equal(76u, Assert.IsType<TmxObjectGroup2D>(dungeonPhysics.GetLayer("Physics")).NumObjects);
        Assert.Equal(5u, Assert.IsType<TmxObjectGroup2D>(dungeonPhysics.GetLayer("Story")).NumObjects);
        Assert.NotNull(dungeonPhysics.GetLayer("Walls"));
        Assert.NotNull(dungeonPhysics.GetLayer("RoomWalls"));
        Assert.NotNull(dungeonPhysics.GetLayer("Props"));
        Assert.Null(dungeonPhysics.GetLayer("Coins"));

        var tiledSample = Assert.IsType<TmxFile2D>(
            _fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/TiledSample/Sample.tmx"));
        var secondFloor = Assert.IsType<TmxTileLayer2D>(tiledSample.GetLayer("Second floor"));
        Assert.Equal(new Vector2f(0.0f, 0.78f), secondFloor.Offset);
    }

    [Fact]
    public void Isometric_tiles_use_tmx_layer_order_at_equal_depth()
    {
        var tmx = Assert.IsType<TmxFile2D>(
            _fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/TiledSample/Sample.tmx"));
        using var scene = new global::RedBolt.Scene.Scene();
        var tileMap = scene.CreateChild("TileMap").CreateComponent<TileMap2D>();
        tileMap.TmxFile = tmx;

        var floor = Assert.IsType<TileMapLayer2D>(tileMap.GetLayer("Floor"));
        var walls = Assert.IsType<TileMapLayer2D>(tileMap.GetLayer("Walls"));
        var floorSprite = Assert.IsType<StaticSprite2D>(floor.GetTileNode(29, 15)?.GetComponent<StaticSprite2D>());
        var wallSprite = Assert.IsType<StaticSprite2D>(walls.GetTileNode(29, 15)?.GetComponent<StaticSprite2D>());

        Assert.Equal(floorSprite.Layer, wallSprite.Layer);
        Assert.True(floorSprite.OrderInLayer < wallSprite.OrderInLayer);
    }

    [Theory]
    [InlineData(Orientation2D.Orthogonal, 2.0f, 1.76f)]
    [InlineData(Orientation2D.Isometric, 2.24f, 2.32f)]
    [InlineData(Orientation2D.Staggered, 2.0f, 0.72f)]
    [InlineData(Orientation2D.Hexagonal, 2.0f, 1.08f)]
    public void World_positions_map_to_tile_indices(Orientation2D orientation, float positionX, float positionY)
    {
        var info = new TileMapInfo2D
        {
            Orientation = orientation,
            Width = 12,
            Height = 10,
            TileWidth = 0.64f,
            TileHeight = 0.32f,
        };
        var x = -1;
        var y = -1;
        Assert.True(info.PositionToTileIndex(ref x, ref y, new Vector2f(positionX, positionY)));
        Assert.Equal(3, x);
        Assert.Equal(4, y);
    }

    [Theory]
    [InlineData(Orientation2D.Orthogonal, 1.92f, 1.6f)]
    [InlineData(Orientation2D.Isometric, 3.2f, 1.76f)]
    [InlineData(Orientation2D.Staggered, 1.92f, 0.8f)]
    [InlineData(Orientation2D.Hexagonal, 1.92f, 1.2f)]
    public void Tile_indices_map_to_rbfx_world_positions(Orientation2D orientation, float expectedX, float expectedY)
    {
        var info = new TileMapInfo2D
        {
            Orientation = orientation,
            Width = 12,
            Height = 10,
            TileWidth = 0.64f,
            TileHeight = 0.32f,
        };

        var position = info.TileIndexToPosition(3, 4);
        Assert.InRange(MathF.Abs(expectedX - position.X), 0.0f, 0.00001f);
        Assert.InRange(MathF.Abs(expectedY - position.Y), 0.0f, 0.00001f);
    }
}
