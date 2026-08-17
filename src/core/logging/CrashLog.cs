namespace SeaAnomaly;

using System;

/// <summary>
///   Static facade for <see cref="CrashLogService"/>. Safe when the autoload
///   is missing — every method no-ops. Does not print to the Godot console
///   (Logger callbacks must not re-enter <c>GD.Print*</c>).
/// </summary>
public static class CrashLog
{
  public static CrashLogWriter? Writer { get; private set; }
  public static CrashLogService? Service { get; private set; }

  internal static void Attach(CrashLogService service, CrashLogWriter writer)
  {
    Service = service;
    Writer = writer;
  }

  internal static void Detach(CrashLogService service)
  {
    if (Service != service)
      return;

    Service = null;
    Writer = null;
  }

  public static void Info(string message) => Writer?.WriteLine(CrashLogLevel.Info, message);

  public static void Warn(string message) => Writer?.WriteLine(CrashLogLevel.Warn, message);

  public static void Error(string message) => Writer?.WriteLine(CrashLogLevel.Error, message);

  public static void ReportException(Exception exception, string? context = null) =>
    Service?.ReportException(exception, context);
}
