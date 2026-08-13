// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>Represents a grid-based building system in a 3D space.</summary>
public partial class BuildingSystemGrid : Node3D
{
  /// <summary>Gets or sets the array of grid cells in the building system.</summary>
  public GridCell[] GridCells { get; private set; } = [];

  private int _xSize = 200;
  private int _zSize = 200;
  private float _cellSize = 1f;
  private float _cellHeight = 3f;
  private uint _floorLayerMask;
  private uint _wallLayerMask;
  private MeshInstance3D? _gridVisual;

  /// <inheritdoc/>
  public override void _Ready()
  {
    // Plan Decision 3: the upstream GroundStaticBody3D child was removed from
    // the scene; the build ray shoots at the existing Ground (layer 1) in
    // Game.tscn instead, so this grid no longer owns a ground body.
    _gridVisual = GetNode<MeshInstance3D>("GridVisual");
  }

  /// <summary>Initializes the building system grid with the specified parameters.</summary>
  /// <param name="xSize">The size of the grid along the X-axis.</param>
  /// <param name="zSize">The size of the grid along the Z-axis.</param>
  /// <param name="cellSize">The size of each grid cell.</param>
  /// <param name="cellHeight">The height of each grid cell.</param>
  /// <param name="floorLayerMask">The layer mask for the floor objects.</param>
  /// <param name="wallLayerMask">The layer mask for the wall objects.</param>
  public void Initialize(
    int xSize,
    int zSize,
    float cellSize,
    float cellHeight,
    uint floorLayerMask,
    uint wallLayerMask
  )
  {
    _xSize = xSize;
    _zSize = zSize;
    _cellSize = cellSize;
    _cellHeight = cellHeight;
    GridCells = new GridCell[_xSize * _zSize];
    for (var i = 0; i < GridCells.Length; i++)
    {
      GridCells[i] = new GridCell();
    }

    _floorLayerMask = floorLayerMask;
    _wallLayerMask = wallLayerMask;
  }

  /// <summary>Sets the active state of the building system grid.</summary>
  /// <param name="isActive">The active state of the building system grid.</param>
  public void SetActive(bool isActive)
  {
    // Plan Decision 13: the grid visual visibility follows this flag; the
    // BuildingSystem keeps it false unless build mode is active.
    if (_gridVisual != null)
    {
      _gridVisual.Visible = isActive;
    }
  }

  /// <summary>
  ///   Gets the global position of a cell's corner in the grid (upstream
  ///   GetGlobalPosition; kept for completeness, cell math lives in
  ///   <see cref="GridMath"/>).
  /// </summary>
  public Vector3 GetGlobalPosition(int x, int z)
  {
    return (new Vector3(x, 0, z) * _cellSize)
      + GlobalPosition
      - new Vector3(_xSize / 2f, 0, _zSize / 2f);
  }

  /// <summary>Gets the global position of the CENTER of a cell in the grid.</summary>
  /// <param name="x">The X-coordinate of the cell.</param>
  /// <param name="z">The Z-coordinate of the cell.</param>
  /// <returns>The global position of the cell center.</returns>
  public Vector3 GetCellGlobalPosition(int x, int z) =>
    GridMath.CellGlobalPosition(GlobalPosition, _xSize, _zSize, x, z, _cellSize);

  /// <summary>Gets the grid position of a global position.</summary>
  /// <param name="globalPosition">The global position.</param>
  /// <returns>The grid position.</returns>
  public Vector2I GetGridPosition(Vector3 globalPosition) =>
    GridMath.GridPosition(globalPosition, GlobalPosition, _xSize, _zSize, _cellSize);

  /// <summary>Gets the snapped position of the mouse on the grid for a buildable object.</summary>
  /// <param name="mousePosition">The position of the mouse.</param>
  /// <param name="mouseObject">The buildable object to snap to the grid.</param>
  /// <returns>The snapped position of the mouse on the grid.</returns>
  public Vector3 GetMouseSnappedPosition(Vector3 mousePosition, MouseObject mouseObject)
  {
    // As we are using a layer mask to detect only the ground/grid layer
    // there is no need for an extra check whether the mouse is over the grid.
    return GridMath.SnappedPosition(
      mousePosition,
      GlobalPosition,
      _cellSize,
      mouseObject.HasXOffset,
      mouseObject.HasZOffset,
      mouseObject.GetYRotationInDegrees()
    );
  }

