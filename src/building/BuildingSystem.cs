// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   This is the main class and represents a building system in a 3D
///   environment. This class handles object placement, demolition, and other
///   related functionalities.
///
///   Integration fixes vs upstream (plan Decisions):
///   - EventBus replaced by the static <see cref="GameEvents"/> hub.
///   - Material cost is checked AFTER occupancy validation and BEFORE the
///     actual placement, so a failed payment never spawns a building
  ///     (FIX(iter4-plan): Decision 2).
  ///   - SetBuildMode toggles the mouse cursor Visible/Captured (FIX(iter4-plan): Decision 6);
///     the W3 GameManager is expected to call SetBuildMode(false) before
///     pausing (pause takes priority over build mode).
  ///   - PageUp/PageDown skip the camera move when Levels == 1 (FIX(iter4-plan): Decision 14).
  ///   - The grid visual follows build mode (FIX(iter4-plan): Decision 13).
  ///   - F5/F9 quick save/load are wired to the W2 BuildingSaveSystem
  ///     (FIX(iter4-plan): Decision 7); free objects stay out of the save this iteration
  ///     (plan OUT list).
/// </summary>
public partial class BuildingSystem : Node3D
{
  #region Export Variables

  /// <summary>The main camera used in the building system.</summary>
  [ExportGroup("Dependencies")]
  [Export] public Camera3D? MainCamera { get; set; }

  /// <summary>
  ///   The player node (W3 wires it to the Player scene node). Used to reach
  ///   the player's InventorySystem for material cost deduction.
  /// </summary>
  [Export] public PlayerController? Player { get; set; }

  /// <summary>Gets or sets the library of buildable resources.</summary>
  [Export] public BuildableResourceLibrary? BuildableObjectLibrary { get; set; }

  /// <summary>The size of each cell in the grid.</summary>
  [Export] public float CellSize { get; set; } = BSConstants.DEFAULT_CELL_SIZE;

  /// <summary>The height of each cell in the grid.</summary>
  [Export] public float CellHeight { get; set; } = BSConstants.DEFAULT_CELL_HEIGHT;

  /// <summary>The layer mask for the ground.</summary>
  [Export(PropertyHint.Layers3DPhysics)] public uint GroundLayerMask { get; set; } =
    BSConstants.DEFAULT_GROUND_LAYER_MASK;

  /// <summary>The layer mask for the floor.</summary>
  [Export(PropertyHint.Layers3DPhysics)] public uint FloorLayerMask { get; set; } =
    BSConstants.DEFAULT_FLOOR_LAYER_MASK;

  /// <summary>The layer mask for the walls.</summary>
  [Export(PropertyHint.Layers3DPhysics)] public uint WallLayerMask { get; set; } =
    BSConstants.DEFAULT_WALL_LAYER_MASK;

  /// <summary>The layer mask for free objects.</summary>
  [Export(PropertyHint.Layers3DPhysics)] public uint FreeLayerMask { get; set; } =
    BSConstants.DEFAULT_FREE_LAYER_MASK;

  /// <summary>The size of the grid in the X direction.</summary>
  [Export] public int XSize { get; set; } = BSConstants.DEFAULT_GRID_X_SIZE;

  /// <summary>The size of the grid in the Z direction.</summary>
  [Export] public int ZSize { get; set; } = BSConstants.DEFAULT_GRID_Z_SIZE;

  /// <summary>The number of levels in the grid.</summary>
  [Export] public int Levels { get; set; } = BSConstants.DEFAULT_GRID_LEVELS;

  /// <summary>The drag behavior for placing objects.</summary>
  [Export] public DragBehavior DragBehavior { get; set; } = DragBehavior.InstantPlacement;

  /// <summary>The threshold for delayed placement.</summary>
  [Export] public float DragThreshold { get; set; } = BSConstants.DEFAULT_DRAG_THRESHOLD;

  /// <summary>The offset of the drag visual from the ground.</summary>
  [Export] public float DragVisualGroundOffset { get; set; } =
    BSConstants.DEFAULT_DRAG_VISUAL_GROUND_OFFSET;

