// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Day/night cycle service, ported from upstream SurvivalIsland's
///   DayNightCycle with these adaptations:
///   - all signals published through the static <see cref="GameEvents"/> bus;
///   - fog: this service ONLY disables fog during true night
///     (<see cref="DayNightMath.IsNight"/>); it never touches
///     <see cref="Environment.FogDensity"/> — fog density/enablement belong to
///     <see cref="WeatherService"/> (plan Decision 5);
///   - a single DirectionalLight3D doubles as the moon at night (upstream
///     behavior, no extra lights);
///   - AmbientLightColor/AmbientLightEnergy writes are kept for upstream
///     parity, but with ambient_light_source=Sky in Game.tscn they have no
///     visual effect — sky color interpolation still applies.
///   This node only PUBLISHES events; it subscribes to none, so no
///   _ExitTree unsubscribe is needed.
/// </summary>
public partial class DayNightService : Node
{
  [Export(PropertyHint.Range, "0.01, 60, 0.01")]
  public float DayDurationMinutes = 0.5f; // Real-time minutes per game day (30 sec)

  [Export(PropertyHint.Range, "0, 24, 0.5")]
  public float StartHour = 8f; // Start at 8 AM

  [Export] public NodePath? SunPath { get; set; }
  [Export] public NodePath? EnvironmentPath { get; set; }

  [Export] public Color DawnColor = new(1.0f, 0.4f, 0.2f); // Deep orange
  [Export] public Color DayColor = new(1.0f, 0.98f, 0.95f); // Warm white
  [Export] public Color DuskColor = new(1.0f, 0.3f, 0.1f); // Red-orange
  [Export] public Color NightColor = new(0.1f, 0.1f, 0.2f); // Very dark blue
  [Export] public Color MoonColor = new(0.6f, 0.7f, 0.9f); // Pale blue moonlight

  [Export] public float DawnIntensity = 0.5f;
  [Export] public float DayIntensity = 1.0f;
  [Export] public float DuskIntensity = 0.4f;
  [Export] public float NightIntensity = 0.05f;
  [Export] public float MoonIntensity = 0.15f; // Moonlight intensity

  private DirectionalLight3D? _sun;
  private WorldEnvironment? _environment;

  private float _currentTime; // 0-24 hours
  private int _currentDay = 1;
  private DayPeriod _currentPeriod;
  private float _secondsPerHour;

  public float CurrentHour => _currentTime;
  public int CurrentDay => _currentDay;
  public DayPeriod CurrentPeriod => _currentPeriod;
  public bool IsNight => DayNightMath.IsNight(_currentTime);

  /// <summary>Ambient temperature based on time (kept as environment data).</summary>
  public float AmbientTemperature => DayNightMath.AmbientTemperature(_currentTime);

  public override void _Ready()
  {
    _currentTime = StartHour;
    _secondsPerHour = DayNightMath.SecondsPerHour(DayDurationMinutes);

    if (SunPath != null)
      _sun = GetNodeOrNull<DirectionalLight3D>(SunPath);
    if (EnvironmentPath != null)
      _environment = GetNodeOrNull<WorldEnvironment>(EnvironmentPath);

    GD.Print($"DayNightService: Started at {StartHour}:00, {DayDurationMinutes} min/day");
    GD.Print($"DayNightService: Sun={_sun != null}, Environment={_environment != null}");

    UpdatePeriod();
    UpdateLighting();

    // Publish the initial hour so late subscribers (WeatherService, HUD) can
    // pull a consistent starting value.
    GameEvents.RaiseTimeChanged(_currentTime);
  }

  public override void _Process(double delta)
  {
    // Advance time.
    var hoursToAdd = (float)delta / _secondsPerHour;
    _currentTime += hoursToAdd;

    // Handle day rollover.
    if (_currentTime >= 24f)
    {
      _currentTime -= 24f;
      _currentDay++;
      GameEvents.RaiseDayChanged(_currentDay);
    }

    UpdatePeriod();
    UpdateLighting();

    GameEvents.RaiseTimeChanged(_currentTime);
  }

