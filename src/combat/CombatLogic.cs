// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   What an attack input resolves to for the currently selected item
///   (Iter6 plan Decision 1: tools ARE weapons).
/// </summary>
public enum AttackType
{
  None,
  Melee,
  Throw,
  Shoot
}

/// <summary>
///   Pure static combat math (Iter6 plan Decision 8). No scene tree access —
///   every function is deterministic and unit-tested.
/// </summary>
public static class CombatLogic
{
  /// <summary>Fixed charge speed multiplier (Decision 7/8).</summary>
  public const float ChargeSpeedBoost = 3f;

  /// <summary>Fixed night speed multiplier for wolves (Decision 7/8).</summary>
  public const float NightSpeedBoost = 1.5f;

  /// <summary>
  ///   True when <paramref name="elapsed"/> seconds since the last attack are
  ///   at least <paramref name="cooldown"/> seconds (Decision 3 cooldown gate).
  /// </summary>
  public static bool IsReady(float elapsed, float cooldown) =>
    elapsed >= cooldown;

  /// <summary>
  ///   Ballistic projectile position after <paramref name="t"/> seconds:
  ///   x = x0 + vx·t, y = y0 + vy·t + ½·g·t², z = z0 + vz·t.
  /// </summary>
  public static Vector3 ProjectilePosition(
    Vector3 start, Vector3 velocity, float gravity, float t
  )
  {
    return new Vector3(
      start.X + velocity.X * t,
      start.Y + velocity.Y * t + 0.5f * gravity * t * t,
      start.Z + velocity.Z * t
    );
  }

  /// <summary>
  ///   Resolves the attack the selected item can perform (Decision 1). The
  ///   id match order is case-sensitive <c>Contains("spear")</c> →
  ///   <c>Contains("bow")</c> → <c>Contains("axe")</c>, everything else that
  ///   is a tool melees ("stone_axe" melees on purpose). Non-tools never
  ///   attack. Ammo gates: spears need a held spear (primary), bows need an
  ///   arrow (secondary); without ammo both fall back to melee.
  /// </summary>
  public static AttackType ResolveAttack(
    string selectedId,
    bool hasPrimaryAmmo,
    bool hasSecondaryAmmo,
    bool isTool
  )
  {
    if (!isTool)
      return AttackType.None;

    if (selectedId.Contains("spear"))
      return hasPrimaryAmmo ? AttackType.Throw : AttackType.Melee;

    if (selectedId.Contains("bow"))
      return hasSecondaryAmmo ? AttackType.Shoot : AttackType.Melee;

    // "axe" and every other tool melee (Decision 1).
    return AttackType.Melee;
  }

  /// <summary>
  ///   3f when the player is inside <paramref name="chargeRange"/> AND the
  ///   charge cooldown is ready, otherwise 1f (Decision 7/8).
  /// </summary>
  public static float ChargeSpeedMultiplier(
    float dist, float chargeRange, bool cooldownReady
  ) => dist <= chargeRange && cooldownReady ? ChargeSpeedBoost : 1f;

  /// <summary>
  ///   1.5f for wolves at night, otherwise 1f (Decision 7/8).
  /// </summary>
  public static float NightSpeedMultiplier(string enemyId, bool isNight) =>
    enemyId == "wolf" && isNight ? NightSpeedBoost : 1f;

  /// <summary>
  ///   True when the target is inside <paramref name="range"/> AND the attack
  ///   cooldown is ready (Decision 8).
  /// </summary>
  public static bool ShouldAttack(float dist, float range, bool cooldownReady) =>
    dist <= range && cooldownReady;
}
