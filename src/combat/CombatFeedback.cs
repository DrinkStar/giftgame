// Original — no upstream port
namespace SeaAnomaly;

/// <summary>
///   Pure static dispatch seam between the resolved <see cref="AttackType"/>
///   and the combat feedback events. Melee raises
///   <see cref="GameEvents.AttackSwing"/>; Throw/Shoot raise
///   <see cref="GameEvents.RangedAttack"/>, so each event has one meaning
///   (melee swing audio vs bow shot / spear whoosh).
/// </summary>
public static class CombatFeedback
{
  public static void RaiseAttack(AttackType attack, string itemId)
  {
    if (attack == AttackType.Melee)
      GameEvents.RaiseAttackSwing(itemId);
    else
      GameEvents.RaiseRangedAttack(itemId);
  }
}
