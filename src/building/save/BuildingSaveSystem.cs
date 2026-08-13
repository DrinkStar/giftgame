// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   Save system for the grid building system (upstream BSSaveSystem +
///   SaveExtensions, merged into one static class). Snapshot the grids and
///   free objects into <see cref="SaveFile"/> DTOs and restore them.
/// </summary>
/// <remarks>
///   Plan Decision 5 fixes locked here:
///   - fix ③: the ENTIRE load (including the free-object loop) runs inside
///     the null guard — a missing or corrupt file returns false without
///     touching the scene (upstream dereferenced the null save file).
///   - fix ④: Save writes EVERY grid with its index (upstream skipped empty
///     grids); Load uses the stored Index to locate the target grid.
///   - fix ⑤: rotation is stored and applied in DEGREES everywhere; free
///     objects convert to radians exactly once on load.
///   - Decision 4: grid object Y comes from the grid's GlobalPosition.Y,
///     never from the grid index.
/// </remarks>
public static class BuildingSaveSystem
{
  /// <summary>
  ///   Saves the current state of the grid building system as JSON.
  /// </summary>
  /// <param name="overwrite">Whether to overwrite <paramref name="currentFile"/>.</param>
  /// <param name="currentFile">
  ///   The previously used save file path; reused when overwriting.
  /// </param>
  /// <param name="grids">The list of grids to save.</param>
  /// <param name="freeObjects">The list of free objects to save.</param>
  /// <param name="folder">The save folder (user://-style or absolute).</param>
  /// <returns>The full path of the written save file.</returns>
  public static string Save(
    bool overwrite,
    string? currentFile,
    List<BuildingSystemGrid> grids,
    List<BuildableInstance> freeObjects,
    string folder = JsonSaveSystem.DEFAULT_FOLDER
  )
  {
    var saveFile = new SaveFile();

    // Fix ④: write ALL grids with their list index — upstream only wrote
    // non-empty grids, so a level-0-only save silently restored every object
    // onto grid 0 after a reload. Empty grids are cheap and keep the file a
    // faithful snapshot of the grid stack.
    for (var i = 0; i < grids.Count; i++)
    {
      saveFile.Grids.Add(ToSaveGrid(i, grids[i]));
    }

    foreach (var freeObject in freeObjects)
    {
      saveFile.FreeObjects.Add(ToSaveFreeObject(freeObject));
    }

    var fileName = overwrite && currentFile != null ? StripFolder(currentFile) : null;
    return JsonSaveSystem.Save(saveFile, folder, fileName);
  }

  /// <summary>
  ///   Loads a saved state of the grid building system. Returns false without
  ///   touching the scene when the file is missing or corrupt (fix ③).
  /// </summary>
  /// <param name="filename">The path of the save file to load.</param>
  /// <param name="library">The library used to resolve buildables by name.</param>
  /// <param name="grids">The list of grids to restore into (list order matches save order).</param>
  /// <param name="freeObjectContainer">The container node for free objects.</param>
  /// <param name="freeLayerMask">The layer mask for free objects.</param>
  /// <param name="folder">The save folder (unused for probing, kept for API symmetry).</param>
  /// <returns>True when the file was loaded; false when missing/corrupt.</returns>
  public static bool Load(
    string filename,
    BuildableResourceLibrary library,
    IReadOnlyList<BuildingSystemGrid> grids,
    Node freeObjectContainer,
    uint freeLayerMask,
    string folder = JsonSaveSystem.DEFAULT_FOLDER
  )
  {
    _ = folder;

    // Fix ③: the free-object loop used to live OUTSIDE this guard, so a
    // missing save file crashed with a null reference instead of reporting
    // failure. Now the whole load sits inside the guard.
    var saveFile = JsonSaveSystem.Load<SaveFile>(filename);
    if (saveFile == null)
    {
      return false;
    }

    // Fix ④: locate each saved grid by its stored Index. The grid list is
    // index-ordered (Index == i for valid files), but a tampered file must
    // never index out of bounds.
    for (var i = 0; i < saveFile.Grids.Count; i++)
    {
      var targetIndex = saveFile.Grids[i].Index;
      if (targetIndex < 0 || targetIndex >= grids.Count)
      {
        continue;
      }

      var grid = grids[targetIndex];
      foreach (var gridObject in saveFile.Grids[i].Objects)
      {
        var resource = library.GetByName(gridObject.Name);
        if (resource == null)
        {
          // Missing asset (plan integration trap): skip this object instead
          // of crashing, so one broken reference cannot block a whole save.
          GD.PushWarning($"BuildingSaveSystem: buildable '{gridObject.Name}' not in library; skipped.");
          continue;
        }

        // Decision 4: the Y coordinate comes from the grid's global height
        // (0.5 + level * cell height in Game.tscn), never from the grid
        // index as upstream did.
        var position = new Vector3(gridObject.PositionX, grid.GlobalPosition.Y, gridObject.PositionZ);

        // Fix ⑤: rotation is already in degrees; TryToPlaceObject converts
        // internally. Occupancy failures (e.g. duplicate entries) are safe
        // to ignore — the cell was already restored.
        grid.TryToPlaceObject(resource, gridObject.RotationDegreesY, position);
      }
    }

    // Fix ③: free objects restore inside the guard, after the grids.
    foreach (var freeObject in saveFile.FreeObjects)
    {
      var resource = library.GetByName(freeObject.Name);
      if (resource == null)
      {
        GD.PushWarning($"BuildingSaveSystem: buildable '{freeObject.Name}' not in library; skipped.");
        continue;
      }

      var instance = BuildableInstance.Create(resource, freeLayerMask);
      freeObjectContainer.AddChild(instance);

      // Saved position is LOCAL to the free object container; the load
      // contract assigns it as the global position (upstream behavior —
      // the container sits at the world origin).
      instance.GlobalPosition = new Vector3(
        freeObject.PositionX,
        freeObject.PositionY,
        freeObject.PositionZ
      );

      // Fix ⑤: convert the saved degrees to radians exactly ONCE (upstream
      // saved radians and re-converted, double-rotating the object).
      instance.RotateY(Mathf.DegToRad(freeObject.RotationDegreesY));
    }

    return true;
  }

