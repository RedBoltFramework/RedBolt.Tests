// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TextureCubeSphericalHarmonicsTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TextureCubeSphericalHarmonicsTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Texture_cube_keeps_spherical_harmonics_from_the_xml_and_zone_uses_them()
    {
        _fixture.ResetGraphics();
        var texture = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");

        var cached = texture.GetSphericalHarmonics();
        Assert.NotNull(cached);

        var zone = new Zone();
        zone.SetAmbientColor(Color.Black);
        zone.SetBackgroundBrightness(1.0f);
        zone.SetZoneTexture(texture);
        zone.UpdateCachedData();

        var actual = zone.GetAmbientAndBackgroundLighting().Evaluate(Vector3f.Up);
        Assert.True(actual.ApproximatelyEquivalent(cached.Value.Evaluate(Vector3f.Up), 0.001f));

        // The cache does not depend on the zone texture image data staying loaded.
        zone.SetZoneTexture(null);
        zone.SetZoneTexture(texture);
        zone.UpdateCachedData();
        Assert.True(zone.GetAmbientAndBackgroundLighting().Evaluate(Vector3f.Up)
            .ApproximatelyEquivalent(actual, 0.0001f));
    }
}
