// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

/// <summary>Represents a cell in a grid-based building system.</summary>
public class GridCell
{
  /// <summary>Gets the ground object placed on this grid cell.</summary>
  public BuildableInstance? GroundObject { get; private set; }

  /// <summary>
  ///   Gets the wall objects placed on this grid cell. Fix ⑦: the model only
  ///   stores TWO sides (index 0 = <see cref="Side.MinusZ"/>, index 1 =
  ///   <see cref="Side.MinusX"/>) although the <see cref="Side"/> enum has
  ///   four values. Upstream indexed this array with the raw enum value, so
  ///   <see cref="Side.Z"/> and <see cref="Side.X"/> read/wrote out of
  ///   bounds. All accessors now guard the side before indexing.
  /// </summary>
  public BuildableInstance?[] WallObjects { get; private set; }

  /// <summary>Initializes a new instance of the <see cref="GridCell"/> class.</summary>
  public GridCell()
  {
    GroundObject = null;
    WallObjects = new BuildableInstance?[2];
  }

  /// <summary>Sets the ground object on this grid cell.</summary>
  /// <param name="buildableInstance">The buildable instance representing the ground object.</param>
  public void SetGroundObject(BuildableInstance buildableInstance)
  {
    if (buildableInstance.BuildableResource.SnapBehaviour == SnapBehaviour.Ground)
    {
      GroundObject = buildableInstance;
    }
  }

  /// <summary>Clears the ground object from this grid cell.</summary>
  public void ClearGroundObject()
  {
    GroundObject = null;
  }

  /// <summary>Sets the wall object on this grid cell.</summary>
  /// <param name="buildableInstance">The buildable instance representing the wall object.</param>
  /// <param name="side">The <see cref="Side"/> of the grid cell where the wall object is placed.</param>
  public void SetWallObject(BuildableInstance buildableInstance, Side side)
  {
    // Fix ⑦: ignore sides outside the two-slot model instead of indexing
    // out of bounds.
    if (buildableInstance.BuildableResource.SnapBehaviour != SnapBehaviour.Wall)
    {
      return;
    }

    if (TryGetSideIndex(side, out var index))
    {
      WallObjects[index] = buildableInstance;
    }
  }

  /// <summary>Clears the specified wall object from this grid cell.</summary>
  /// <param name="wall">The wall object to clear.</param>
  public void ClearWallObject(BuildableInstance wall)
  {
    for (var i = 0; i < WallObjects.Length; i++)
    {
      if (WallObjects[i] == wall)
      {
        WallObjects[i] = null;
      }
    }
  }

  /// <summary>Clears the wall object from the specified side of this grid cell.</summary>
  /// <param name="side">The <see cref="Side"/> of the grid cell where the wall object is placed.</param>
  public void ClearWallObject(Side side)
  {
    // Fix ⑦: same bounds guard as SetWallObject.
    if (TryGetSideIndex(side, out var index))
    {
      WallObjects[index] = null;
    }
  }

  /// <summary>
  ///   Determines whether this grid cell has a ground object.
  /// </summary>
  /// <returns><c>true</c> if this grid cell has a ground object; otherwise, <c>false</c>.</returns>
  public bool HasGroundObject() => GroundObject != null;

  /// <summary>
  ///   Determines whether this grid cell has a wall object on the specified
  ///   side.
  /// </summary>
  /// <param name="side">The <see cref="Side"/> of the grid cell to check.</param>
  /// <returns><c>true</c> if this grid cell has a wall object on the specified side; otherwise, <c>false</c>.</returns>
  public bool HasWallObject(Side side) =>
    TryGetSideIndex(side, out var index) && WallObjects[index] != null;

  /// <summary>Gets the wall object placed on the specified side of this grid cell.</summary>
  /// <param name="side">The <see cref="Side"/> of the grid cell to get the wall object from.</param>
  /// <returns>The wall object placed on the specified side of this grid cell.</returns>
  public BuildableInstance? GetWallObject(Side side) =>
    TryGetSideIndex(side, out var index) ? WallObjects[index] : null;

  /// <summary>
  ///   Fix ⑦ helper: maps a <see cref="Side"/> to a slot in the two-entry
  ///   wall array. The model intentionally only supports the two sides that
  ///   exist as array entries (MinusZ, MinusX); the other enum values are
  ///   unsupported and return false instead of writing out of bounds.
  /// </summary>
  private static bool TryGetSideIndex(Side side, out int index)
  {
    index = (int)side;
    return side is Side.MinusZ or Side.MinusX;
  }
}
