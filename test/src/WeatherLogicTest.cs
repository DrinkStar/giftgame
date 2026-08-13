// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure-C# <see cref="WeatherLogic"/> Markov tables and
///   temperature modifiers.
/// </summary>
public class WeatherLogicTest : TestClass
{
  public WeatherLogicTest(Node testScene) : base(testScene) { }

  [Test]
  public void ClearRollBoundarySplitsAtPointSeven()
  {
    WeatherLogic.NextWeather(WeatherType.Clear, 0.6999f).ShouldBe(WeatherType.Clear);
    WeatherLogic.NextWeather(WeatherType.Clear, 0.7f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Clear, 0.999f).ShouldBe(WeatherType.Cloudy);
  }

  [Test]
  public void CloudyTableTransitionsAreValid()
  {
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.1f).ShouldBe(WeatherType.Clear);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.2999f).ShouldBe(WeatherType.Clear);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.3f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.5f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.6999f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.7f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Cloudy, 0.999f).ShouldBe(WeatherType.Rain);
  }

  [Test]
  public void RainTableTransitionsAreValid()
  {
    WeatherLogic.NextWeather(WeatherType.Rain, 0.1f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.1999f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.2f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.5f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.6999f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.7f).ShouldBe(WeatherType.Storm);
    WeatherLogic.NextWeather(WeatherType.Rain, 0.999f).ShouldBe(WeatherType.Storm);
  }

  [Test]
  public void StormTableTransitionsAreValid()
  {
    WeatherLogic.NextWeather(WeatherType.Storm, 0.1f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Storm, 0.5999f).ShouldBe(WeatherType.Rain);
    WeatherLogic.NextWeather(WeatherType.Storm, 0.6f).ShouldBe(WeatherType.Cloudy);
    WeatherLogic.NextWeather(WeatherType.Storm, 0.999f).ShouldBe(WeatherType.Cloudy);
  }

  [Test]
  public void TemperatureModifierMatchesDesignTable()
  {
    WeatherLogic.TemperatureModifier(WeatherType.Clear).ShouldBe(0f);
    WeatherLogic.TemperatureModifier(WeatherType.Cloudy).ShouldBe(-2f);
    WeatherLogic.TemperatureModifier(WeatherType.Rain).ShouldBe(-5f);
    WeatherLogic.TemperatureModifier(WeatherType.Storm).ShouldBe(-8f);
  }
}
