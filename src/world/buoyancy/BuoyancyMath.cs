// Ported from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure static buoyancy math (Archimedes' principle + per-cell gravity),
///   ported from ManickYoj's buoyancy fork. Contains no Godot node
///   dependencies — only GodotSharp math structs — so it can be unit-tested
///   without a scene tree.
/// </summary>
public static class BuoyancyMath
{
  /// <summary>Water density in kg/m³ ("Thanks, science" — buoyant_cell.gd:14).</summary>
  public const float FluidDensityKgPerM3 = 1000f;

  /// <summary>
  ///   Fraction of a cell's height that is submerged, given the depth of the
  ///   cell's center below the water surface. Depth &gt; 0 means below water
  ///   (buoyant_cell.gd:82).
  /// </summary>
  public static float SubmergedFraction(float depth, float cellHeight) =>
    Mathf.Clamp((depth + 0.5f * cellHeight) / cellHeight, 0f, 1f);

  /// <summary>Mass of displaced fluid in kg (buoyant_cell.gd:85).</summary>
  public static float DisplacedMass(
    float fluidDensity, float volume, float fraction
  ) => fluidDensity * volume * fraction;

  /// <summary>
  ///   Buoyant force: displaced mass pushed opposite to gravity
  ///   (buoyant_cell.gd:86).
  /// </summary>
  public static Vector3 BuoyancyForce(float displacedMass, Vector3 gravity) =>
    displacedMass * -gravity;

  /// <summary>Weight of the cell itself under gravity (buoyant_cell.gd:89).</summary>
  public static Vector3 CellGravity(
    float cellDensity, float volume, Vector3 gravity
  ) => cellDensity * volume * gravity;

  /// <summary>
  ///   Net force on a cell = buoyancy + cell gravity (buoyant_cell.gd:92).
  ///   <paramref name="cellMass"/> is the cell's own mass in kg.
  /// </summary>
  public static Vector3 NetForce(
    float displacedMass, float cellMass, Vector3 gravity
  ) => BuoyancyForce(displacedMass, gravity) + cellMass * gravity;
}
