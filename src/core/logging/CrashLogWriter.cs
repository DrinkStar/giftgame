namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

/// <summary>Severity of a session-log line. Crash dumps are a separate file.</summary>
public enum CrashLogLevel
{
  Info,
  Warn,
  Error
}

/// <summary>
///   File-backed session log + crash dumps under a <c>user://</c> (or absolute)
///   folder. Thread-safe: Godot <see cref="Logger"/> callbacks may arrive off
///   the main thread. Directory is injectable so tests never touch the real
///   player log folder.
/// </summary>
public sealed class CrashLogWriter : IDisposable
{
  public const string DefaultFolder = "user://logs";
  public const string SessionFileName = "seaanomaly.log";
  public const int RingCapacity = 200;
  public const int MaxCrashFiles = 10;
  public const int MaxSessionArchives = 5;
  public const long MaxSessionBytes = 2L * 1024 * 1024;

  private readonly object _gate = new();
  private readonly Queue<string> _ring = new();
  private readonly string _folder;
  private readonly string _absoluteFolder;
  private readonly bool _writeSessionFile;
  private readonly int _ringCapacity;
  private readonly int _maxCrashFiles;
  private readonly long _maxSessionBytes;
  private StreamWriter? _session;
  private bool _disposed;

  public string SessionId { get; }
  public string Folder => _folder;
  public string AbsoluteFolder => _absoluteFolder;
  public bool WritesSessionFile => _writeSessionFile;
  public int CrashDumpCount { get; private set; }

  public CrashLogWriter(
    string folder,
    bool writeSessionFile,
    int ringCapacity = RingCapacity,
    int maxCrashFiles = MaxCrashFiles,
    long maxSessionBytes = MaxSessionBytes
  )
  {
    _folder = folder;
    _writeSessionFile = writeSessionFile;
    _ringCapacity = Math.Max(1, ringCapacity);
    _maxCrashFiles = Math.Max(1, maxCrashFiles);
    _maxSessionBytes = Math.Max(1024, maxSessionBytes);
    _absoluteFolder = ToAbsolutePath(folder);
    SessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    Directory.CreateDirectory(_absoluteFolder);

    if (_writeSessionFile)
    {
      RotateSessionFileOnStart();
      OpenSessionWriter();
    }
  }

  public void WriteLine(CrashLogLevel level, string message)
  {
    var line = FormatLine(level, message);
    lock (_gate)
    {
      if (_disposed)
        return;

      EnqueueRing(line);
      _session?.WriteLine(line);
      RotateSessionFileIfOversized();
    }
  }

  /// <summary>
  ///   Writes a crash dump immediately. Snapshot keys matching inventory/save
  ///   payloads are dropped (contract 11). Returns the absolute path written.
  /// </summary>
  public string WriteCrashDump(
    string reason,
    string body,
    IReadOnlyDictionary<string, string>? snapshot = null
  )
  {
    lock (_gate)
    {
      CrashDumpCount++;
      Directory.CreateDirectory(_absoluteFolder);

      var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
      var path = Path.Combine(_absoluteFolder, $"crash-{stamp}.log");
      if (File.Exists(path))
        path = Path.Combine(_absoluteFolder, $"crash-{stamp}-{CrashDumpCount}.log");

      var text = BuildDumpText(reason, body, snapshot);
      File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      PruneCrashFiles();
      return path;
    }
  }

  public IReadOnlyList<string> CopyRing()
  {
    lock (_gate)
      return [.. _ring];
  }

  public int CountCrashFiles()
  {
    if (!Directory.Exists(_absoluteFolder))
      return 0;

    return Directory.GetFiles(_absoluteFolder, "crash-*.log").Length;
  }

  /// <summary>
  ///   Reads the live session file with <see cref="FileShare.ReadWrite"/> so
  ///   tests (and a player copying logs mid-session) are not locked out.
  /// </summary>
  public string ReadSessionText()
  {
    lock (_gate)
    {
      _session?.Flush();
      var path = Path.Combine(_absoluteFolder, SessionFileName);
      if (!File.Exists(path))
        return "";

      using var stream = new FileStream(
        path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite
      );
      using var reader = new StreamReader(stream, Encoding.UTF8);
      return reader.ReadToEnd();
    }
  }

