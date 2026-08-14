// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Collections.Generic;

/// <summary>Represents a saved buildable object (FIX(iter4-plan): plan Decision 5 fix ⑤).</summary>
/// <remarks>
///   The rotation field is named <c>RotationDegreesY</c> and stores DEGREES.
///   Upstream stored a degrees value inside a field named "YRotationRadiants",
///   which broke every consumer that assumed radians. The fix applies
///   consistently: Save writes degrees, Load feeds degrees straight into
///   <see cref="BuildingSystemGrid.TryToPlaceObject"/> and converts to
///   radians exactly once for free objects.
/// </remarks>
public class SaveObject
{
  /// <summary>Gets or sets the name of the buildable resource.</summary>
  public string Name { get; set; } = "";

  /// <summary>Gets or sets the resource path of the buildable resource.</summary>
  public string ResourcePath { get; set; } = "";

  /// <summary>Gets or sets the Y rotation of the object in degrees.</summary>
  public float RotationDegreesY { get; set; }
}

/// <summary>Represents a saved object placed on a grid.</summary>
/// <remarks>
///   The grid position is stored as two float fields instead of a nested
///   class so System.Text.Json emits a flat, readable document. X/Z are the
///   object's global snapped position; Y is not saved because it is derived
///   from the grid's global height on load (plan Decision 4).
/// </remarks>
public class SaveGridObject : SaveObject
{
  /// <summary>Gets or sets the global X coordinate of the object.</summary>
  public float PositionX { get; set; }

  /// <summary>Gets or sets the global Z coordinate of the object.</summary>
  public float PositionZ { get; set; }

  /// <summary>
  ///   R2 (Iter8p): durability left on load, -1 = invincible/unknown. The
  ///   property initializer keeps legacy saves working: deserialization runs
  ///   the default constructor, so files without this key come back as -1.
  ///   Reserved only — no damage logic this iteration.
  /// </summary>
  public int DurabilityRemaining { get; set; } = -1;
}

/// <summary>Represents a saved object placed freely in the scene.</summary>
/// <remarks>
///   The position is stored as three float fields (flat JSON). It is the
///   object's LOCAL position relative to the free object container; Load
///   assigns it as the global position (upstream contract, container at
///   world origin).
/// </remarks>
public class SaveFreeObject : SaveObject
{
  /// <summary>Gets or sets the local X coordinate of the object.</summary>
  public float PositionX { get; set; }

  /// <summary>Gets or sets the local Y coordinate of the object.</summary>
  public float PositionY { get; set; }

  /// <summary>Gets or sets the local Z coordinate of the object.</summary>
  public float PositionZ { get; set; }
}

/// <summary>Represents a saved grid with all of its placed objects.</summary>
public class SaveGrid
{
  /// <summary>
  ///   Gets or sets the index of the grid inside the BuildingSystem's grid
  ///   list (FIX(iter4-plan): plan Decision 5 fix ④). Load uses this value to locate the
  ///   target grid instead of assuming list order.
  /// </summary>
  public int Index { get; set; }

  /// <summary>Gets or sets the list of objects placed on the grid.</summary>
  public List<SaveGridObject> Objects { get; set; } = [];
}

/// <summary>Represents a complete building save file.</summary>
public class SaveFile
{
  /// <summary>
  ///   Gets or sets the list of saved grids. FIX(iter4-plan): Plan Decision 5 fix ④: EVERY
  ///   grid is written with its index, including empty ones, so the file is
  ///   a faithful snapshot of the grid stack.
  /// </summary>
  public List<SaveGrid> Grids { get; set; } = [];

  /// <summary>Gets or sets the list of saved free objects.</summary>
  public List<SaveFreeObject> FreeObjects { get; set; } = [];
}