  /// <summary>Gets or sets a value indicating whether the ground mouse is visible.</summary>
  /// <value><c>true</c> if the ground mouse is visible; otherwise, <c>false</c>.</value>
  [ExportSubgroup("Debug")]
  [Export] public bool GroundMouseVisible { get; set; }

  #endregion Export Variables

  #region Private Variables

  private uint _demolishLayerMask;
  private List<BuildableInstance> _freeObjectsList = [];
  private List<BuildingSystemGrid> _grids = [];
  private BuildingSystemGrid _activeGrid = default!;
  private bool _isBuildModeActive;
  private bool _isDemolishModeActive;
  private BuildableResource? _selectedObject;
  private Vector3 _currentMousePosition;
  private Vector3 _lastSnappedPosition = Vector3.Zero;
  private bool _isMousePressed;
  private int _currentRotation;
  private uint _activeLayerMask;
  private Vector3 _mouseStartPosition;
  private Vector3 _mouseEndPosition;
  private MeshInstance3D? _dragRectangleMesh;
  private Vector2 _mousePressPosition;

  /// <summary>
  ///   The save file the last quick-save wrote to (W3). Null until the first
  ///   F5. FIX(code-review P2-22): the BuildingLoaded/BuildingSaved events
  ///   were dead API (no trigger, no subscriber) and were removed from
  ///   GameEvents; this field now only feeds LoadMostRecent's path recovery.
  /// </summary>
  private string? _currentSaveFile;

  #endregion Private Variables

  #region OnReady Variables

  private Node3D _gridContainer = default!;
  private Node3D _freeObjectsContainer = default!;
  private Node3D _groundMouseNode = default!;
  private MouseObject _mouseObject = default!;
  private BuildingMenu _objectMenu = default!;
  private PackedScene _gridResource = default!;
  private Node3D? _demolishCollider;

  #endregion OnReady Variables

  /// <summary>
  ///   Public enumeration API (plan Decision 9): read-only view of the grids,
  ///   used by the W2 save system, the W3 StationLinker, and tests.
  /// </summary>
  public IReadOnlyList<BuildingSystemGrid> Grids => _grids;

  #region Built-In Methods

  /// <inheritdoc/>
  public override void _Ready()
  {
    BuildingInput.RegisterInputActions();
    _freeObjectsList = [];
    _grids = [];

    // Combine all layer masks for demolition (Decision 3: buildings live on
    // the floor layer; walls and free objects keep their own masks).
    _demolishLayerMask = FloorLayerMask | WallLayerMask | FreeLayerMask;

    // Set active layer mask to ground.
    _activeLayerMask = GroundLayerMask;

    // Get all necessary nodes.
    _gridContainer = GetNode<Node3D>("GridContainer");
    _freeObjectsContainer = GetNode<Node3D>("FreeObjectsContainer");
    _mouseObject = GetNode<MouseObject>("MouseObject");
    _gridResource = GD.Load<PackedScene>("res://scenes/building/building_system_grid.tscn");

    // For debug purposes.
    _groundMouseNode = GetNode<Node3D>("GroundMouseDebug");

    // Get UI nodes.
    _objectMenu = GetNode<BuildingMenu>("UI/BuildingMenu");

    // Populate the object menu and register UI events. The library is null
    // when the scene is instantiated without wiring (tests); W3 wires
    // buildable_library.tres in Game.tscn.
    if (BuildableObjectLibrary != null)
    {
      _objectMenu.PopulateObjectGrid(BuildableObjectLibrary);
    }

    GameEvents.BuildingSlotClicked += OnObjectMenuInteract;

    // Mouse tiles must detect placed buildings (Decision 3: layer 16).
    _mouseObject.SetLayerMask(FloorLayerMask);

    // Initialize grids. FIX(iter4-plan): Decision 4: the grid is positioned with LOCAL
    // Position (upstream used GlobalPosition, which breaks when the
    // GridContainer is offset — e.g. y=0.5 on the ground top in Game.tscn).
    for (var i = 0; i < Levels; i++)
    {
      var gridInstance = _gridResource.Instantiate<BuildingSystemGrid>();
      _gridContainer.AddChild(gridInstance);
      gridInstance.Initialize(XSize, ZSize, CellSize, CellHeight, FloorLayerMask, WallLayerMask);
      gridInstance.Name = $"Level{i}";
      gridInstance.Position = new Vector3(0, i * CellHeight, 0);
      _grids.Add(gridInstance);
    }

    // Set active grid to the first grid.
    _activeGrid = _grids[0];
    RefreshActiveGridVisual();

    _groundMouseNode.Visible = GroundMouseVisible;
  }