  /// <summary>Gets the grid cell at the specified coordinates.</summary>
  /// <param name="x">The X-coordinate of the cell.</param>
  /// <param name="z">The Z-coordinate of the cell.</param>
  /// <returns>The grid cell at the specified coordinates, or null when out of bounds.</returns>
  public GridCell? GetGridCell(int x, int z)
  {
    if (IsValidGridIndex(x, z))
    {
      return GridCells[GetGridIndexWithoutValidation(x, z)];
    }

    return null;
  }

  /// <summary>Checks if the grid index is valid.</summary>
  /// <param name="x">The X-coordinate of the index.</param>
  /// <param name="z">The Z-coordinate of the index.</param>
  /// <returns>True if the grid index is valid, false otherwise.</returns>
  public bool IsValidGridIndex(int x, int z) =>
    GridMath.IsValidGridIndex(x, z, _xSize, _zSize);

  /// <summary>
  ///   Validates that <paramref name="selectedObject"/> can be placed at
  ///   <paramref name="position"/> WITHOUT placing it (plan Decision 2). The
  ///   BuildingSystem calls this BEFORE deducting inventory cost, then calls
  ///   <see cref="TryToPlaceObject"/> which re-runs the same footprint check
  ///   and can therefore no longer fail on occupancy.
  /// </summary>
  /// <param name="selectedObject">The object to validate.</param>
  /// <param name="yRotation">The rotation of the object in degrees.</param>
  /// <param name="position">The position to place the object.</param>
  /// <returns>True when every footprint cell is free or out of bounds.</returns>
  public bool CanPlace(BuildableResource selectedObject, float yRotation, Vector3 position)
  {
    if (!GetFootprint(selectedObject, yRotation, position, out var xStart, out var zStart, out var xLength, out var zLength))
    {
      return false;
    }

    return IsAreaFree(selectedObject, yRotation, xStart, zStart, xLength, zLength);
  }

  /// <summary>
  ///   Tries to place an object on the grid. Plan Decision 2 change: returns
  ///   a bool — <c>false</c> when a footprint cell is occupied (or would be
  ///   for walls), <c>true</c> when the object was actually placed. The
  ///   BuildingSystem only deducts the material cost after <c>true</c>.
  /// </summary>
  /// <param name="selectedObject">The object to be placed.</param>
  /// <param name="yRotation">The rotation of the object in degrees.</param>
  /// <param name="position">The position to place the object.</param>
  /// <returns>True when placed, false when the area is occupied.</returns>
  public bool TryToPlaceObject(BuildableResource selectedObject, float yRotation, Vector3 position)
  {
    // As this is already placed and positioned properly, for optimization
    // there will be no additional calculations and snapping to the grid again.
    if (!GetFootprint(selectedObject, yRotation, position, out var xStart, out var zStart, out var xLength, out var zLength))
    {
      return false;
    }

    if (!IsAreaFree(selectedObject, yRotation, xStart, zStart, xLength, zLength))
    {
      return false;
    }

    var buildableInstance = BuildableInstance.Create(
      selectedObject,
      selectedObject.SnapBehaviour == SnapBehaviour.Ground ? _floorLayerMask : _wallLayerMask
    );
    AddChild(buildableInstance);
    buildableInstance.GlobalPosition = position;
    buildableInstance.RotateY(Mathf.DegToRad(yRotation));

    // Add a simple small animation when placing objects (upstream
    // AnimationUtils.AnimatePlacement, inlined because that utility class is
    // out of port scope).
    AnimatePlacement(buildableInstance.ObjectInstance);

    for (var i = xStart; i < xStart + xLength; i++)
    {
      for (var j = zStart; j < zStart + zLength; j++)
      {
        var gridCell = GetGridCell(i, j);
        if (gridCell == null)
        {
          continue;
        }

        if (selectedObject.SnapBehaviour == SnapBehaviour.Ground)
        {
          gridCell.SetGroundObject(buildableInstance);
        }
        else if (selectedObject.SnapBehaviour == SnapBehaviour.Wall)
        {
          if (Math.Abs(yRotation) != 90f)
          {
            gridCell.SetWallObject(buildableInstance, Side.MinusZ);
          }
          else
          {
            gridCell.SetWallObject(buildableInstance, Side.MinusX);
          }
        }

        buildableInstance.AddCell(gridCell);
      }
    }

    return true;
  }

