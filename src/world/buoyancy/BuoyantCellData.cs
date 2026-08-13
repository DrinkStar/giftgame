// Ported from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Immutable description of one buoyancy cell: its position relative to the
///   body origin, its box size, and its material density. Volume and mass are
///   derived (buoyant_cell.gd:57-60).
/// </summary>
public record BuoyantCellData(
  Vector3 LocalPosition,
  Vector3 Size,
  float CellDensityKgPerM3
)
{
  /// <summary>Box volume in m³ (size.x * size.y * size.z).</summary>
  public float Volume => Size.X * Size.Y * Size.Z;

  /// <summary>Cell mass in kg (density × volume).</summary>
  public float Mass() => CellDensityKgPerM3 * Volume;
}
