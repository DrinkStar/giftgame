// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   <see cref="CombatFeedback"/> dispatch seam: melee raises only
///   <see cref="GameEvents.AttackSwing"/>, throw/shoot raise only
///   <see cref="GameEvents.RangedAttack"/>. Handlers are always removed
///   (try/finally) to keep the static bus clean between tests.
/// </summary>
public class CombatFeedbackTest : TestClass
{
  public CombatFeedbackTest(Node testScene) : base(testScene) { }

  [Test]
  public void RaiseAttack_Melee_RaisesOnlyAttackSwing()
  {
    var swings = new List<string>();
    var ranged = new List<string>();
    Action<string> swing = id => swings.Add(id);
    Action<string> shoot = id => ranged.Add(id);
    GameEvents.AttackSwing += swing;
    GameEvents.RangedAttack += shoot;
    try
    {
      CombatFeedback.RaiseAttack(AttackType.Melee, "wooden_axe");
    }
    finally
    {
      GameEvents.AttackSwing -= swing;
      GameEvents.RangedAttack -= shoot;
    }

    swings.ShouldBe(new[] { "wooden_axe" });
    ranged.ShouldBeEmpty();
  }

  [Test]
  public void RaiseAttack_ThrowAndShoot_RaiseOnlyRangedAttack()
  {
    var swings = 0;
    var ranged = new List<string>();
    Action<string> swing = _ => swings++;
    Action<string> shoot = id => ranged.Add(id);
    GameEvents.AttackSwing += swing;
    GameEvents.RangedAttack += shoot;
    try
    {
      CombatFeedback.RaiseAttack(AttackType.Throw, "wooden_spear");
      CombatFeedback.RaiseAttack(AttackType.Shoot, "wooden_bow");
    }
    finally
    {
      GameEvents.AttackSwing -= swing;
      GameEvents.RangedAttack -= shoot;
    }

    swings.ShouldBe(0);
    ranged.ShouldBe(new[] { "wooden_spear", "wooden_bow" });
  }
}
