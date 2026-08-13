// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure static grid math, extracted from the upstream
///   <c>BuildingSystemGrid</c> methods so it is unit-testable without a scene
///   tree. Two upstream bugs are fixed here and locked by GridMathTest:
///   - Fix ① (<see cref="IsValidGridIndex"/>): upstream checked
///     <c>x &lt;= _xSize</c> (one cell too far) and required x/y &gt; 0, which
///     wrongly rejected row 0 and column 0.
///   - Fix ② (<see cref="SnappedPosition"/>): upstream rounded the raw
///     distance from the grid origin without dividing by the cell size, so a
///     cell size other than 1 snapped to the wrong points.
/// </summary>
public static class GridMath
{
  /// <summary>
  ///   Converts a world position into grid indices, following the upstream
  ///   GetGridPosition math. The grid origin (<c>gridGlobalPosition</c>) is
  ///   the world position of the grid node; negative world coordinates fall
  ///   into cell 0 via floor rounding.
  /// </summary>
  public static Vector2I GridPosition(
    Vector3 worldPosition,
    Vector3 gridGlobalPosition,
    int xSize,
    int zSize,
    float cellSize
  )
  {
    var x = Mathf.FloorToInt((worldPosition.X - gridGlobalPosition.X) / cellSize)
      + (xSize / 2);
    var z = Mathf.FloorToInt((worldPosition.Z - gridGlobalPosition.Z) / cellSize)
      + (zSize / 2);
    return new Vector2I(x, z);
  }

  /// <summary>
  ///   Returns the world position of the CENTER of cell (x, z), following the
  ///   upstream GetCellGlobalPosition math.
  /// </summary>
  public static Vector3 CellGlobalPosition(
    Vector3 gridGlobalPosition,
    int xSize,
    int zSize,
    int x,
    int z,
    float cellSize
  )
  {
    return (new Vector3(x, 0, z) * cellSize)
      + gridGlobalPosition
      - new Vector3(xSize / 2f, 0, zSize / 2f)
      + new Vector3(cellSize / 2f, 0, cellSize / 2f);
  }

  /// <summary>
  ///   Fix ①: a grid index is valid only when strictly inside
  ///   <c>[0, xSize)</c> x <c>[0, zSize)</c>. Upstream used
  ///   <c>x &lt;= _xSize &amp;&amp; y &lt;= _zSize &amp;&amp; x &gt; 0 &amp;&amp; y &gt; 0</c>
  ///   which rejected the first row/column and accepted one cell past the
  ///   edge.
  /// </summary>
  public static bool IsValidGridIndex(int x, int z, int xSize, int zSize) =>
    x >= 0 && x < xSize && z >= 0 && z < zSize;

  /// <summary>
  ///   Snaps a world position to the grid, following the upstream
  ///   GetMouseSnappedPosition math with fix ②: divide by the cell size
  ///   BEFORE rounding and multiply back after, so any cell size snaps
  ///   correctly (upstream only worked for cellSize == 1). The odd-size
  ///   offset logic (upstream lines 117-137) is kept verbatim: objects whose
  ///   footprint is an odd number of cells snap to cell centers instead of
  ///   grid lines, and the half-cell offset flips sign to stay inside the
  ///   cell the mouse is actually over.
  /// </summary>
  /// <param name="mousePosition">World position of the mouse hit.</param>
  /// <param name="gridGlobalPosition">World position of the grid origin.</param>
  /// <param name="cellSize">Size of one cell in world units.</param>
  /// <param name="hasXOffset">Footprint has an odd X size.</param>
  /// <param name="hasZOffset">Footprint has an odd Z size.</param>
  /// <param name="yRotationInDegrees">Current rotation of the object.</param>
  public static Vector3 SnappedPosition(
    Vector3 mousePosition,
    Vector3 gridGlobalPosition,
    float cellSize,
    bool hasXOffset,
    bool hasZOffset,
    float yRotationInDegrees
  )
  {
    // Fix ②: upstream rounded (mousePosition - GlobalPosition) directly,
    // which is only correct when cellSize == 1.
    var delta = mousePosition - gridGlobalPosition;
    var x = Mathf.Round(delta.X / cellSize) * cellSize;
    var z = Mathf.Round(delta.Z / cellSize) * cellSize;

    var xOffset = 0f;
    var zOffset = 0f;

    if (hasXOffset || hasZOffset)
    {
      // If the footprint is not even on an axis, offset by half a cell to
      // snap at the cell midpoint instead of the grid line/border.
      if (Mathf.Abs(yRotationInDegrees) != 90f)
      {
        xOffset = hasXOffset ? cellSize / 2f : 0f;
        zOffset = hasZOffset ? cellSize / 2f : 0f;
      }
      else
      {
        xOffset = hasZOffset ? cellSize / 2f : 0f;
        zOffset = hasXOffset ? cellSize / 2f : 0f;
      }

      // Keep the snap point inside the current cell (upstream lines 133-134).
      if (xOffset != 0f)
      {
        xOffset *= x < delta.X ? 1f : -1f;
      }

      if (zOffset != 0f)
      {
        zOffset *= z < delta.Z ? 1f : -1f;
      }
    }

    return new Vector3(x + xOffset, 0, z + zOffset) + gridGlobalPosition;
  }
}
