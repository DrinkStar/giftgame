// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

/// <summary>Contains constants used in the building system.</summary>
public static class BSConstants
{
  /// <summary>The default size of a cell.</summary>
  public const int DEFAULT_CELL_SIZE = 1;

  /// <summary>The default height of a cell.</summary>
  public const int DEFAULT_CELL_HEIGHT = 2;

  /// <summary>
  ///   The default ground layer mask (FIX(iter4-plan): plan Decision 3): layer 1 = "World".
  ///   The building raycast shoots at the existing ground of Game.tscn; the
  ///   player capsule also lives on layers 1/2, so the build ray can
  ///   occasionally hit the player — accepted this iteration.
  /// </summary>
  public const uint DEFAULT_GROUND_LAYER_MASK = 1u;

  /// <summary>
  ///   The default floor layer mask (FIX(iter4-plan): plan Decision 3): layer 5 = "Buildings".
  ///   Placed buildings' colliders live here; demolition raycasts and mouse
  ///   tile collision feedback target this layer. The player does NOT collide
  ///   with buildings this iteration (walking through them is accepted).
  /// </summary>
  public const uint DEFAULT_FLOOR_LAYER_MASK = 1u << 4;

  /// <summary>
  ///   The default wall layer mask (FIX(iter4-plan): plan Decision 3): layer 6 =
  ///   "BuildingWalls". No wall objects exist this iteration; the constant is
  ///   kept so the mask contract stays stable for the future wall port.
  /// </summary>
  public const uint DEFAULT_WALL_LAYER_MASK = 1u << 5;

  /// <summary>
  ///   The default free layer mask (FIX(iter4-plan): plan Decision 3): layer 7 =
  ///   "FreeObjects". Unused this iteration (no free object use case).
  /// </summary>
  public const uint DEFAULT_FREE_LAYER_MASK = 1u << 6;

  /// <summary>The default X size of the grid.</summary>
  public const int DEFAULT_GRID_X_SIZE = 200;

  /// <summary>The default Z size of the grid.</summary>
  public const int DEFAULT_GRID_Z_SIZE = 200;

  /// <summary>The default number of levels in the grid.</summary>
  public const int DEFAULT_GRID_LEVELS = 10;

  /// <summary>The default drag threshold.</summary>
  public const float DEFAULT_DRAG_THRESHOLD = 10f;

  /// <summary>The default drag visual ground offset.</summary>
  public const float DEFAULT_DRAG_VISUAL_GROUND_OFFSET = 0.2f;
}
