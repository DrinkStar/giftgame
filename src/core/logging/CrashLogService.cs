namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
///   Autoload that owns the session log, engine <see cref="Logger"/> sink, and
///   C# unhandled-exception hooks. Must be the first autoload so startup
///   failures after C# boots still land in <c>user://logs</c>.
///   Init lives in <see cref="_EnterTree"/> — Godot may skip C# constructors
///   when instantiating scripted nodes.
/// </summary>
[GlobalClass]
public partial class CrashLogService : Node
{
  public const string TestFolder = "user://logs_test";

  private EngineLogSink? _sink;
  private CrashLogWriter? _writer;
  private int _dumping;
  private int _lastDay;
  private DayPeriod _lastPeriod;
  private WeatherType _lastWeather;
  private bool _paused;
  private bool _eventsHooked;

  public CrashLogWriter? Writer => _writer;

  public override void _EnterTree()
  {
    ProcessMode = ProcessModeEnum.Always;
    EnsureInitialized();
    if (_eventsHooked)
      return;

    GameEvents.DayChanged += OnDayChanged;
    GameEvents.PeriodChanged += OnPeriodChanged;
    GameEvents.WeatherChanged += OnWeatherChanged;
    GameEvents.GamePaused += OnPaused;
    GameEvents.GameResumed += OnResumed;
    _eventsHooked = true;
  }

  public override void _ExitTree()
  {
    if (_eventsHooked)
    {
      GameEvents.DayChanged -= OnDayChanged;
      GameEvents.PeriodChanged -= OnPeriodChanged;
      GameEvents.WeatherChanged -= OnWeatherChanged;
      GameEvents.GamePaused -= OnPaused;
      GameEvents.GameResumed -= OnResumed;
      _eventsHooked = false;
    }

    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

    if (_sink != null)
    {
      try
      {
        OS.RemoveLogger(_sink);
      }
      catch
      {
        // Engine may already be tearing down.
      }

      _sink = null;
    }

    _writer?.Dispose();
    CrashLog.Detach(this);
    _writer = null;
  }

  public override void _Notification(int what)
  {
    if (what != NotificationCrash)
      return;

    ReportCrash("NOTIFICATION_CRASH", "Engine crash handler invoked.");
  }

  public void ReportException(Exception exception, string? context = null)
  {
    var reason = context ?? exception.GetType().FullName ?? "Exception";
    ReportCrash(reason, exception.ToString());
  }

  public static bool IsRunningTests()
  {
    foreach (var arg in OS.GetCmdlineArgs())
    {
      if (string.Equals(arg, "--run-tests", StringComparison.Ordinal))
        return true;
    }

    return false;
  }

  private void EnsureInitialized()
  {
    if (_writer != null)
      return;

    try
    {
      var testing = IsRunningTests();
      _writer = new CrashLogWriter(
        testing ? TestFolder : CrashLogWriter.DefaultFolder,
        writeSessionFile: !testing
      );
      CrashLog.Attach(this, _writer);
      _sink = new EngineLogSink(_writer);
      OS.AddLogger(_sink);
      AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
      TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
      _writer.WriteLine(
        CrashLogLevel.Info,
        testing
          ? "CrashLogService started (test mode — session file disabled)"
          : "CrashLogService started"
      );
    }
    catch
    {
      // Logging must never prevent the game from booting.
    }
  }

  private void ReportCrash(string reason, string body)
  {
    if (Interlocked.Exchange(ref _dumping, 1) == 1)
      return;

    try
    {
      _writer?.WriteLine(CrashLogLevel.Error, $"CRASH {reason}");
      _writer?.WriteCrashDump(reason, body, CaptureSnapshot());
    }
    catch
    {
      // Last-chance path: swallow so we do not crash while crashing.
    }
    finally
    {
      Interlocked.Exchange(ref _dumping, 0);
    }
  }

  private Dictionary<string, string> CaptureSnapshot()
  {
    var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
    try
    {
      snapshot["godot"] = Engine.GetVersionInfo()["string"].AsString();
      snapshot["os"] = OS.GetName();
      snapshot["cmdline"] = string.Join(' ', OS.GetCmdlineArgs());
      snapshot["user_data"] = OS.GetUserDataDir();
      snapshot["editor"] = OS.HasFeature("editor").ToString();
      snapshot["ticks_msec"] = Time.GetTicksMsec().ToString(CultureInfo.InvariantCulture);
      snapshot["day"] = _lastDay.ToString(CultureInfo.InvariantCulture);
      snapshot["period"] = _lastPeriod.ToString();
      snapshot["weather"] = _lastWeather.ToString();
      snapshot["paused"] = _paused.ToString();

      var scene = GetTree()?.CurrentScene;
      snapshot["scene"] = scene?.SceneFilePath ?? "";
      var player = scene?.GetNodeOrNull<Node3D>("Player");
      if (player != null)
      {
        var p = player.GlobalPosition;
        snapshot["player_pos"] = string.Create(
          CultureInfo.InvariantCulture,
          $"{p.X:F1},{p.Y:F1},{p.Z:F1}"
        );
      }
    }
    catch
    {
      // Snapshot is best-effort.
    }

    return snapshot;
  }

  private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
  {
    if (args.ExceptionObject is Exception exception)
      ReportException(exception, "AppDomain.UnhandledException");
    else
      ReportCrash("AppDomain.UnhandledException", args.ExceptionObject?.ToString() ?? "");
  }

  private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
  {
    ReportException(args.Exception, "TaskScheduler.UnobservedTaskException");
    args.SetObserved();
  }

  private void OnDayChanged(int day) => _lastDay = day;

  private void OnPeriodChanged(DayPeriod period) => _lastPeriod = period;

  private void OnWeatherChanged(WeatherType weather) => _lastWeather = weather;

  private void OnPaused() => _paused = true;

  private void OnResumed() => _paused = false;
}
