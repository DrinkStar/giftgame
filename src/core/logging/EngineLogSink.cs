namespace SeaAnomaly;

using Godot;
using Godot.Collections;

/// <summary>
///   Receives the engine error/warning stream via <see cref="OS.AddLogger"/>.
///   Engine errors are session-logged; they do not themselves write a crash
///   dump (dumps are reserved for unhandled exceptions and
///   <see cref="Node.NotificationCrash"/>).
/// </summary>
public partial class EngineLogSink : Logger
{
  private readonly CrashLogWriter _writer;

  public EngineLogSink(CrashLogWriter writer) => _writer = writer;

  public override void _LogMessage(string message, bool error)
  {
    // Skip ordinary prints — DayNight/Weather/building spam would drown the
    // session file. stderr-bound messages still land as ERROR lines.
    if (!error)
      return;

    _writer.WriteLine(CrashLogLevel.Error, TrimMessage(message));
  }

  public override void _LogError(
    string function,
    string file,
    int line,
    string code,
    string rationale,
    bool editorNotify,
    int errorType,
    Array<ScriptBacktrace> scriptBacktraces
  )
  {
    var type = (ErrorType)errorType;
    var level = type == ErrorType.Warning ? CrashLogLevel.Warn : CrashLogLevel.Error;
    var message = $"{type} {file}:{line} {function} | {code} {rationale}".Trim();
    _writer.WriteLine(level, message);
  }

  private static string TrimMessage(string message) =>
    message.TrimEnd('\r', '\n');
}