  /// <inheritdoc/>
  public override void _ExitTree()
  {
    GameEvents.BuildingSlotClicked -= OnObjectMenuInteract;
  }

  /// <inheritdoc/>
  public override void _PhysicsProcess(double delta)
  {
    // When build mode is active we need to raycast to be able to snap objects
    // to the grid. As for demolition mode, this is useful if you want to show
    // a visual indicator which object will be demolished, as is currently
    // implemented. If you don't need it, just remove _isDemolishModeActive.
    if (!GetTree().Paused && (_isBuildModeActive || _isDemolishModeActive))
    {
      if (MainCamera == null)
      {
        return;
      }

      // This is the most reliable place to cast a ray by Godot documentation.
      var spaceState = GetWorld3D().DirectSpaceState;
      var mousePos = GetViewport().GetMousePosition();

      // Use global coordinates, not local to node.
      var origin = MainCamera.ProjectRayOrigin(mousePos);
      var end = origin + (MainCamera.ProjectRayNormal(mousePos) * 999f);
      var query = PhysicsRayQueryParameters3D.Create(origin, end, _activeLayerMask);
      var result = spaceState.IntersectRay(query);

      if (result.Count > 0)
      {
        _currentMousePosition = (Vector3)result["position"];
        if (_activeLayerMask == _demolishLayerMask)
        {
          if (IsInstanceValid(_demolishCollider))
          {
            _demolishCollider?.GetParent<BuildableInstance>()?.SetDemolitionView(false);
          }

          _demolishCollider = (Node3D)result["collider"];
          _demolishCollider.GetParent<BuildableInstance>()?.SetDemolitionView(true);
        }
      }
      else if (_activeLayerMask == _demolishLayerMask && _demolishCollider != null)
      {
        if (IsInstanceValid(_demolishCollider))
        {
          _demolishCollider?.GetParent<BuildableInstance>()?.SetDemolitionView(false);
        }

        _demolishCollider = null;
      }

      if (_activeLayerMask == GroundLayerMask)
      {
        UpdateMouseObjectVisual(delta);
      }
    }
  }

  /// <inheritdoc/>
  public override void _Input(InputEvent @event)
  {
    // FIX(iter8-plan): T8.4 — a modal overlay (CraftUI) may lock gameplay
    // input; the building hotkeys must not fire underneath it.
    if (GameEvents.GameplayInputLocked)
    {
      return;
    }

    if (@event.IsActionPressed("build_mode"))
    {
      SetBuildMode(!_isBuildModeActive);
    }

    if (@event.IsActionPressed("demolish"))
    {
      SetDemolitionMode(!_isDemolishModeActive);
      if (_isDemolishModeActive && _selectedObject != null)
      {
        _selectedObject = null;
        ClearMouseObject();
      }
    }

    // FIX(iter8-plan): T8.1 — F5/F9 quick save/load moved to SaveService
    // (src/core/save/SaveService.cs), which owns the unified game save.
    // BuildingInput still registers the actions; SaveService handles them.

    if (!GetTree().Paused)
    {
      HandleGridLevelChangeEvent(@event);
      HandleObjectRotationEvent(@event);
      HandleCancelEvent(@event);
    }
  }

  /// <inheritdoc/>
  public override void _UnhandledInput(InputEvent @event)
  {
    // This is here to prevent placing objects when the mouse is over UI
    // elements.
    HandleObjectPlacementEvent(@event);
    HandleObjectDemolishEvent(@event);
  }

  #endregion Built-In Methods

  #region Object Placing Logic

