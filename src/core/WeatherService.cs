// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Weather service, ported from upstream SurvivalIsland's WeatherSystem
///   with these adaptations:
///   - transitions computed via the pure <see cref="WeatherLogic"/> tables;
///   - signals published through the static <see cref="GameEvents"/> bus;
///   - fog fully owned by this service (plan Decision 5): FogDensity,
///     FogLightColor, FogSunScatter, FogAerialPerspective and FogEnabled
///     are applied on weather/night changes. Daytime keeps a thin Clear
///     haze so far islands read as distance; FogEnabled is forced off at
///     night (DayNightService only ever disables fog);
///   - no rain particles or audio (explicitly out of scope);
///   - the current hour is cached from GameEvents.TimeChanged, so
///     EffectiveTemperature needs no scene wiring to DayNightService.
///   This node SUBSCRIBES GameEvents.TimeChanged and unsubscribes in
///   _ExitTree (plan Decision 13).
/// </summary>
public partial class WeatherService : Node
{
  [Export] public float MinWeatherDuration = 120f; // Seconds
  [Export] public float MaxWeatherDuration = 600f;

  /// <summary>Path to the WorldEnvironment node (e.g. "../WorldEnvironment").</summary>
  [Export] public NodePath? Environment { get; set; }

  private WorldEnvironment? _environment;
  private WeatherType _currentWeather = WeatherType.Clear;
  private float _weatherTimer;
  private float _nextWeatherChange;
  private WeatherType _appliedFogWeather;
  private bool _appliedFogNight;
  private bool _fogApplied;

  /// <summary>Latest hour published by GameEvents.TimeChanged.</summary>
  private float _currentHour;

  public WeatherType CurrentWeather => _currentWeather;
  public bool IsRaining =>
    _currentWeather == WeatherType.Rain || _currentWeather == WeatherType.Storm;

  public float TemperatureModifier => WeatherLogic.TemperatureModifier(_currentWeather);

  /// <summary>
  ///   Ambient temperature (from the day/night curve at the cached hour) plus
  ///   the weather modifier.
  /// </summary>
  public float EffectiveTemperature =>
    DayNightMath.AmbientTemperature(_currentHour) + TemperatureModifier;

  public override void _Ready()
  {
    _environment = Environment is null ? null : GetNodeOrNull<WorldEnvironment>(Environment);
    _nextWeatherChange = GD.Randf() * (MaxWeatherDuration - MinWeatherDuration)
      + MinWeatherDuration;
    SetWeather(WeatherType.Clear);
    GameEvents.TimeChanged += OnTimeChanged;
  }

  public override void _ExitTree() => GameEvents.TimeChanged -= OnTimeChanged;

  private void OnTimeChanged(float hour) => _currentHour = hour;

  public override void _Process(double delta)
  {
    _weatherTimer += (float)delta;

    if (_weatherTimer >= _nextWeatherChange)
    {
      ChangeWeatherRandomly();
      _weatherTimer = 0;
      _nextWeatherChange = GD.Randf() * (MaxWeatherDuration - MinWeatherDuration)
        + MinWeatherDuration;
    }

    // Fog is owned by WeatherService. Re-apply only when weather or night
    // crossing changes — writing Environment every frame was a P1 setter tax.
    ApplyFog();
  }

  private void ChangeWeatherRandomly() =>
    SetWeather(WeatherLogic.NextWeather(_currentWeather, GD.Randf()));

  public void SetWeather(WeatherType weather)
  {
    if (_currentWeather == weather)
      return;

    _currentWeather = weather;
    GameEvents.RaiseWeatherChanged(weather);

    GD.Print($"Weather changed to: {weather}");
  }

  /// <summary>
  ///   FIX(code-review P2-17): save-load restore entry point. Unlike
  ///   <see cref="SetWeather"/> (which intentionally early-outs on the same
  ///   value — random weather changes must not re-fire events), a RESTORE must
  ///   always publish so consumers (e.g. the SfxHook storm audio) re-sync even
  ///   when the saved weather equals the current one. Mirrors
  ///   <see cref="DayNightService.RestoreTime"/>'s separate load path.
  /// </summary>
  public void RestoreWeather(WeatherType weather)
  {
    _currentWeather = weather;
    GameEvents.RaiseWeatherChanged(weather);
    _fogApplied = false;
  }

  private void ApplyFog()
  {
    if (_environment?.Environment is not { } env)
      return;

    var night = DayNightMath.IsNight(_currentHour);
    if (_fogApplied && _appliedFogWeather == _currentWeather && _appliedFogNight == night)
      return;

    // Exponential fog: table lives in the pure WeatherFog resolver (tested
    // against the 4 weather x day/night combinations).
    var fog = WeatherFog.Resolve(_currentWeather, night);
    env.FogMode = Godot.Environment.FogModeEnum.Exponential;
    env.FogDensity = fog.Density;
    env.FogLightColor = fog.LightColor;
    env.FogSunScatter = fog.SunScatter;
    env.FogAerialPerspective = fog.AerialPerspective;
    // Daytime always keeps a thin haze so far islands (Explore / Easter)
    // read as distance rather than hard cutouts against the sky.
    env.FogEnabled = fog.Enabled;
    _appliedFogWeather = _currentWeather;
    _appliedFogNight = night;
    _fogApplied = true;
  }
}
