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

  #region Iter6.1 enemy behaviors (todo 3)

  /// <summary>Fixed flyer speed boost over its .tres MoveSpeed (Iter6.1).</summary>
  public const float FlyerSpeedBoost = 1.25f;

  /// <summary>Mutant rage triggers strictly below half health (Iter6.1).</summary>
  public const float MutantRageThreshold = 0.5f;

  public const float MutantRageSpeedBoost = 1.5f;
  public const float MutantRageRangeBoost = 1.5f;

  /// <summary>Speed/range multipliers applied by a mutant rage.</summary>
  public readonly struct RageFactors
  {
    public readonly float SpeedMultiplier;
    public readonly float RangeMultiplier;

    public RageFactors(float speedMultiplier, float rangeMultiplier)
    {
      SpeedMultiplier = speedMultiplier;
      RangeMultiplier = rangeMultiplier;
    }
  }

  /// <summary>
  ///   Vertical velocity that seeks the target at <paramref name="dy"/>
  ///   meters above the enemy, capped at <paramref name="speed"/>. Returns
  ///   the exact closing velocity for the current frame (dy/dt) when the gap
  ///   is small, so the flyer converges instead of oscillating around the
  ///   target Y. Zero when dt is not positive.
  /// </summary>
  public static float FlyerVerticalSeek(float dy, float speed, float dt)
  {
    if (dt <= 0f)
      return 0f;

    return Mathf.Clamp(dy / dt, -speed, speed);
  }

  /// <summary>
  ///   Effective speed multiplier of a slow that has <paramref name="remaining"/>
  ///   seconds left BEFORE the current <paramref name="dt"/> tick: the imposed
  ///   <paramref name="factor"/> while time remains after the tick, decaying
  ///   back to 1f once the timer expires. Deterministic for tests.
  /// </summary>
  public static float SlowFactor(float factor, float remaining, float dt) =>
    remaining > dt ? factor : 1f;

  /// <summary>
  ///   Whether a sea beast pursues: the player is on the watched float, or no
  ///   float is wired (<paramref name="defaultAggro"/> — the enemy treats the
  ///   player as permanently at sea and always chases).
  /// </summary>
  public static bool SeaBeastAggro(bool playerOnFloat, bool defaultAggro) =>
    playerOnFloat || defaultAggro;

  /// <summary>
  ///   Mutant rage multipliers: below <see cref="MutantRageThreshold"/> health
  ///   the mutant moves and slams ×1.5, otherwise both are 1f.
  /// </summary>
  public static RageFactors MutantRage(float hpRatio) =>
    hpRatio < MutantRageThreshold
      ? new RageFactors(MutantRageSpeedBoost, MutantRageRangeBoost)
      : new RageFactors(1f, 1f);

  /// <summary>
  ///   Mutant slam radius: the base attack range scaled by the current rage
  ///   range multiplier (rage widens the AoE slam).
  /// </summary>
  public static float MutantAoeRadius(float baseRadius, float hpRatio) =>
    baseRadius * MutantRage(hpRatio).RangeMultiplier;

  #endregion Iter6.1 enemy behaviors (todo 3)
}
