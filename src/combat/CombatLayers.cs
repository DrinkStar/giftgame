// Original — no upstream port
namespace SeaAnomaly;

/// <summary>
///   3D physics layer bits for combat. Layers 1–8 are the existing project
///   layers (World … Enemies). Hurtboxes occupy the next free slots:
///   <list type="bullet">
///     <item>Layer 9 / bit 256 = EnemyHurtbox — player melee ray and projectiles</item>
///     <item>Layer 10 / bit 512 = PlayerHurtbox — enemy attack overlap</item>
///   </list>
///   Interact (E) stays mask 29 (layers 1+3+4+5) and does not include 9/10.
///   Enemy CharacterBody stays layer 8 (128) for movement physics only.
///   Production melee/projectiles mask the EnemyHurtbox, not the body.
///   <see cref="Hurtbox.ResolveEnemy"/> still accepts an <c>EnemyBase</c>
///   collider as a documented thin-miss fallback if some other query hits
///   the body.
/// </summary>
public static class CombatLayers
{
  public const uint WorldMask = 1u;
  public const uint EnemiesMask = 128u;
  public const uint EnemyHurtboxMask = 256u;
  public const uint PlayerHurtboxMask = 512u;

  /// <summary>Player melee ray: EnemyHurtbox only (not the body capsule).</summary>
  public const uint MeleeRayMask = EnemyHurtboxMask;

  /// <summary>Projectiles: World (stop on ground) + EnemyHurtbox.</summary>
  public const uint ProjectileMask = WorldMask | EnemyHurtboxMask;
}