  private void TryToPlaceObject()
  {
    // Duplicate() can be used instead of a new object instance, but because
    // it would not duplicate the script with an initialization parameter,
    // which limits customization, it is not used.
    if (_selectedObject == null)
    {
      return;
    }

    if (_selectedObject.SnapBehaviour != SnapBehaviour.Free)
    {
      // Handle the object in the grid as it is snappable.
      // FIX(iter4-plan): Decision 2 atomicity: validate occupancy BEFORE paying, then pay,
      // then place. CanPlace and TryToPlaceObject share the same footprint
      // check, so a placement that passed CanPlace cannot fail — the cost is
      // only deducted when the object is guaranteed to land.
      if (!_activeGrid.CanPlace(_selectedObject, _mouseObject.GetYRotationInDegrees(), _lastSnappedPosition))
      {
        return;
      }

      if (!TryConsumeCost(_selectedObject))
      {
        return;
      }

      _activeGrid.TryToPlaceObject(
        _selectedObject,
        _mouseObject.GetYRotationInDegrees(),
        _lastSnappedPosition
      );
    }
    else
    {
      // Handle not snappable (or free) objects here as they do not belong to
      // the grid. Free objects skip the material cost entirely this
      // iteration (no free object use case).
      var buildableInstance = BuildableInstance.Create(
        _selectedObject,
        GetLayerMask(_selectedObject.SnapBehaviour)
      );
      _freeObjectsContainer.AddChild(buildableInstance);
      _freeObjectsList.Add(buildableInstance);
      buildableInstance.GlobalPosition = new Vector3(
        _currentMousePosition.X,
        _activeGrid.GlobalPosition.Y,
        _currentMousePosition.Z
      );
      buildableInstance.RotateY(_mouseObject.GetYRotationInRadians());

      AnimatePlacement(buildableInstance.ObjectInstance);
    }

    // FIX(iter7-plan): quest hook — building placed. Both success paths
    // (snappable grid placement and free placement) converge here; the null
    // guard above guarantees _selectedObject is set (plan Decision 7).
    GameEvents.RaiseBuildingPlaced(_selectedObject.Name);
  }

  /// <summary>
  ///   Deducts the material cost of <paramref name="selected"/> from the
  ///   player's inventory (FIX(iter4-plan): plan Decision 2). CostItemId == "" means free.
  /// </summary>
  private bool TryConsumeCost(BuildableResource selected)
  {
    if (string.IsNullOrEmpty(selected.CostItemId))
    {
      return true;
    }

    var inventory = Player?.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
    {
      GD.PushWarning(
        $"BuildingSystem: cannot pay for '{selected.Name}' — no player inventory found."
      );
      return false;
    }

    return BuildCost.TryConsume(inventory, selected);
  }

  #endregion Object Placing Logic

  #region Input Handlers

  private void HandleCancelEvent(InputEvent @event)
  {
    if (
      _selectedObject != null
      && @event is InputEventMouseButton mouseEvent
      && mouseEvent.ButtonIndex == MouseButton.Right
      && mouseEvent.Pressed
    )
    {
      _selectedObject = null;
      ClearMouseObject();
    }
  }

  private void HandleObjectDemolishEvent(InputEvent @event)
  {
    if (
      _isDemolishModeActive
      && _demolishCollider != null
      && @event is InputEventMouseButton mouseEvent
      && mouseEvent.ButtonIndex == MouseButton.Left
      && mouseEvent.Pressed
    )
    {
      // Demolition does NOT refund the material cost — intentional design
      // choice (plan Decision 3), matching upstream behavior.
      var mainNode = _demolishCollider.GetParent<BuildableInstance>();
      mainNode?.ClearObject();
    }
  }