  public static bool IsForbiddenSnapshotKey(string key)
  {
    return key.Contains("inventory", StringComparison.OrdinalIgnoreCase)
      || key.Contains("backpack", StringComparison.OrdinalIgnoreCase)
      || key.Contains("savejson", StringComparison.OrdinalIgnoreCase)
      || key.Contains("save_json", StringComparison.OrdinalIgnoreCase)
      || key.Contains("save_body", StringComparison.OrdinalIgnoreCase)
      || key.Contains("item_ids", StringComparison.OrdinalIgnoreCase)
      || key.Contains("item_count", StringComparison.OrdinalIgnoreCase)
      || key.Contains("grid_items", StringComparison.OrdinalIgnoreCase);
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _session?.Flush();
      _session?.Dispose();
      _session = null;
      _disposed = true;
    }
  }

  internal static string ToAbsolutePath(string path) =>
    path.StartsWith("user://", StringComparison.Ordinal)
      || path.StartsWith("res://", StringComparison.Ordinal)
      ? ProjectSettings.GlobalizePath(path)
      : path;

  private string BuildDumpText(
    string reason,
    string body,
    IReadOnlyDictionary<string, string>? snapshot
  )
  {
    var sb = new StringBuilder();
    sb.AppendLine("=== SeaAnomaly crash dump ===");
    sb.Append("utc=").AppendLine(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    sb.Append("session=").AppendLine(SessionId);
    sb.Append("reason=").AppendLine(reason);
    if (snapshot != null)
    {
      sb.AppendLine("--- snapshot ---");
      foreach (var pair in snapshot)
      {
        if (IsForbiddenSnapshotKey(pair.Key))
          continue;
        sb.Append(pair.Key).Append('=').AppendLine(pair.Value);
      }
    }

    sb.AppendLine("--- event ---");
    sb.AppendLine(body);
    sb.AppendLine("--- recent log ---");
    foreach (var line in _ring)
      sb.AppendLine(line);
    return sb.ToString();
  }

  private static string FormatLine(CrashLogLevel level, string message)
  {
    var stamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    var tag = level switch
    {
      CrashLogLevel.Warn => "WARN",
      CrashLogLevel.Error => "ERROR",
      _ => "INFO"
    };
    return $"{stamp} [{tag}] {message}";
  }

  private void EnqueueRing(string line)
  {
    _ring.Enqueue(line);
    while (_ring.Count > _ringCapacity)
      _ring.Dequeue();
  }

  private void OpenSessionWriter()
  {
    var path = Path.Combine(_absoluteFolder, SessionFileName);
    _session = new StreamWriter(
      new FileStream(path, FileMode.Append, System.IO.FileAccess.Write, FileShare.ReadWrite),
      new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    )
    {
      AutoFlush = true
    };
  }

  private void RotateSessionFileOnStart()
  {
    var current = Path.Combine(_absoluteFolder, SessionFileName);
    if (!File.Exists(current) || new FileInfo(current).Length == 0)
      return;

    var stamp = File.GetLastWriteTimeUtc(current)
      .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    var archive = Path.Combine(_absoluteFolder, $"seaanomaly-{stamp}.log");
    if (File.Exists(archive))
      archive = Path.Combine(_absoluteFolder, $"seaanomaly-{stamp}-{SessionId}.log");
    File.Move(current, archive);
    PruneSessionArchives();
  }

  private void RotateSessionFileIfOversized()
  {
    if (_session == null)
      return;

    var current = Path.Combine(_absoluteFolder, SessionFileName);
    if (!File.Exists(current) || new FileInfo(current).Length < _maxSessionBytes)
      return;

    _session.Flush();
    _session.Dispose();
    _session = null;
    RotateSessionFileOnStart();
    OpenSessionWriter();
  }

  private void PruneCrashFiles()
  {
    PruneByPattern("crash-*.log", _maxCrashFiles);
  }

  private void PruneSessionArchives()
  {
    PruneByPattern("seaanomaly-*.log", MaxSessionArchives);
  }

  private void PruneByPattern(string pattern, int keep)
  {
    if (!Directory.Exists(_absoluteFolder))
      return;

    var files = new DirectoryInfo(_absoluteFolder).GetFiles(pattern);
    Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
    for (var i = keep; i < files.Length; i++)
    {
      try
      {
        files[i].Delete();
      }
      catch (IOException)
      {
        // File may still be open in an editor; skip.
      }
    }
  }
}
