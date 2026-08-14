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

  /// <summary>Wolf-specific night speed boost (Decision 7/8).</summary>
  public const float NightSpeedBoost = 1.5f;

  /// <summary>General night speed/damage multiplier for all enemies (T8.5.5).</summary>
  public const float NightMultiplier = 1.25f;

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
  ///   T8.5.5: night speed boost — wolves keep their 1.5× hunt boost
  ///   (Decision 7/8), every other enemy gets the default 1.25×; day = 1.
  /// </summary>
  public static float NightSpeedMultiplier(string enemyId, bool isNight) =>
    !isNight ? 1f : enemyId == "wolf" ? NightSpeedBoost : NightMultiplier;

  /// <summary>
  ///   T8.5.5: night damage multiplier (default 1.25), day = 1. The constant
  ///   value is exported on EnemyBase (NightDamageMultiplierValue) and applied
  ///   through this pure helper so the gate is unit-testable.
  /// </summary>
  public static float NightDamageMultiplier(bool isNight, float value) =>
    isNight ? value : 1f;

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

  #region Iter6.1 boss phases (todo 4)

  /// <summary>Phase 2 (minion summons) starts at or below 60% hp.</summary>
  public const float BossPhase2Threshold = 0.6f;

  /// <summary>Phase 3 (rage) starts below 30% hp.</summary>
  public const float BossPhase3Threshold = 0.3f;

  public const float BossRageSpeedBoost = 1.8f;
  public const float BossRageDamageBoost = 1.5f;

  /// <summary>Phase 3 attack interval factor (0.6 = 40% shorter cooldown).</summary>
  public const float BossRageCooldownFactor = 0.6f;

  /// <summary>Speed/damage multipliers applied by a phase-3 boss rage.</summary>
  public readonly struct BossRageFactors
  {
    public readonly float SpeedMultiplier;
    public readonly float DamageMultiplier;

    public BossRageFactors(float speedMultiplier, float damageMultiplier)
    {
      SpeedMultiplier = speedMultiplier;
      DamageMultiplier = damageMultiplier;
    }
  }

  /// <summary>
  ///   Boss phase from the hp ratio: 1 above 60%, 2 between 30% and 60%
  ///   (inclusive at the top), 3 below 30%.
  /// </summary>
  public static int BossPhase(float hpRatio) =>
    hpRatio > BossPhase2Threshold ? 1 : hpRatio > BossPhase3Threshold ? 2 : 3;

  /// <summary>
  ///   True when the summon timer elapsed its interval AND fewer than
  ///   <paramref name="max"/> minions are alive (todo 4 phase-2 spawning).
  /// </summary>
  public static bool ShouldSpawnMinion(
    float elapsed, float interval, int alive, int max
  ) => elapsed >= interval && alive < max;

  /// <summary>
  ///   Boss rage multipliers: speed ×1.8 and damage ×1.5 in phase 3, base 1f
  ///   before that (phase 0 = no controller attached).
  /// </summary>
  public static BossRageFactors BossRage(int phase) =>
    phase >= 3
      ? new BossRageFactors(BossRageSpeedBoost, BossRageDamageBoost)
      : new BossRageFactors(1f, 1f);

  /// <summary>Phase 3 shortens the boss attack interval (0.6× cooldown).</summary>
  public static float BossRageCooldownMultiplier(int phase) =>
    phase >= 3 ? BossRageCooldownFactor : 1f;

  #endregion Iter6.1 boss phases (todo 4)

  #region Iter6.1 dual weapon slots (todo 5)

  /// <summary>
  ///   The item attacks use (Iter6.1 todo 5): the secondary slot item when
  ///   the secondary slot is the active one AND equipped, otherwise the
  ///   primary hotbar item. An empty secondary always falls back to the
  ///   primary slot. Deterministic for tests.
  /// </summary>
  public static ItemData? EffectiveItem(
    ItemData? primary, ItemData? secondary, bool secondarySlotActive
  ) => secondarySlotActive && secondary != null ? secondary : primary;

  #endregion Iter6.1 dual weapon slots (todo 5)
}