  private void HandleObjectPlacementEvent(InputEvent @event)
  {
    if (_selectedObject == null || _isDemolishModeActive)
    {
      return;
    }

    if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
    {
      if (mouseEvent.Pressed)
      {
        if (DragBehavior == DragBehavior.None)
        {
          TryToPlaceObject();
        }
        else
        {
          _isMousePressed = true;
          _mousePressPosition = mouseEvent.Position;
          if (DragBehavior == DragBehavior.DelayedPlacement)
          {
            _mouseStartPosition = GetRaycastPoint(mouseEvent.Position);
          }
        }
      }
      else
      {
        // Mouse button released.
        if (_isMousePressed)
        {
          if (_mousePressPosition.DistanceTo(mouseEvent.Position) < DragThreshold)
          {
            // The mouse was not moved beyond the threshold, treat it as a
            // click.
            if (DragBehavior == DragBehavior.InstantPlacement)
            {
              TryToPlaceObject();
            }
          }

          if (DragBehavior == DragBehavior.DelayedPlacement)
          {
            CompleteDelayedDrag();
          }

          _isMousePressed = false;
        }
      }
    }
    else if (
      @event is InputEventMouseMotion mouseMotion
      && _isMousePressed
      && DragBehavior != DragBehavior.None
    )
    {
      if (_mousePressPosition.DistanceTo(mouseMotion.Position) >= DragThreshold)
      {
        // Mouse moved beyond the threshold, treat it as a drag.
        if (DragBehavior == DragBehavior.InstantPlacement)
        {
          TryToPlaceObject();
        }
        else
        {
          // Update end position and redraw.
          _mouseEndPosition = GetRaycastPoint(mouseMotion.Position);
          OnDragDelayed();
        }
      }
    }
  }

  private void HandleGridLevelChangeEvent(InputEvent @event)
  {
    if (
      !@event.IsActionPressed("grid_level_up")
      && !@event.IsActionPressed("grid_level_down")
    )
    {
      return;
    }

    // FIX(iter4-plan): Decision 14: with a single level there is nowhere to switch to; skip
    // entirely so the camera never teleports (upstream moved the camera to
    // the new grid's height even when the grid set never changed).
    if (_grids.Count <= 1)
    {
      return;
    }

    _activeGrid.SetActive(false);
    var modifier = @event.IsActionPressed("grid_level_up") ? 1 : -1;
    var nextSelectedGridIndex = (_grids.IndexOf(_activeGrid) + modifier) % _grids.Count;
    if (nextSelectedGridIndex < 0)
    {
      nextSelectedGridIndex = _grids.Count - 1;
    }

    _activeGrid = _grids[nextSelectedGridIndex];
    RefreshActiveGridVisual();

    // Move camera. Upstream checked for a CameraController parent that is
    // not ported; the camera node itself is moved instead.
    if (MainCamera != null)
    {
      var camera = MainCamera;
      var newCameraPosition = new Vector3(
        camera.GlobalPosition.X,
        _activeGrid.GlobalPosition.Y,
        camera.GlobalPosition.Z
      );
      camera.GlobalPosition = newCameraPosition;
    }

    GameEvents.RaiseBuildingLevelChanged(nextSelectedGridIndex);
  }

  private void HandleObjectRotationEvent(InputEvent @event)
  {
    if (@event.IsActionPressed("rotate_object") && _mouseObject.HasMouseObject())
    {
      // To avoid loss of precision due to floating-point error, rotation is
      // reset and remembered.
      _currentRotation = _currentRotation == 270 ? 0 : _currentRotation + 90;
      _mouseObject.Rotate(_currentRotation);
    }
  }

  #endregion Input Handlers

  #region Build mode

  /// <summary>
  ///   Toggles build mode. FIX(iter4-plan): Decision 6: the mouse cursor follows build mode
  ///   (Visible while building, Captured while playing). The W3 GameManager
  ///   pauses the game through ui_cancel and must call SetBuildMode(false)
  ///   first when build mode is active — pause takes priority over build
  ///   mode.
  /// </summary>
  public void SetBuildMode(bool value)
  {
    _isBuildModeActive = value;
    _objectMenu.Visible = _isBuildModeActive;

    // FIX(iter4-plan): Decision 6: mouse cursor.
    Input.MouseMode = _isBuildModeActive
      ? Input.MouseModeEnum.Visible
      : Input.MouseModeEnum.Captured;

    // FIX(iter4-plan): Decision 13: the grid visual follows build mode.
    RefreshActiveGridVisual();

    if (!_isBuildModeActive)
    {
      ClearMouseObject();
    }

    GameEvents.RaiseBuildModeChanged(_isBuildModeActive);
  }

