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
        var sprite = Assert.IsType<Sprite2D>(_fixture.Resources.Get<Sprite2D>("Urho2D/Aster.png"));
        Assert.True(sprite.GetDrawRectangle(out var drawRectangle), "Sprite draw rectangle was empty.");
        Assert.True(drawRectangle.Size.X > 0.0f, $"Sprite draw width was {drawRectangle.Size.X}.");
        Assert.True(sprite.GetTextureRectangle(out var textureRectangle), "Sprite texture rectangle was empty.");
        Assert.InRange(textureRectangle.Min.X, 0.0f, 1.0f);

        var sheet = Assert.IsType<SpriteSheet2D>(_fixture.Resources.Get<SpriteSheet2D>("Urho2D/Orc/Orc.xml"));
        Assert.NotNull(sheet.Texture);
        Assert.True(sheet.SpriteMapping.Count >= 10, $"Sprite sheet contained {sheet.SpriteMapping.Count} sprites.");
        Assert.NotNull(sheet.GetSprite("orc_0004_body"));
    }

    [Fact]
    public void Tmx_and_pex_resources_load_complete_data()
    {
        var tmx = Assert.IsType<TmxFile2D>(_fixture.Resources.Get<TmxFile2D>("Urho2D/Tilesets/atrium.tmx"));
        Assert.Equal(Orientation2D.Isometric, tmx.Info.Orientation);
        Assert.Equal(20, tmx.Info.Width);
        Assert.True(tmx.NumLayers >= 5);
        Assert.NotNull(tmx.GetTileSprite(1));

        var effect = Assert.IsType<ParticleEffect2D>(_fixture.Resources.Get<ParticleEffect2D>("Urho2D/sun.pex"));
        Assert.NotNull(effect.Sprite);
        Assert.True(effect.MaxParticles > 0);
        Assert.True(effect.ParticleLifeSpan > 0.0f);
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
