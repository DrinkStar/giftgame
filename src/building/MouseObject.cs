// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>Represents a 3D mouse object in a grid building system.</summary>
/// <remarks>
///   This class is used to represent a 3D object that is controlled by the
///   mouse in a grid-based building system. It contains methods to update the
///   visual representation of the mouse object, rotate the object, check for
///   collisions, and manage the visual collision tile grid.
/// </remarks>
public partial class MouseObject : Node3D
{
  #region Public Variables

  /// <summary>Gets whether the buildable resource has an X offset.</summary>
  /// <remarks>
  ///   The X offset is used as optimization to center the buildable resource
  ///   on the grid.
  /// </remarks>
  public bool HasXOffset { get; private set; }

  /// <summary>Gets whether the buildable resource has a Z offset.</summary>
  /// <remarks>
  ///   The Z offset is used as optimization to center the buildable resource
  ///   on the grid.
  /// </remarks>
  public bool HasZOffset { get; private set; }

  #endregion Public Variables

  #region Private Variables

  private Node3D? _mouseNodeChild;
  private List<MouseTile> _mouseTiles = [];
  private bool _tileGridVisible;

  #endregion Private Variables

  #region OnReady Variables

  private Node3D _objectContainer = default!;
  private Node3D _gridContainer = default!;
  private PackedScene _mouseTile = default!;

  /// <summary>
  ///   Mouse tiles detect placed buildings (plan Decision 3): buildings'
  ///   colliders live on layer 5 (1 &lt;&lt; 4) so the tiles turn red when the
  ///   ghost overlaps an existing building. The BuildingSystem re-applies its
  ///   exported FloorLayerMask in _Ready.
  /// </summary>
  private uint _layerMask = 1u << 4;

  #endregion OnReady Variables

  #region Built-In Methods

  /// <inheritdoc/>
  public override void _Ready()
  {
    _objectContainer = GetNode<Node3D>("ObjectContainer");
    _gridContainer = GetNode<Node3D>("GridContainer");
    _mouseTile = GD.Load<PackedScene>("res://scenes/building/mouse_tile.tscn");

    GameEvents.MouseTileBodyEntered += OnMouseTileBodyEntered;
    GameEvents.MouseTileBodyExited += OnMouseTileBodyExited;
  }

  /// <inheritdoc/>
  public override void _ExitTree()
  {
    // FIX(iter4-plan): Plan Decision 13: static bus subscriptions must be released on exit.
    GameEvents.MouseTileBodyEntered -= OnMouseTileBodyEntered;
    GameEvents.MouseTileBodyExited -= OnMouseTileBodyExited;
  }

  #endregion Built-In Methods

  #region Public Methods

  /// <summary>Sets the layer mask for the mouse tiles.</summary>
  /// <param name="layerMask">The layer mask to set.</param>
  public void SetLayerMask(uint layerMask)
  {
    _layerMask = layerMask;
    if (_mouseTiles.Count > 0)
    {
      foreach (var tile in _mouseTiles)
      {
        tile.SetLayerMask(layerMask);
      }
    }
  }

  /// <summary>Updates the visual representation of the mouse object with the specified buildable object.</summary>
  /// <param name="buildableObject">The buildable object to update the visual representation with.</param>
  public void UpdateVisual(BuildableResource buildableObject)
  {
    ClearMouseObject();
    var objectInstance = buildableObject.Object3DModel!.Instantiate<Node3D>();

    _objectContainer.AddChild(objectInstance);
    _mouseNodeChild = objectInstance;

    HasXOffset = buildableObject.Size.X % 2 != 0;
    HasZOffset = buildableObject.Size.Z % 2 != 0;

    CreateTilesGrid(new Vector2I(buildableObject.Size.X, buildableObject.Size.Z));
  }

  /// <summary>Clears the mouse object and the grid.</summary>
  public void ClearMouseObject()
  {
    _mouseNodeChild?.QueueFree();
    _mouseNodeChild = null;
    ClearGrid();
  }

  /// <summary>Clears the grid and removes all mouse tiles.</summary>
  public void ClearGrid()
  {
    foreach (var child in _mouseTiles)
    {
      child.QueueFree();
    }

    _mouseTiles = [];
    ResetRotation(_gridContainer);
  }

  /// <summary>Rotates the mouse object and the grid by the specified rotation in degrees.</summary>
  /// <param name="rotation">The rotation in degrees.</param>
  public void Rotate(int rotation)
  {
    if (_mouseNodeChild != null)
    {
      ResetRotation(_mouseNodeChild);
    }

    ResetRotation(_gridContainer);

    // Finally rotate the object.
    _mouseNodeChild?.RotateY(Mathf.DegToRad(rotation));
    _gridContainer.RotateY(Mathf.DegToRad(rotation));
  }

  /// <summary>Checks if the mouse object exists.</summary>
  public bool HasMouseObject() => _mouseNodeChild != null;

  /// <summary>Gets the Y rotation of the mouse object in radians.</summary>
  public float GetYRotationInRadians() => _mouseNodeChild?.Rotation.Y ?? 0f;

  /// <summary>Gets the Y rotation of the mouse object in degrees.</summary>
  public float GetYRotationInDegrees() => _mouseNodeChild?.RotationDegrees.Y ?? 0f;

  /// <summary>Checks if the mouse object is colliding.</summary>
  public bool IsColliding() => _mouseNodeChild != null && !_mouseNodeChild.Visible;

  /// <summary>Checks if any of the mouse object's children are colliding.</summary>
  public bool AreChildrenColliding()
  {
    foreach (var child in _mouseTiles)
    {
      if (child.IsColliding())
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>Sets the visibility of the tile grid.</summary>
  /// <param name="visible">Whether the tile grid should be visible or not.</param>
  public void SetTileGridVisible(bool visible)
  {
    foreach (var child in _mouseTiles)
    {
      child.SetVisibility(visible);
    }

    _tileGridVisible = visible;
  }

  #endregion Public Methods

  #region Private Methods

  private static void ResetRotation(Node3D node)
  {
    var transform = node.Transform;
    transform.Basis = Basis.Identity;
    node.Transform = transform;
  }

  private void CreateTilesGrid(Vector2I size, int cellSize = 1)
  {
    var width = size.X * cellSize;
    var height = size.Y * cellSize;

    for (var i = 0; i < size.X; i++)
    {
      for (var j = 0; j < size.Y; j++)
      {
        var mouseTileInstance = _mouseTile.Instantiate<MouseTile>();
        _gridContainer.AddChild(mouseTileInstance);
        mouseTileInstance.SetLayerMask(_layerMask);
        if (cellSize != BSConstants.DEFAULT_CELL_SIZE)
        {
          mouseTileInstance.SetSize(cellSize);
        }

        mouseTileInstance.Position = new Vector3(
          i - (width / 2f) + (cellSize / 2f),
          0.15f,
          j - (height / 2f) + (cellSize / 2f)
        );
        _mouseTiles.Add(mouseTileInstance);
      }
    }
  }

  #endregion Private Methods

  #region Event Handlers

  private void OnMouseTileBodyEntered()
  {
    if (_mouseNodeChild != null)
    {
      _mouseNodeChild.Visible = false;
    }

    if (!_tileGridVisible)
    {
      SetTileGridVisible(true);
    }
  }

  private void OnMouseTileBodyExited()
  {
    if (!AreChildrenColliding())
    {
      if (_mouseNodeChild != null)
      {
        _mouseNodeChild.Visible = true;
      }

      SetTileGridVisible(false);
    }
  }

  #endregion Event Handlers
}