  private void SetDemolitionMode(bool value)
  {
    _isDemolishModeActive = value;
    if (_isDemolishModeActive)
    {
      _activeLayerMask = _demolishLayerMask;
    }
    else
    {
      _activeLayerMask = GroundLayerMask;
      if (IsInstanceValid(_demolishCollider))
      {
        _demolishCollider?.GetParent<BuildableInstance>()?.SetDemolitionView(false);
      }

      _demolishCollider = null;
    }

    GameEvents.RaiseDemolitionModeChanged(_isDemolishModeActive);
  }

  #endregion Build mode

  #region Save system

  /// <summary>
  ///   FIX(iter8-plan): T8.1 — builds the building snapshot DTO WITHOUT
  ///   writing, so the unified SaveService can embed it in the full game save
  ///   (one file, no double-save). Grid-only, matching the previous F5
  ///   behavior (free objects are out of scope this iteration).
  /// </summary>
  public SaveFile BuildSaveSnapshot() =>
    BuildingSaveSystem.Snapshot(Grids.ToList(), []);

  /// <summary>
  ///   FIX(iter8-plan): T8.1 — restores building state from an already
  ///   deserialized <see cref="SaveFile"/> (embedded in the unified save).
  ///   Resets the current scene first so the load is a faithful snapshot,
  ///   then restores. Fails closed (returns false, touches nothing) when the
  ///   library is not wired.
  /// </summary>
  public bool RestoreFromSnapshot(SaveFile saveFile)
  {
    if (BuildableObjectLibrary == null)
    {
      GD.PushWarning(
        "BuildingSystem: RestoreFromSnapshot skipped — BuildableObjectLibrary not wired."
      );
      return false;
    }

    Reset();
    return BuildingSaveSystem.Restore(
      saveFile,
      BuildableObjectLibrary,
      Grids,
      _freeObjectsContainer,
      FreeLayerMask
    );
  }

  /// <summary>
  ///   Plan Decision 9 public API: loads the most recent building save
  ///   through the W2 BuildingSaveSystem and records the file it came from.
  ///   Returns false when there is no save, or when the library is not wired
  ///   (tests / unwired scenes must fail closed instead of crashing). The
  ///   free-object container is passed through but stays empty: free objects
  ///   are out of scope this iteration, so saves never contain them.
  /// </summary>
  public bool LoadMostRecent()
  {
    if (BuildableObjectLibrary == null)
    {
      GD.PushWarning(
        "BuildingSystem: LoadMostRecent skipped — BuildableObjectLibrary not wired."
      );
      return false;
    }

    var loaded = BuildingSaveSystem.LoadMostRecent(
      BuildableObjectLibrary,
      Grids,
      _freeObjectsContainer,
      FreeLayerMask,
      JsonSaveSystem.DEFAULT_FOLDER
    );

    if (loaded)
    {
      // The static helper reports success only; recover the file name for
      // the BuildingLoaded event from the (same, newest-first) listing.
      // Should the listing race empty, the event still fires with "".
      var saveFiles = JsonSaveSystem.GetSaveFilesInfo(JsonSaveSystem.DEFAULT_FOLDER);
      _currentSaveFile =
        saveFiles.Count > 0 ? ProjectSettings.LocalizePath(saveFiles[0].FullName) : "";
    }

    return loaded;
  }

  /// <summary>
  ///   Clears all placed objects (free objects and every grid). Public
  ///   because the W2 save system must reset the scene before loading a
  ///   save file (upstream Reset, kept as the pre-load hook).
  /// </summary>
  public void Reset()
  {
    foreach (var freeObject in _freeObjectsList)
    {
      freeObject.ClearObject();
    }

    _freeObjectsList = [];
    foreach (var grid in _grids)
    {
      grid.ResetGrid();
    }
  }

  #endregion Save system

  #region Utility methods

  private uint GetLayerMask(SnapBehaviour type)
  {
    return type switch
    {
      SnapBehaviour.Ground => GroundLayerMask,
      SnapBehaviour.Wall => WallLayerMask,
      SnapBehaviour.Free => FreeLayerMask,
      _ => GroundLayerMask
    };
  }

  private void OnDragDelayed()
  {
    // TODO: Complete implementation (upstream TODO).
    UpdateDragRectangle();
  }

  private static void CompleteDelayedDrag()
  {
    // TODO: Implementation (upstream TODO).
    GD.Print("Drag completed");
  }

