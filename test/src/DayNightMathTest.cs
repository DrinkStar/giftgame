// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure-C# <see cref="DayNightMath"/> (period mapping,
///   night predicate, ambient temperature curve, hour scaling).
/// </summary>
public class DayNightMathTest : TestClass
{
  public DayNightMathTest(Node testScene) : base(testScene) { }

  private const double Tolerance = 0.01;

  [Test]
  public void PeriodBoundariesMapToExpectedPeriods()
  {
    DayNightMath.FromHour(5f).ShouldBe(DayPeriod.Dawn);
    DayNightMath.FromHour(7f).ShouldBe(DayPeriod.Day);
    DayNightMath.FromHour(18f).ShouldBe(DayPeriod.Dusk);
    DayNightMath.FromHour(20f).ShouldBe(DayPeriod.Night);
  }

  [Test]
  public void MidnightAndNoonFallIntoNightAndDay()
  {
    DayNightMath.FromHour(0f).ShouldBe(DayPeriod.Night);
    DayNightMath.FromHour(12f).ShouldBe(DayPeriod.Day);
    DayNightMath.FromHour(23.99f).ShouldBe(DayPeriod.Night);
  }

  [Test]
  public void IsNightIsTrueOnlyBelowSixOrFromTwenty()
  {
    DayNightMath.IsNight(0f).ShouldBeTrue();
    DayNightMath.IsNight(5.9f).ShouldBeTrue();
    DayNightMath.IsNight(6f).ShouldBeFalse();
    DayNightMath.IsNight(19.9f).ShouldBeFalse();
    DayNightMath.IsNight(20f).ShouldBeTrue();
    DayNightMath.IsNight(23f).ShouldBeTrue();
  }

  [Test]
  public void AmbientTemperatureMatchesFormulaTruth()
  {
    // Truth values of 17.5 + sin((h-4)*PI/12)*7.5 (plan Decision 16). Note:
    // the upstream comment claims 10 °C at 4 AM / 25 °C at 2 PM, but the
    // formula disagrees with the 2 PM claim (its actual peak is 25 °C at
    // 10 AM) — the formula is kept as-is.
    DayNightMath.AmbientTemperature(4f).ShouldBe(17.5f, Tolerance);
    DayNightMath.AmbientTemperature(10f).ShouldBe(25f, Tolerance);
    DayNightMath.AmbientTemperature(14f).ShouldBe(21.25f, Tolerance);
    DayNightMath.AmbientTemperature(22f).ShouldBe(10f, Tolerance);
  }

  [Test]
  public void SecondsPerHourScalesWithDayLength()
  {
    DayNightMath.SecondsPerHour(0.5f).ShouldBe(1.25f, Tolerance); // 30 s/day
    DayNightMath.SecondsPerHour(24f).ShouldBe(60f, Tolerance); // 24 min/day
  }
}