  /// <summary>Loads the most recent building save from the folder.</summary>
  /// <returns>True when a save existed and was loaded; false otherwise.</returns>
  public static bool LoadMostRecent(
    BuildableResourceLibrary library,
    IReadOnlyList<BuildingSystemGrid> grids,
    Node freeObjectContainer,
    uint freeLayerMask,
    string folder = JsonSaveSystem.DEFAULT_FOLDER
  )
  {
    var saveFiles = JsonSaveSystem.GetSaveFilesInfo(folder);
    return saveFiles.Count > 0
      && Load(saveFiles[0].FullName, library, grids, freeObjectContainer, freeLayerMask, folder);
  }

  /// <summary>
  ///   Converts a grid into its save DTO. Multi-cell objects are collected
  ///   once via the dedup set (upstream wrote them once per occupied cell
  ///   and relied on the load failing to re-place the duplicates).
  /// </summary>
  private static SaveGrid ToSaveGrid(int index, BuildingSystemGrid grid)
  {
    var objects = new List<SaveGridObject>();
    var seen = new HashSet<BuildableInstance>();
    foreach (var cell in grid.GridCells)
    {
      if (cell.GroundObject is { } groundObject && seen.Add(groundObject))
      {
        objects.Add(ToSaveGridObject(groundObject));
      }

      foreach (var wallObject in cell.WallObjects)
      {
        if (wallObject is not null && seen.Add(wallObject))
        {
          objects.Add(ToSaveGridObject(wallObject));
        }
      }
    }

    return new SaveGrid { Index = index, Objects = objects };
  }

  /// <summary>Converts a grid-placed instance into its save DTO.</summary>
  private static SaveGridObject ToSaveGridObject(BuildableInstance instance) =>
    new()
    {
      Name = instance.BuildableResource.Name,
      ResourcePath = instance.BuildableResource.ResourcePath,
      // Fix ⑤: degrees — upstream wrote the degrees value into a field
      // named "radiants", corrupting the semantics.
      RotationDegreesY = instance.RotationDegrees.Y,
      PositionX = instance.GlobalPosition.X,
      PositionZ = instance.GlobalPosition.Z
    };

  /// <summary>Converts a free instance into its save DTO (LOCAL position).</summary>
  private static SaveFreeObject ToSaveFreeObject(BuildableInstance instance) =>
    new()
    {
      Name = instance.BuildableResource.Name,
      ResourcePath = instance.BuildableResource.ResourcePath,
      // Fix ⑤: degrees from the rotated BuildableInstance (upstream read
      // ObjectInstance.Rotation.Y in radians — always 0 — and lost the
      // rotation entirely).
      RotationDegreesY = instance.RotationDegrees.Y,
      // Local position relative to the free object container; Load assigns
      // it as the global position.
      PositionX = instance.Position.X,
      PositionY = instance.Position.Y,
      PositionZ = instance.Position.Z
    };

  /// <summary>Strips the folder from a save path, leaving the file name.</summary>
  private static string StripFolder(string path)
  {
    var slash = path.LastIndexOf('/');
    return slash >= 0 ? path[(slash + 1)..] : path;
  }
}