  private void UpdateDragRectangle()
  {
    _dragRectangleMesh ??= new MeshInstance3D();
    if (_dragRectangleMesh.GetParent() == null)
    {
      AddChild(_dragRectangleMesh);
    }

    // Create a PlaneMesh for the rectangle.
    var planeMesh = new PlaneMesh
    {
      Size = new Vector2(
        (_mouseEndPosition - _mouseStartPosition).X,
        (_mouseEndPosition - _mouseStartPosition).Z
      ),
      CenterOffset = new Vector3(
        (_mouseEndPosition.X + _mouseStartPosition.X) / 2,
        ((_mouseEndPosition.Y + _mouseStartPosition.Y) / 2) + DragVisualGroundOffset,
        (_mouseEndPosition.Z + _mouseStartPosition.Z) / 2
      )
    };
    _dragRectangleMesh.Mesh = planeMesh;
  }

  private void UpdateMouseObjectVisual(double delta)
  {
    if (_isBuildModeActive && _mouseObject.HasMouseObject() && _selectedObject != null)
    {
      if (_selectedObject.SnapBehaviour != SnapBehaviour.Free)
      {
        // Snap to grid.
        _lastSnappedPosition = _activeGrid.GetMouseSnappedPosition(
          _currentMousePosition,
          _mouseObject
        );
        _mouseObject.GlobalPosition = _mouseObject.GlobalPosition.Lerp(
          _lastSnappedPosition,
          (float)(delta * 15)
        );
      }
      else
      {
        _mouseObject.GlobalPosition = new Vector3(
          _currentMousePosition.X,
          _activeGrid.GlobalPosition.Y,
          _currentMousePosition.Z
        );
      }
    }

    if (GroundMouseVisible)
    {
      _groundMouseNode.GlobalPosition = _currentMousePosition;
    }
  }

  private void ClearMouseObject()
  {
    _currentRotation = 0;
    _mouseObject.ClearMouseObject();
  }

  /// <summary>
  ///   FIX(iter4-plan): Decision 13 helper: the active grid's visual is visible only while
  ///   build mode is active.
  /// </summary>
  private void RefreshActiveGridVisual()
  {
    _activeGrid.SetActive(_isBuildModeActive);
  }

  /// <summary>
  ///   Gets the position of the mouse cursor in the 3D world (upstream
  ///   BSUtils.GetRaycastPoint, inlined because BSUtils is out of port
  ///   scope).
  /// </summary>
  private Vector3 GetRaycastPoint(Vector2 mousePosition)
  {
    if (MainCamera == null)
    {
      return Vector3.Zero;
    }

    var spaceState = GetWorld3D().DirectSpaceState;
    var origin = MainCamera.ProjectRayOrigin(mousePosition);
    var end = origin + (MainCamera.ProjectRayNormal(mousePosition) * 999f);
    var query = PhysicsRayQueryParameters3D.Create(origin, end, _activeLayerMask);
    var result = spaceState.IntersectRay(query);

    if (result.ContainsKey("position"))
    {
      return (Vector3)result["position"];
    }

    return Vector3.Zero;
  }

  /// <summary>
  ///   Animates the placement of a Node3D by scaling it down and then back to
  ///   its original scale (upstream AnimationUtils.AnimatePlacement, inlined
  ///   because that utility class is out of port scope).
  /// </summary>
  private void AnimatePlacement(Node3D node)
  {
    var originalScale = node.Scale;
    node.Scale *= 0.8f;
    var tween = CreateTween();
    tween.TweenProperty(node, "scale", originalScale, 0.1)
      .SetTrans(Tween.TransitionType.Bounce);
    tween.Play();
  }

  #endregion Utility methods

  #region Event Handlers

  private void OnObjectMenuInteract(int index, int button)
  {
    if (_isDemolishModeActive)
    {
      SetDemolitionMode(false);
    }

    if (BuildableObjectLibrary == null)
    {
      return;
    }

    _selectedObject = BuildableObjectLibrary.BuildableObjects[index];
    ClearMouseObject();
    _mouseObject.UpdateVisual(_selectedObject);
  }

  #endregion Event Handlers
}
