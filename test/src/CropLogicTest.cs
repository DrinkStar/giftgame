// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Locks the pure <see cref="CropLogic"/> math (Iter5 plan Decision 1):
///   progress clamping, readiness boundary, inclusive yield range across many
///   rolls, the fixed seed return, seeded-rng determinism and inverted-range
///   normalization. All math is static and needs no scene tree.
/// </summary>
public class CropLogicTest : TestClass
{
  private const int Rolls = 1000;

  public CropLogicTest(Node testScene)
    : base(testScene) { }

  [Test]
  public void ProgressClampsToUnitRange()
  {
    CropLogic.Progress(-5f, 60f).ShouldBe(0f);
    CropLogic.Progress(0f, 60f).ShouldBe(0f);
    CropLogic.Progress(30f, 60f).ShouldBe(0.5f);
    CropLogic.Progress(60f, 60f).ShouldBe(1f);
    CropLogic.Progress(999f, 60f).ShouldBe(1f);
  }

  [Test]
  public void IsReadyOnlyAtOrPastFullGrowth()
  {
    CropLogic.IsReady(0f, 60f).ShouldBeFalse();
    CropLogic.IsReady(59.9f, 60f).ShouldBeFalse();
    CropLogic.IsReady(60f, 60f).ShouldBeTrue();
    CropLogic.IsReady(60.1f, 60f).ShouldBeTrue();
  }

  [Test]
  public void HarvestYieldStaysWithinInclusiveRangeAcrossManyRolls()
  {
    var rng = new Random(12345);
    for (var i = 0; i < Rolls; i++)
    {
      var yieldCount = CropLogic.HarvestYield(2, 4, rng);
      yieldCount.ShouldBeInRange(2, 4);
    }
  }

  [Test]
  public void SeedReturnIsExactlyOne()
  {
    CropLogic.SeedReturn.ShouldBe(1);
  }

  [Test]
  public void HarvestYieldRespectsMinAndMaxWithSeededRng()
  {
    // A seeded rng makes the roll sequence deterministic: after 1000 rolls
    // both bounds of [2, 4] must have occurred and nothing outside them.
    var rng = new Random(2025);
    var sawMin = false;
    var sawMax = false;

    for (var i = 0; i < Rolls; i++)
    {
      var yieldCount = CropLogic.HarvestYield(2, 4, rng);
      yieldCount.ShouldBeInRange(2, 4);
      sawMin |= yieldCount == 2;
      sawMax |= yieldCount == 4;
    }

    sawMin.ShouldBeTrue();
    sawMax.ShouldBeTrue();
  }

  [Test]
  public void HarvestYieldNormalizesInvertedRange()
  {
    var rng = new Random(7);
    for (var i = 0; i < Rolls; i++)
    {
      CropLogic.HarvestYield(6, 3, rng).ShouldBeInRange(3, 6);
    }
  }
}
