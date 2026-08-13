// Ported from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure-C# <see cref="BuoyancyMath"/> — Archimedes'
///   principle math ported from ManickYoj's buoyancy fork. Written
///   tests-after style, like <see cref="PlayerMotionTest"/>.
/// </summary>
public class BuoyancyMathTest : TestClass
{
  public BuoyancyMathTest(Node testScene) : base(testScene) { }

  private const double Tolerance = 0.0001;
  private static readonly Vector3 Gravity = new(0f, -9.8f, 0f);

  [Test]
  public void FullySubmergedCellHasFractionOne()
  {
    BuoyancyMath.SubmergedFraction(depth: 1f, cellHeight: 1f)
      .ShouldBe(1f, Tolerance);
  }

  [Test]
  public void FullyAboveWaterCellHasFractionZero()
  {
    BuoyancyMath.SubmergedFraction(depth: -1f, cellHeight: 1f)
      .ShouldBe(0f, Tolerance);
  }

  [Test]
  public void HalfSubmergedCellHasFractionHalf()
  {
    BuoyancyMath.SubmergedFraction(depth: 0f, cellHeight: 1f)
      .ShouldBe(0.5f, Tolerance);
  }

  [Test]
  public void DisplacedMassIsDensityTimesSubmergedVolume()
  {
    BuoyancyMath.DisplacedMass(
      fluidDensity: 1000f, volume: 2f, fraction: 0.5f
    ).ShouldBe(1000f, Tolerance);
  }

  [Test]
  public void HalfSubmergedHalfDensityCellIsInEquilibrium()
  {
    // 1×1×1 m cell at ρ = 500 kg/m³, half submerged in water (ρ = 1000):
    // displaced mass = 1000 * 1 * 0.5 = 500 kg == cell mass → NetForce.Y ≈ 0.
    const float volume = 1f;
    var cellMass = 500f * volume;
    var displacedMass = BuoyancyMath.DisplacedMass(
      BuoyancyMath.FluidDensityKgPerM3, volume, fraction: 0.5f
    );
    displacedMass.ShouldBe(cellMass, Tolerance);

    var netForce = BuoyancyMath.NetForce(displacedMass, cellMass, Gravity);
    netForce.Y.ShouldBe(0f, Tolerance);
  }

  [Test]
  public void WaterDensityCellIsNeutrallyBuoyantWhenFullySubmerged()
  {
    // ρ_cell == ρ_fluid == 1000 kg/m³, fully submerged: mass == displaced
    // mass, so the net force vanishes at any depth (density balance point).
    const float volume = 2f;
    var cellMass = BuoyancyMath.FluidDensityKgPerM3 * volume;
    var displacedMass = BuoyancyMath.DisplacedMass(
      BuoyancyMath.FluidDensityKgPerM3, volume, fraction: 1f
    );

    var netForce = BuoyancyMath.NetForce(displacedMass, cellMass, Gravity);
    netForce.Y.ShouldBe(0f, Tolerance);
    netForce.X.ShouldBe(0f, Tolerance);
    netForce.Z.ShouldBe(0f, Tolerance);
  }

  [Test]
  public void BuoyancyForceOpposesGravity()
  {
    var force = BuoyancyMath.BuoyancyForce(displacedMass: 500f, Gravity);

    force.Y.ShouldBe(-500f * Gravity.Y, Tolerance);
    force.Y.ShouldBeGreaterThan(0f);
  }

  [Test]
  public void CellGravityFollowsGravity()
  {
    var force = BuoyancyMath.CellGravity(
      cellDensity: 500f, volume: 1f, Gravity
    );

    force.Y.ShouldBe(500f * Gravity.Y, Tolerance);
    force.Y.ShouldBeLessThan(0f);
  }
}
