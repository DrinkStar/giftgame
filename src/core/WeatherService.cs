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
///     FogLightColor and FogEnabled are re-applied EVERY frame, with
///     FogEnabled forced off at night (DayNightService only ever disables
///     fog, so the night-off rule must also hold here);
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

    // Fog is owned by WeatherService and re-applied every frame (plan
    // Decision 5). DayNightService only disables fog at night, so the
    // night-off rule is honored here too.
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

  private void ApplyFog()
  {
    if (_environment?.Environment is not { } env)
      return;

    env.FogDensity = _currentWeather switch
    {
      WeatherType.Clear => 0f,
      WeatherType.Cloudy => 0.001f,
      WeatherType.Rain => 0.003f,
      WeatherType.Storm => 0.005f,
      _ => 0f
    };

    env.FogLightColor = _currentWeather switch
    {
      WeatherType.Cloudy => new Color(0.7f, 0.7f, 0.75f),
      WeatherType.Rain => new Color(0.5f, 0.5f, 0.55f),
      WeatherType.Storm => new Color(0.3f, 0.3f, 0.35f),
      _ => new Color(0.7f, 0.7f, 0.75f)
    };

    env.FogEnabled = _currentWeather != WeatherType.Clear
      && !DayNightMath.IsNight(_currentHour);
  }
}
