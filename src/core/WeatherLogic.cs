// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

/// <summary>
///   Pure static weather state machine ported from upstream WeatherSystem's
///   ChangeWeatherRandomly switch, extracted so the Markov tables can be
///   unit-tested deterministically. <paramref name="roll"/> is expected in
///   [0, 1) (e.g. GD.Randf()).
/// </summary>
public static class WeatherLogic
{
  /// <summary>
  ///   Markov transition tables (plan Decision 6):
  ///   Clear  → roll &lt; 0.7 Clear, else Cloudy;
  ///   Cloudy → &lt; 0.3 Clear, &lt; 0.7 Cloudy, else Rain;
  ///   Rain   → &lt; 0.2 Cloudy, &lt; 0.7 Rain, else Storm;
  ///   Storm  → &lt; 0.6 Rain, else Cloudy.
  /// </summary>
  public static WeatherType NextWeather(WeatherType current, float roll) =>
    current switch
    {
      WeatherType.Clear => roll < 0.7f ? WeatherType.Clear : WeatherType.Cloudy,
      WeatherType.Cloudy => roll < 0.3f ? WeatherType.Clear
        : roll < 0.7f ? WeatherType.Cloudy
        : WeatherType.Rain,
      WeatherType.Rain => roll < 0.2f ? WeatherType.Cloudy
        : roll < 0.7f ? WeatherType.Rain
        : WeatherType.Storm,
      WeatherType.Storm => roll < 0.6f ? WeatherType.Rain : WeatherType.Cloudy,
      _ => WeatherType.Clear
    };

  /// <summary>
  ///   Temperature delta applied on top of the ambient day/night curve:
  ///   Clear 0, Cloudy -2, Rain -5, Storm -8.
  /// </summary>
  public static float TemperatureModifier(WeatherType weather) =>
    weather switch
    {
      WeatherType.Clear => 0f,
      WeatherType.Cloudy => -2f,
      WeatherType.Rain => -5f,
      WeatherType.Storm => -8f,
      _ => 0f
    };
}