  private void UpdatePeriod()
  {
    var newPeriod = DayNightMath.FromHour(_currentTime);

    if (newPeriod == _currentPeriod)
      return;

    _currentPeriod = newPeriod;
    GameEvents.RaisePeriodChanged(newPeriod);
  }

  private void UpdateLighting()
  {
    if (_sun == null)
      return;

    // Sun is visible from 6 AM to 18 PM.
    var isSunUp = _currentTime >= 6 && _currentTime <= 18;
    var isNightTime = _currentTime < 6 || _currentTime > 18;

    if (isSunUp)
    {
      // Sun arc: rises at 6, peaks at 12, sets at 18.
      var dayProgress = Mathf.Clamp((_currentTime - 6) / 12f, 0f, 1f);
      var sunAltitude = Mathf.Sin(dayProgress * Mathf.Pi) * 80f; // Max 80 degrees at noon

      var (color, intensity) = GetLightingForTime();

      _sun.Visible = sunAltitude > 0;
      var sunAzimuth = -90 + dayProgress * 180; // East to West
      _sun.RotationDegrees = new Vector3(-sunAltitude, sunAzimuth, 0);
      _sun.LightColor = color;
      _sun.LightEnergy = intensity;

      // Soft sun shadows - softer at dawn/dusk.
      var shadowSoftness = Mathf.Lerp(4.0f, 2.0f, Mathf.Sin(dayProgress * Mathf.Pi));
      _sun.ShadowBlur = shadowSoftness;
      _sun.LightAngularDistance = 1.5f; // Sun angular size
    }
    else
    {
      // The same light doubles as the moon: rises at 20, peaks at midnight,
      // sets at 6 (upstream behavior — no extra light nodes).
      float nightProgress;
      if (_currentTime >= 20)
        nightProgress = (_currentTime - 20) / 10f; // 20:00 to 06:00 = 10 hours
      else
        nightProgress = (_currentTime + 4) / 10f; // 00:00 to 06:00

      var moonAltitude = Mathf.Sin(nightProgress * Mathf.Pi) * 60f; // Max 60 degrees at midnight

      _sun.Visible = true; // Use sun as moon.
      var moonAzimuth = 90 - nightProgress * 180; // West to East (opposite of sun)
      _sun.RotationDegrees = new Vector3(-Mathf.Max(moonAltitude, 15f), moonAzimuth, 0);
      _sun.LightColor = MoonColor;
      _sun.LightEnergy = MoonIntensity;

      // Very soft moon shadows.
      _sun.ShadowBlur = 6.0f;
      _sun.LightAngularDistance = 1.0f; // Diffuse moonlight
    }

    // Update environment.
    if (_environment?.Environment != null)
    {
      var env = _environment.Environment;

      if (isNightTime)
      {
        // Night: dim blue moonlight ambient. NOTE: with
        // ambient_light_source=Sky (Game.tscn) these ambient writes are
        // no-ops visually; kept for upstream parity in case the ambient
        // source changes later.
        env.AmbientLightColor = new Color(0.05f, 0.05f, 0.1f);
        env.AmbientLightEnergy = 0.1f;
        env.BackgroundEnergyMultiplier = 0.02f;
      }
      else
      {
        var (color, intensity) = GetLightingForTime();
        env.AmbientLightColor = color * 0.3f;
        env.AmbientLightEnergy = intensity * 0.5f;
        env.BackgroundEnergyMultiplier = 1.0f;
      }

      // Fog: WeatherService owns FogDensity and daytime FogEnabled (plan
      // Decision 5). This service only disables fog during true night
      // (<6 or >=20); it never writes FogDensity and never re-enables fog.
      if (DayNightMath.IsNight(_currentTime))
        env.FogEnabled = false;
    }

    // Update sky colors.
    UpdateSkyColors(isNightTime);
  }

