// Ported from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   RigidBody3D that floats on a heightfield ocean using buoyancy cells,
///   ported from ManickYoj's buoyancy fork. Each cell applies an approximated
///   buoyancy + gravity force at its center (buoyant_cell.gd:73-131); linear
///   and angular hydrodynamic drag resist motion (mass_calculation.gd:48-143).
///   Wave height sampling is injected as a delegate so this class has no
///   dependency on WaterMesh. The body must run with gravity_scale = 0 —
///   gravity is applied per-cell via <see cref="BuoyancyMath.CellGravity"/>.
/// </summary>
public partial class FloatingBody : RigidBody3D
{
  /// <summary>
  ///   Buoyancy cells attached to this body (positions relative to the body
  ///   origin). Injected at runtime by the ocean test scene. Deliberately NOT
  ///   [Export]ed: <see cref="BuoyantCellData"/> is a plain C# record and is
  ///   not Variant-compatible (GD0102), and the cells are never serialized
  ///   in the scene anyway.
  /// </summary>
  public BuoyantCellData[] Cells { get; set; } = Array.Empty<BuoyantCellData>();

  /// <summary>
  ///   Overall body size (meters): the source of drag areas and moment-arm
  ///   lengths (mass_calculation.gd:5-11 uses the mesh size).
  /// </summary>
  [Export]
  public Vector3 BodySize { get; set; }

  /// <summary>Drag coefficient along the local X axis (mass_calculation.gd:5).</summary>
  [Export]
  public float DragCoefAxial { get; set; } = 0.15f;

  /// <summary>Drag coefficient along the local Z axis (mass_calculation.gd:6).</summary>
  [Export]
  public float DragCoefLateral { get; set; } = 1f;

  /// <summary>Drag coefficient along the local Y axis (mass_calculation.gd:7).</summary>
  [Export]
  public float DragCoefVertical { get; set; } = 1f;

  /// <summary>Angular drag coefficient about the local Y axis (mass_calculation.gd:8).</summary>
  [Export]
  public float DragCoefYaw { get; set; } = 100f;

  /// <summary>Angular drag coefficient about the local Z axis (mass_calculation.gd:9).</summary>
  [Export]
  public float DragCoefPitch { get; set; } = 100f;

  /// <summary>Angular drag coefficient about the local X axis (mass_calculation.gd:10).</summary>
  [Export]
  public float DragCoefRoll { get; set; } = 100f;

  /// <summary>
  ///   Wave height sampler (world position → surface height Y). Injected at
  ///   runtime (e.g. waterMesh.GetWaveHeight); keeps this class decoupled
  ///   from the ocean renderer.
  /// </summary>
  public Func<Vector3, float>? WaveHeightProvider { get; set; }

  public override void _PhysicsProcess(double delta)
  {
    if (WaveHeightProvider is null || Cells.Length == 0)
    {
      return;
    }

    var gravity = GetGravity();
    var totalMass = 0f;

    foreach (var cell in Cells)
    {
      var worldPosition = GlobalTransform * cell.LocalPosition;
      var depth = WaveHeightProvider(worldPosition) - worldPosition.Y;
      var submergedFraction = BuoyancyMath.SubmergedFraction(
        depth, cell.Size.Y
      );
      var displacedMass = BuoyancyMath.DisplacedMass(
        BuoyancyMath.FluidDensityKgPerM3, cell.Volume, submergedFraction
      );

      // Per-cell gravity must be ON: this body runs with gravity_scale = 0,
      // so the cell's weight is part of the net force
      // (buoyant_cell.gd:88-92).
      var cellMass = cell.Mass();
      totalMass += cellMass;
      var netForce = BuoyancyMath.NetForce(displacedMass, cellMass, gravity);

      // Force offset: the local offset rotated into global axes
      // (buoyant_cell.gd:94-96, 130).
      ApplyForce(netForce, GlobalBasis * cell.LocalPosition);
    }

    Mass = totalMass;
    ApplyDrag();
  }

  /// <summary>Linear + angular hydrodynamic drag (mass_calculation.gd:36-46).</summary>
  private void ApplyDrag()
  {
    // Linear drag along local axes (mass_calculation.gd:102-128).
    ApplyLinearDrag(GlobalBasis.X, BodySize.Y * BodySize.Z, DragCoefAxial);
    ApplyLinearDrag(GlobalBasis.Z, BodySize.Y * BodySize.X, DragCoefLateral);
    ApplyLinearDrag(GlobalBasis.Y, BodySize.X * BodySize.Z, DragCoefVertical);

    // Angular drag torques (mass_calculation.gd:48-100).
    ApplyAngularDrag(
      GlobalBasis.Y, BodySize.X, BodySize.Y * BodySize.X, DragCoefYaw
    );
    ApplyAngularDrag(
      GlobalBasis.X, BodySize.Z, BodySize.Z * BodySize.X, DragCoefRoll
    );
    ApplyAngularDrag(
      GlobalBasis.Z, BodySize.X, BodySize.X * BodySize.Z, DragCoefPitch
    );
  }

  /// <summary>
  ///   Quadratic drag along one local axis (mass_calculation.gd:138-143).
  ///   Drag always opposes motion.
  /// </summary>
  private void ApplyLinearDrag(Vector3 axis, float area, float coefficient)
  {
    var velocity = LinearVelocity.Dot(axis);
    var magnitude = 0.5f * BuoyancyMath.FluidDensityKgPerM3
      * velocity * velocity * area * coefficient;
    var force = axis * (velocity > 0f ? -magnitude : magnitude);
    ApplyCentralForce(force);
  }

  /// <summary>
  ///   Quadratic angular drag about one local axis
  ///   (mass_calculation.gd:130-136 — the 0.25 is because the average moment
  ///   arm is half the length of the half of the body).
  /// </summary>
  private void ApplyAngularDrag(
    Vector3 axis, float length, float area, float coefficient
  )
  {
    var angularVelocity = AngularVelocity.Dot(axis);
    var magnitude = 0.5f * BuoyancyMath.FluidDensityKgPerM3
      * angularVelocity * angularVelocity * area * coefficient * length * 0.25f;
    var torque = axis * (angularVelocity > 0f ? -magnitude : magnitude);
    ApplyTorque(torque);
  }
}
