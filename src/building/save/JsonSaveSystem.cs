// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>Provides generic JSON save/load functionality (upstream SaveSystem).</summary>
/// <remarks>
///   Uses System.Text.Json with the default PascalCase property names so the
///   produced documents match the C# DTO field names. All file probing goes
///   through System.IO over <see cref="ProjectSettings.GlobalizePath"/>,
///   which also makes the save folder injectable for tests (plan Decision 8).
/// </remarks>
public static class JsonSaveSystem
{
  /// <summary>The default folder where building save files are stored.</summary>
  public const string DEFAULT_FOLDER = "user://buildings";

  /// <summary>
  ///   Saves the specified data as JSON into <paramref name="folder"/>
  ///   (plan Decision 8: the directory is guaranteed via
  ///   <see cref="DirAccess.MakeDirRecursiveAbsolute"/> before writing).
  /// </summary>
  /// <typeparam name="T">The type of data to save.</typeparam>
  /// <param name="data">The data to save.</param>
  /// <param name="folder">The target folder (user://-style or absolute).</param>
  /// <param name="fileName">
  ///   The file name to write. When null, a unique name
  ///   <c>savegame_{DateTime.Now.ToFileTime()}.json</c> is generated.
  /// </param>
  /// <returns>The full path (user://-style) of the written file.</returns>
  public static string Save<T>(T data, string folder = DEFAULT_FOLDER, string? fileName = null)
  {
    EnsureFolderExists(folder);
    var name = fileName ?? $"savegame_{DateTime.Now.ToFileTime()}.json";
    var path = $"{folder.TrimEnd('/')}/{name}";

    // Default JsonSerializer options keep property names PascalCase.
    File.WriteAllText(ToAbsolutePath(path), JsonSerializer.Serialize(data));
    return path;
  }

  /// <summary>
  ///   Loads data from the specified save file. Returns <c>default</c> (null
  ///   for reference types) when the file is missing or corrupt — loading
  ///   never throws (plan Decision 5 fix ③ null-safety).
  /// </summary>
  /// <typeparam name="T">The type of data to load.</typeparam>
  /// <param name="path">The path of the save file (user://-style or absolute).</param>
  /// <returns>The loaded data, or <c>default</c> when unavailable.</returns>
  public static T? Load<T>(string path)
  {
    var absolutePath = ToAbsolutePath(path);
    if (!File.Exists(absolutePath))
    {
      return default;
    }

    try
    {
      return JsonSerializer.Deserialize<T>(File.ReadAllText(absolutePath));
    }
    catch (Exception e)
    {
      // A corrupt save file is treated as missing so callers can fall back
      // to a clean state instead of crashing.
      GD.PrintErr($"JsonSaveSystem: failed to load '{path}': {e.Message}");
      return default;
    }
  }

  /// <summary>
  ///   Retrieves information about the save files in the folder, newest
  ///   first. Plan Decision 5 fix ⑥: ONLY <c>savegame_*.json</c> files are
  ///   listed — upstream returned every file in the folder, so unrelated
  ///   files could be picked up as saves.
  /// </summary>
  /// <param name="folder">The folder to scan (user://-style or absolute).</param>
  /// <returns>The matching files ordered by last write time, newest first.</returns>
  public static List<FileInfo> GetSaveFilesInfo(string folder = DEFAULT_FOLDER)
  {
    var directoryPath = ProjectSettings.GlobalizePath(folder);
    if (!Directory.Exists(directoryPath))
    {
      return [];
    }

    var directoryInfo = new DirectoryInfo(directoryPath);
    return directoryInfo
      .EnumerateFiles()
      .Where(
        f =>
          f.Name.StartsWith("savegame_", StringComparison.Ordinal)
          && f.Name.EndsWith(".json", StringComparison.Ordinal)
      )
      .OrderByDescending(f => f.LastWriteTime)
      .ToList();
  }

  /// <summary>Loads the most recent save file from the folder.</summary>
  /// <typeparam name="T">The type of data to load.</typeparam>
  /// <param name="folder">The folder to scan (user://-style or absolute).</param>
  /// <returns>The loaded data of the newest save, or <c>default</c> when none exists.</returns>
  public static T? LoadMostRecent<T>(string folder = DEFAULT_FOLDER)
  {
    var saveFiles = GetSaveFilesInfo(folder);
    return saveFiles.Count > 0 ? Load<T>(saveFiles[0].FullName) : default;
  }

  /// <summary>
  ///   Guarantees the save folder exists (plan Decision 8). Godot rejects
  ///   res://-style pseudo-paths in <see cref="DirAccess.MakeDirRecursiveAbsolute"/>,
  ///   so user:// folders are globalized to a real absolute path first.
  /// </summary>
  private static void EnsureFolderExists(string folder)
  {
    DirAccess.MakeDirRecursiveAbsolute(ToAbsolutePath(folder));
  }

  /// <summary>Translates user://-style paths to absolute OS paths for System.IO.</summary>
  private static string ToAbsolutePath(string path) =>
    path.StartsWith("user://", StringComparison.Ordinal)
      || path.StartsWith("res://", StringComparison.Ordinal)
      ? ProjectSettings.GlobalizePath(path)
      : path;
}
