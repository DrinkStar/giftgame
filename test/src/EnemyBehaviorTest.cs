// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the Iter6.1 dedicated enemy behaviors (plan todo 3):
///   the pure <see cref="CombatLogic"/> additions (flyer vertical seek,
///   webbing slow decay, sea-beast aggro, mutant rage + AoE), the extended
///   <see cref="EnemyBehavior"/> enum, and the four .tres assets that ship
///   the new behavior values.
/// </summary>
public class EnemyBehaviorTest : TestClass
{
  public EnemyBehaviorTest(Node testScene) : base(testScene) { }

  [Test]
  public void FlyerVerticalSeek_TargetAbove_ReturnsPositiveCappedVelocity()
  {
    // dy > 0 and far away: full positive speed toward the target Y.
    CombatLogic.FlyerVerticalSeek(10f, 4f, 1f).ShouldBe(4f);
    CombatLogic.FlyerVerticalSeek(100f, 4f, 0.1f).ShouldBe(4f);
  }

  [Test]
  public void FlyerVerticalSeek_TargetBelow_ReturnsNegativeCappedVelocity()
  {
    CombatLogic.FlyerVerticalSeek(-10f, 4f, 1f).ShouldBe(-4f);
    CombatLogic.FlyerVerticalSeek(-100f, 4f, 0.1f).ShouldBe(-4f);
  }

  [Test]
  public void FlyerVerticalSeek_SmallGap_ReturnsConvergingVelocity()
  {
    // Close to the target Y the seek slows down below the cap instead of
    // overshooting, so the flyer converges onto the player's altitude.
    var small = CombatLogic.FlyerVerticalSeek(0.4f, 4f, 0.5f);
    small.ShouldBe(0.8f);
    small.ShouldBeLessThan(4f);
    Mathf.Abs(small).ShouldBeLessThan(4f);

    CombatLogic.FlyerVerticalSeek(0f, 4f, 0.5f).ShouldBe(0f);
  }

  [Test]
  public void FlyerVerticalSeek_NonPositiveDt_ReturnsZero()
  {
    CombatLogic.FlyerVerticalSeek(10f, 4f, 0f).ShouldBe(0f);
    CombatLogic.FlyerVerticalSeek(10f, 4f, -0.1f).ShouldBe(0f);
  }

  [Test]
  public void SlowFactor_WhileTimerActive_ReturnsFactor()
  {
    // 2 s slow at 0.5×: still active after a 1 s tick.
    CombatLogic.SlowFactor(0.5f, 2f, 1f).ShouldBe(0.5f);
    CombatLogic.SlowFactor(0.5f, 0.1f, 0.05f).ShouldBe(0.5f);
  }

  [Test]
  public void SlowFactor_WhenTimerExpires_ReturnsOne()
  {
    // The timer runs out during the tick (remaining <= dt): slow decays
    // back to full speed.
    CombatLogic.SlowFactor(0.5f, 1f, 1f).ShouldBe(1f);
    CombatLogic.SlowFactor(0.5f, 0.9f, 1f).ShouldBe(1f);
    CombatLogic.SlowFactor(0.5f, 0f, 0f).ShouldBe(1f);
  }

  [Test]
  public void SeaBeastAggro_PlayerOnFloat_ReturnsTrue()
  {
    CombatLogic.SeaBeastAggro(playerOnFloat: true, defaultAggro: false).ShouldBeTrue();
  }

  [Test]
  public void SeaBeastAggro_PlayerOffFloatWithoutDefault_ReturnsFalse()
  {
    // Wired float target and the player is away from it: no pursuit.
    CombatLogic.SeaBeastAggro(playerOnFloat: false, defaultAggro: false).ShouldBeFalse();
  }

  [Test]
  public void SeaBeastAggro_NoFloatWired_DefaultAggro_ReturnsTrue()
  {
    // No float wired = treated as permanently at sea: always pursue.
    CombatLogic.SeaBeastAggro(playerOnFloat: false, defaultAggro: true).ShouldBeTrue();
  }

  [Test]
  public void MutantRage_BelowHalfHealth_ReturnsBoostedMultipliers()
  {
    var rage = CombatLogic.MutantRage(0.49f);
    rage.SpeedMultiplier.ShouldBe(1.5f);
    rage.RangeMultiplier.ShouldBe(1.5f);

    CombatLogic.MutantRage(0f).SpeedMultiplier.ShouldBe(1.5f);
  }

  [Test]
  public void MutantRage_AtOrAboveHalfHealth_ReturnsBaseMultipliers()
  {
    var calm = CombatLogic.MutantRage(0.5f);
    calm.SpeedMultiplier.ShouldBe(1f);
    calm.RangeMultiplier.ShouldBe(1f);

    CombatLogic.MutantRage(1f).SpeedMultiplier.ShouldBe(1f);
    CombatLogic.MutantRage(0.8f).RangeMultiplier.ShouldBe(1f);
  }

  [Test]
  public void MutantAoeRadius_ScalesByRageRangeMultiplier()
  {
    // Enraged (< 0.5 hp): the slam radius widens by ×1.5.
    CombatLogic.MutantAoeRadius(2f, 0.3f).ShouldBe(3f);
    // Calm: radius equals the base attack range.
    CombatLogic.MutantAoeRadius(2f, 0.8f).ShouldBe(2f);
  }

  [Test]
  public void EnemyBehaviorEnum_NewValuesFollowExistingNumbers()
  {
    ((int)EnemyBehavior.Flyer).ShouldBe(3);
    ((int)EnemyBehavior.Webbing).ShouldBe(4);
    ((int)EnemyBehavior.SeaBeast).ShouldBe(5);
    ((int)EnemyBehavior.Mutant).ShouldBe(6);

    // Existing values stay untouched (serialized in .tres files).
    ((int)EnemyBehavior.MeleeChase).ShouldBe(0);
    ((int)EnemyBehavior.Charge).ShouldBe(1);
    ((int)EnemyBehavior.Swimmer).ShouldBe(2);
  }

  [Test]
  public void PlaceholderEnemyResources_HaveDedicatedBehaviorValues()
  {
    GD.Load<EnemyData>("res://assets/enemies/spider.tres")!
      .Behavior.ShouldBe(EnemyBehavior.Webbing);
    GD.Load<EnemyData>("res://assets/enemies/bat.tres")!
      .Behavior.ShouldBe(EnemyBehavior.Flyer);
    GD.Load<EnemyData>("res://assets/enemies/storm_beast.tres")!
      .Behavior.ShouldBe(EnemyBehavior.SeaBeast);
    GD.Load<EnemyData>("res://assets/enemies/mutant.tres")!
      .Behavior.ShouldBe(EnemyBehavior.Mutant);
  }
}
