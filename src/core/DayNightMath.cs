// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure static day/night math extracted from the upstream DayNightCycle so
///   the period/temperature rules can be unit-tested without a scene tree.
///   All hours are game-world hours in [0, 24).
/// </summary>
public static class DayNightMath
{
  /// <summary>
  ///   Maps an hour to a day period: Dawn [5,7), Day [7,18), Dusk [18,20),
  ///   Night everything else.
  /// </summary>
  public static DayPeriod FromHour(float hour) =>
    hour >= 5 && hour < 7 ? DayPeriod.Dawn
    : hour >= 7 && hour < 18 ? DayPeriod.Day
    : hour >= 18 && hour < 20 ? DayPeriod.Dusk
    : DayPeriod.Night;

  /// <summary>True between midnight and 6 AM, or from 8 PM to midnight.</summary>
  public static bool IsNight(float hour) => hour < 6 || hour >= 20;

  /// <summary>
  ///   Ambient temperature curve, ported from upstream. Upstream's comment
  ///   claims 10 °C at 4 AM and 25 °C at 2 PM, but the formula disagrees with
  ///   the latter: the actual maximum is 25 °C at 10 AM. The formula is kept
  ///   as-is (plan Decision 16).
  /// </summary>
  public static float AmbientTemperature(float hour) =>
    17.5f + Mathf.Sin((hour - 4f) * Mathf.Pi / 12f) * 7.5f;

  /// <summary>Real-time seconds that one game hour takes.</summary>
  public static float SecondsPerHour(float dayDurationMinutes) =>
    dayDurationMinutes * 60f / 24f;
}
