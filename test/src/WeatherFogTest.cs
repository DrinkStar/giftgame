// Original — weather fog table must stay a pure, testable table
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Decision 5 contract — WeatherService owns fog, DayNightService only ever
///   disables it at night. The numeric table lives in a pure resolver so the
///   4 weather x 2 day/night combinations are pinned without a scene tree.
/// </summary>
public class WeatherFogTest : TestClass
{
  public WeatherFogTest(Node testScene) : base(testScene) { }

  private const float Tolerance = 0.000001f;

  [Test]
  public void Resolve_ClearDayKeepsThinHaze()
  {
    var fog = WeatherFog.Resolve(WeatherType.Clear, night: false);

    fog.Enabled.ShouldBeTrue();
    fog.Density.ShouldBe(0.00022f, Tolerance);
    fog.SunScatter.ShouldBe(0.12f, Tolerance);
    fog.AerialPerspective.ShouldBe(0.55f, Tolerance);
    fog.LightColor.IsEqualApprox(new Color(0.72f, 0.78f, 0.85f)).ShouldBeTrue();
  }

  [Test]
  public void Resolve_MatchesDesignTable()
  {
    AssertFog(WeatherType.Cloudy, 0.0009f, 0.08f, 0.45f, new Color(0.7f, 0.7f, 0.75f));
    AssertFog(WeatherType.Rain, 0.0024f, 0.04f, 0.3f, new Color(0.5f, 0.5f, 0.55f));
    AssertFog(WeatherType.Storm, 0.004f, 0.02f, 0.15f, new Color(0.3f, 0.3f, 0.35f));
  }

  [Test]
  public void Resolve_StormIsThickestAndClearestIsThinnest()
  {
    var clear = WeatherFog.Resolve(WeatherType.Clear, night: false);
    var cloudy = WeatherFog.Resolve(WeatherType.Cloudy, night: false);
    var rain = WeatherFog.Resolve(WeatherType.Rain, night: false);
    var storm = WeatherFog.Resolve(WeatherType.Storm, night: false);

    (cloudy.Density > clear.Density).ShouldBeTrue();
    (rain.Density > cloudy.Density).ShouldBeTrue();
    (storm.Density > rain.Density).ShouldBeTrue();
    (storm.AerialPerspective < clear.AerialPerspective).ShouldBeTrue();
  }

  [Test]
  public void Resolve_NightDisablesFogForEveryWeather()
  {
    foreach (var weather in new[]
    {
      WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.Storm
    })
    {
      WeatherFog.Resolve(weather, night: false).Enabled.ShouldBeTrue();
      WeatherFog.Resolve(weather, night: true).Enabled.ShouldBeFalse();
    }
  }

  private static void AssertFog(
    WeatherType weather,
    float density,
    float scatter,
    float aerial,
    Color lightColor
  )
  {
    var fog = WeatherFog.Resolve(weather, night: false);

    fog.Enabled.ShouldBeTrue();
    fog.Density.ShouldBe(density, Tolerance);
    fog.SunScatter.ShouldBe(scatter, Tolerance);
    fog.AerialPerspective.ShouldBe(aerial, Tolerance);
    fog.LightColor.IsEqualApprox(lightColor).ShouldBeTrue();
  }
}
