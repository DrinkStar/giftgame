// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure <see cref="CombatLogic"/> functions (Iter6 plan
///   Decision 8): cooldown gate, ballistic positions, weapon dispatch,
///   charge/night speed multipliers and the attack trigger.
/// </summary>
public class CombatLogicTest : TestClass
{
  public CombatLogicTest(Node testScene) : base(testScene) { }

  [Test]
  public void IsReady_BelowCooldown_ReturnsFalse()
  {
    CombatLogic.IsReady(0.49f, 0.5f).ShouldBeFalse();
    CombatLogic.IsReady(0f, 0.5f).ShouldBeFalse();
  }

  [Test]
  public void IsReady_AtOrPastCooldown_ReturnsTrue()
  {
    CombatLogic.IsReady(0.5f, 0.5f).ShouldBeTrue();
    CombatLogic.IsReady(1.2f, 0.5f).ShouldBeTrue();
  }

  [Test]
  public void ProjectilePosition_WithoutGravity_MovesLinearly()
  {
    var start = new Vector3(1f, 2f, 3f);
    var velocity = new Vector3(4f, 5f, 6f);

    CombatLogic.ProjectilePosition(start, velocity, 0f, 2f)
      .ShouldBe(new Vector3(9f, 12f, 15f));
  }

  [Test]
  public void ProjectilePosition_WithGravity_FollowsParabola()
  {
    var start = Vector3.Zero;
    var velocity = new Vector3(10f, 8f, 0f);

    // y = vy*t + 0.5*g*t^2
    CombatLogic.ProjectilePosition(start, velocity, -9.8f, 1f)
      .ShouldBe(new Vector3(10f, 3.1f, 0f));

    // Apex at t = -vy/g.
    var apex = CombatLogic.ProjectilePosition(start, velocity, -9.8f, 8f / 9.8f);
    apex.Y.ShouldBeGreaterThan(3.1f);
  }

  [Test]
  public void ProjectilePosition_AtZeroTime_ReturnsStart()
  {
    var start = new Vector3(5f, 6f, 7f);
    CombatLogic.ProjectilePosition(start, new Vector3(1f, 2f, 3f), 9.8f, 0f)
      .ShouldBe(start);
  }

  [Test]
  public void ResolveAttack_NonTool_ReturnsNone()
  {
    // Even a spear-shaped id never attacks when the item is not a tool.
    CombatLogic.ResolveAttack("wooden_spear", true, true, false).ShouldBe(AttackType.None);
    CombatLogic.ResolveAttack("wood", false, false, false).ShouldBe(AttackType.None);
  }

  [Test]
  public void ResolveAttack_Spear_WithAmmo_ReturnsThrow()
  {
    CombatLogic.ResolveAttack("wooden_spear", true, false, true).ShouldBe(AttackType.Throw);
  }

  [Test]
  public void ResolveAttack_Spear_WithoutAmmo_FallsBackToMelee()
  {
    CombatLogic.ResolveAttack("wooden_spear", false, false, true).ShouldBe(AttackType.Melee);
  }

  [Test]
  public void ResolveAttack_Bow_WithArrows_ReturnsShoot()
  {
    CombatLogic.ResolveAttack("wooden_bow", false, true, true).ShouldBe(AttackType.Shoot);
  }

  [Test]
  public void ResolveAttack_Bow_WithoutArrows_FallsBackToMelee()
  {
    CombatLogic.ResolveAttack("wooden_bow", false, false, true).ShouldBe(AttackType.Melee);
  }

  [Test]
  public void ResolveAttack_AxeAndOtherTools_ReturnMelee()
  {
    // "stone_axe" melees on purpose (Decision 1).
    CombatLogic.ResolveAttack("stone_axe", false, false, true).ShouldBe(AttackType.Melee);
    CombatLogic.ResolveAttack("torch", false, false, true).ShouldBe(AttackType.Melee);
  }

  [Test]
  public void ResolveAttack_MatchIsCaseSensitive()
  {
    // "Spear" (capital S) matches none of the lowercase substrings -> melee.
    CombatLogic.ResolveAttack("Wooden_Spear", true, true, true).ShouldBe(AttackType.Melee);
  }

  [Test]
  public void ChargeSpeedMultiplier_InRangeAndReady_ReturnsThree()
  {
    CombatLogic.ChargeSpeedMultiplier(6f, 6f, true).ShouldBe(3f);
    CombatLogic.ChargeSpeedMultiplier(0.5f, 6f, true).ShouldBe(3f);
  }

  [Test]
  public void ChargeSpeedMultiplier_OutOfRangeOrNotReady_ReturnsOne()
  {
    CombatLogic.ChargeSpeedMultiplier(6.1f, 6f, true).ShouldBe(1f);
    CombatLogic.ChargeSpeedMultiplier(1f, 6f, false).ShouldBe(1f);
  }

  [Test]
  public void NightSpeedMultiplier_WolfAtNight_ReturnsOneAndAHalf()
  {
    CombatLogic.NightSpeedMultiplier("wolf", true).ShouldBe(1.5f);
  }

  [Test]
  public void NightSpeedMultiplier_WolfAtDayOrOtherEnemy_ReturnsOne()
  {
    CombatLogic.NightSpeedMultiplier("wolf", false).ShouldBe(1f);
    CombatLogic.NightSpeedMultiplier("boar", true).ShouldBe(1f);
    CombatLogic.NightSpeedMultiplier("crab", true).ShouldBe(1f);
  }

  [Test]
  public void ShouldAttack_InRangeAndReady_ReturnsTrue()
  {
    CombatLogic.ShouldAttack(1.5f, 1.5f, true).ShouldBeTrue();
    CombatLogic.ShouldAttack(0.1f, 1.5f, true).ShouldBeTrue();
  }

  [Test]
  public void ShouldAttack_OutOfRangeOrNotReady_ReturnsFalse()
  {
    CombatLogic.ShouldAttack(1.6f, 1.5f, true).ShouldBeFalse();
    CombatLogic.ShouldAttack(0.5f, 1.5f, false).ShouldBeFalse();
  }
}