  private void UpdateSkyColors(bool isNight)
  {
    if (_environment?.Environment?.Sky?.SkyMaterial is not ProceduralSkyMaterial sky)
      return;

    // Define color palettes.
    var nightTop = new Color(0.0f, 0.0f, 0.01f); // Almost black
    var nightHorizon = new Color(0.0f, 0.0f, 0.02f); // Very dark

    var dawnTop = new Color(0.2f, 0.1f, 0.3f); // Deep purple
    var dawnHorizon = new Color(1.0f, 0.35f, 0.1f); // Vivid orange-red

    var dayTop = new Color(0.3f, 0.5f, 0.9f); // Blue
    var dayHorizon = new Color(0.6f, 0.75f, 0.95f); // Light blue

    var duskTop = new Color(0.3f, 0.1f, 0.2f); // Deep purple-red
    var duskHorizon = new Color(1.0f, 0.25f, 0.05f); // Intense red-orange

    if (isNight)
    {
      sky.SkyTopColor = nightTop;
      sky.SkyHorizonColor = nightHorizon;
      sky.GroundHorizonColor = nightHorizon;
    }
    else if (_currentTime >= 5 && _currentTime < 7) // Dawn
    {
      var t = (_currentTime - 5) / 2f;
      sky.SkyTopColor = nightTop.Lerp(dawnTop, t).Lerp(dayTop, t);
      sky.SkyHorizonColor = nightHorizon.Lerp(dawnHorizon, Mathf.Sin(t * Mathf.Pi));
      sky.GroundHorizonColor = sky.SkyHorizonColor * 0.8f;
    }
    else if (_currentTime >= 17 && _currentTime < 19) // Dusk
    {
      var t = (_currentTime - 17) / 2f;
      sky.SkyTopColor = dayTop.Lerp(duskTop, t);
      sky.SkyHorizonColor = dayHorizon.Lerp(duskHorizon, Mathf.Sin(t * Mathf.Pi));
      sky.GroundHorizonColor = sky.SkyHorizonColor * 0.8f;
    }
    else // Day
    {
      sky.SkyTopColor = dayTop;
      sky.SkyHorizonColor = dayHorizon;
      sky.GroundHorizonColor = dayHorizon * 0.9f;
    }
  }

  private (Color color, float intensity) GetLightingForTime()
  {
    // Dawn: 5-7 / Day: 7-18 / Dusk: 18-20 / Night: 20-5.
    if (_currentTime >= 5 && _currentTime < 7)
    {
      var t = (_currentTime - 5) / 2;
      return (NightColor.Lerp(DawnColor, t).Lerp(DayColor, t),
              Mathf.Lerp(NightIntensity, DawnIntensity, t));
    }
    else if (_currentTime >= 7 && _currentTime < 18)
    {
      var t = (_currentTime - 7) / 11;
      var midT = 1 - Mathf.Abs(t - 0.5f) * 2; // Peak at noon.
      return (DayColor, Mathf.Lerp(DawnIntensity, DayIntensity, midT));
    }
    else if (_currentTime >= 18 && _currentTime < 20)
    {
      var t = (_currentTime - 18) / 2;
      return (DayColor.Lerp(DuskColor, t).Lerp(NightColor, t),
              Mathf.Lerp(DayIntensity, DuskIntensity, t));
    }
    else
    {
      return (NightColor, NightIntensity);
    }
  }

  public string GetTimeString()
  {
    var hours = (int)_currentTime;
    var minutes = (int)((_currentTime - hours) * 60);
    return $"{hours:D2}:{minutes:D2}";
  }

  public void SetTime(float hour)
  {
    _currentTime = Mathf.Clamp(hour, 0, 24);
    UpdatePeriod();
    UpdateLighting();
    // Upstream SetTime does not emit TimeChanged; we publish it anyway so
    // WeatherService's cached hour stays consistent with the new time.
    GameEvents.RaiseTimeChanged(_currentTime);
  }

  /// <summary>
  ///   FIX(iter8-plan): T8.1 — restores BOTH the hour and the day counter from
  ///   a save. <see cref="SetTime"/> intentionally never touches
  ///   <see cref="_currentDay"/> (the bed's skip-night depends on that), so
  ///   loading a save needs this separate entry point.
  /// </summary>
  public void RestoreTime(float hour, int day)
  {
    _currentTime = Mathf.Clamp(hour, 0, 24);
    _currentDay = Mathf.Max(1, day);
    UpdatePeriod();
    UpdateLighting();
    GameEvents.RaiseTimeChanged(_currentTime);
    GameEvents.RaiseDayChanged(_currentDay);
  }
}
