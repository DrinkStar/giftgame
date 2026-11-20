// Original — pure fog table for WeatherService (Decision 5)
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Applied fog values for one weather / day-night state. Kept as a value
///   type so <see cref="WeatherService"/> can compare-and-skip writes and
///   tests can pin the table without a scene tree.
/// </summary>
public readonly record struct FogSettings(
  float Density,
  Color LightColor,
  float SunScatter,
  float AerialPerspective,
  bool Enabled
);

/// <summary>
///   Pure fog table (plan Decision 5). WeatherService owns every fog field;
///   DayNightService only ever disables fog at night, so the night rule lives
///   here as <c>Enabled = !night</c>.
///
///   Values are tuned for the enlarged archipelago (WorldLayout rings out to
///   EasterMaxDistance): daytime keeps a thin Clear haze so far islands read
///   as distance instead of hard cutouts against the sky, and Storm haze
///   reaches the story ring without blacking out the Main beach.
/// </summary>
public static class WeatherFog
{
  public const float ClearDensity = 0.00022f;
  public const float CloudyDensity = 0.0009f;
  public const float RainDensity = 0.0024f;
  public const float StormDensity = 0.004f;

  public static FogSettings Resolve(WeatherType weather, bool night) =>
    new(
      Density: weather switch
      {
        WeatherType.Cloudy => CloudyDensity,
        WeatherType.Rain => RainDensity,
        WeatherType.Storm => StormDensity,
        _ => ClearDensity
      },
      LightColor: weather switch
      {
        WeatherType.Cloudy => new Color(0.7f, 0.7f, 0.75f),
        WeatherType.Rain => new Color(0.5f, 0.5f, 0.55f),
        WeatherType.Storm => new Color(0.3f, 0.3f, 0.35f),
        _ => new Color(0.72f, 0.78f, 0.85f)
      },
      SunScatter: weather switch
      {
        WeatherType.Clear => 0.12f,
        WeatherType.Cloudy => 0.08f,
        WeatherType.Rain => 0.04f,
        WeatherType.Storm => 0.02f,
        _ => 0.08f
      },
      AerialPerspective: weather switch
      {
        WeatherType.Clear => 0.55f,
        WeatherType.Cloudy => 0.45f,
        WeatherType.Rain => 0.3f,
        WeatherType.Storm => 0.15f,
        _ => 0.4f
      },
      Enabled: !night
    );
}