  /// <summary>Resets the building system grid by clearing all objects from the grid cells.</summary>
  public void ResetGrid()
  {
    foreach (var cell in GridCells)
    {
      cell.GroundObject?.ClearObject();
      foreach (var wall in cell.WallObjects)
      {
        wall?.ClearObject();
      }
    }
  }

  /// <summary>
  ///   Computes the footprint of <paramref name="selectedObject"/> at
  ///   <paramref name="position"/>, following the upstream size/rotation math
  ///   in TryToPlaceObject. Walls clamp their zero thickness to 1 cell.
  ///   Returns false when the footprint's anchor cell is outside the grid —
  ///   upstream placed such objects as unregistered ghosts that could never
  ///   be demolished or saved (part of its unhandled max-edge TODO); we treat
  ///   them as invalid instead.
  /// </summary>
  private bool GetFootprint(
    BuildableResource selectedObject,
    float yRotation,
    Vector3 position,
    out int xStart,
    out int zStart,
    out int xLength,
    out int zLength
  )
  {
    // Get object size based on rotation.
    xLength = Math.Abs(yRotation) != 90f ? selectedObject.Size.X : selectedObject.Size.Z;
    zLength = Math.Abs(yRotation) != 90f ? selectedObject.Size.Z : selectedObject.Size.X;

    // This is for the walls.
    if (xLength == 0)
    {
      xLength = 1;
    }

    if (zLength == 0)
    {
      zLength = 1;
    }

    // Split by 2 so we can get the start position.
    var gridPosition = GetGridPosition(position);
    if (!IsValidGridIndex(gridPosition.X, gridPosition.Y))
    {
      xStart = 0;
      zStart = 0;
      return false;
    }

    xStart = gridPosition.X - (xLength / 2);
    zStart = gridPosition.Y - (zLength / 2);
    return true;
  }

  /// <summary>
  ///   Occupancy check shared by <see cref="CanPlace"/> and
  ///   <see cref="TryToPlaceObject"/> (upstream inline loops, extracted).
  ///   Out-of-bounds cells pass the check — the upstream TODO about walls on
  ///   the max edge is inherited.
  /// </summary>
  private bool IsAreaFree(
    BuildableResource selectedObject,
    float yRotation,
    int xStart,
    int zStart,
    int xLength,
    int zLength
  )
  {
    // Without this check it can happen that by dragging or fast clicking an
    // object gets placed on an existing one or overlaps it.
    for (var i = xStart; i < xStart + xLength; i++)
    {
      for (var j = zStart; j < zStart + zLength; j++)
      {
        var gridCell = GetGridCell(i, j);
        if (selectedObject.SnapBehaviour == SnapBehaviour.Ground)
        {
          if (gridCell?.HasGroundObject() == true)
          {
            return false;
          }
        }
        else if (selectedObject.SnapBehaviour == SnapBehaviour.Wall && gridCell != null)
        {
          if (Math.Abs(yRotation) != 90f)
          {
            if (gridCell.HasWallObject(Side.MinusZ))
            {
              return false;
            }
          }
          else if (gridCell.HasWallObject(Side.MinusX))
          {
            return false;
          }
        }
      }
    }

    return true;
  }

  private void AnimatePlacement(Node3D target)
  {
    var originalScale = target.Scale;
    target.Scale *= 0.8f;
    var tween = CreateTween();
    tween.TweenProperty(target, "scale", originalScale, 0.1)
      .SetTrans(Tween.TransitionType.Bounce);
    tween.Play();
  }

  private int GetGridIndexWithoutValidation(int x, int z) => (x * _zSize) + z;
}
