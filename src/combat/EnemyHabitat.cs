// Original — habitat data helpers around the existing enemy AI (contract 3)
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   Pure habitat math: wander targets, chase leash, biome attractors.
///   Does not own AI states — <see cref="EnemyBase"/> still uses the
///   existing behavior switch and only asks these helpers for a direction.
/// </summary>
public static class EnemyHabitat
{
  public const float ForestMaxRadial = 0.58f;
  public const float BeachMinRadial = 0.70f;
  public const float BeachMaxRadial = 0.95f;
  public const float ForestMinHeight01 = 0.56f;
  public const float ForestMaxHeight01 = 0.78f;
  public const float ShoreMinHeight01 = 0.48f;
  public const float ShoreMaxHeight01 = 0.62f;
  public const float WanderSlotSeconds = 5f;
  public const float LeashSlack = 1.08f;

  public static float DefaultWanderRadius(EnemyHabitatKind kind) =>
    kind switch
    {
      EnemyHabitatKind.ForestRiver => 18f,
      EnemyHabitatKind.BeachShore => 14f,
      _ => 0f
    };

  public static float EffectiveWanderRadius(EnemyHabitatKind kind, float authored) =>
    authored > 0f ? authored : DefaultWanderRadius(kind);

  public static float Horizontal(Vector3 a, Vector3 b)
  {
    float dx = a.X - b.X;
    float dz = a.Z - b.Z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  /// <summary>
  ///   Chase while the player is inside the den disk, or already in melee
  ///   of the enemy. WanderRadius &lt;= 0 keeps the old always-chase path.
  /// </summary>
  public static bool ShouldChase(
    Vector3 home,
    float wanderRadius,
    Vector3 enemyPos,
    Vector3 playerPos,
    float attackRange)
  {
    if (wanderRadius <= 0f)
      return true;

    float playerToEnemy = Horizontal(enemyPos, playerPos);
    if (playerToEnemy <= attackRange * 2f)
      return true;

    return Horizontal(home, playerPos) <= wanderRadius;
  }

  public static bool IsLeakingLeash(Vector3 home, float wanderRadius, Vector3 enemyPos) =>
    wanderRadius > 0f && Horizontal(home, enemyPos) > wanderRadius * LeashSlack;

  public static Vector3 ClampToHabitat(Vector3 home, float wanderRadius, Vector3 pos)
  {
    if (wanderRadius <= 0f)
      return pos;

    var offset = new Vector3(pos.X - home.X, 0f, pos.Z - home.Z);
    float len = offset.Length();
    if (len <= wanderRadius || len < 0.0001f)
      return pos;

    var n = offset / len;
    return new Vector3(home.X + n.X * wanderRadius, pos.Y, home.Z + n.Z * wanderRadius);
  }

  /// <summary>
  ///   Den / forage / drink-or-beach / forage. Slot is 0..3 from a wander clock.
  /// </summary>
  public static Vector3 PickWanderTarget(
    Vector3 home, Vector3 attractor, float wanderRadius, int slot)
  {
    if (wanderRadius <= 0f)
      return home;

    int phase = ((slot % 4) + 4) % 4;
    if (phase == 0)
      return home;

    if (phase == 2)
      return ClampToHabitat(home, wanderRadius, attractor);

    float ang = phase * 2.39996f;
    float r = wanderRadius * 0.55f;
    return new Vector3(
      home.X + Mathf.Cos(ang) * r,
      home.Y,
      home.Z + Mathf.Sin(ang) * r);
  }

  /// <summary>
  ///   World XZ attractor for a den: river bank for forest species, outer
  ///   coastline for shore species. Deterministic from the island spec.
  /// </summary>
  public static Vector2 WorldAttractor(
    Vector2 homeXz, EnemyHabitatKind kind, IReadOnlyList<IslandSpec> specs)
  {
    if (kind == EnemyHabitatKind.None || specs == null || specs.Count == 0)
      return homeXz;

    var spec = NearestSpec(homeXz, specs);
    var local = homeXz - spec.Center;
    float hash = Hash01(homeXz);

    if (kind == EnemyHabitatKind.ForestRiver)
    {
      float along = 0.32f + hash * 0.36f;
      float side = hash > 0.5f ? 1f : -1f;
      return spec.Center + IslandHeightmap.SampleRiverBankLocal(spec, along, side);
    }

    if (kind == EnemyHabitatKind.BeachShore)
    {
      var dir = local.LengthSquared() > 0.01f ? local.Normalized() : Vector2.Right;
      float theta = Mathf.Atan2(dir.Y, dir.X);
      float shore = IslandHeightmap.ShorelineRadius(spec, theta);
      return spec.Center + dir * (shore * 0.90f);
    }

    return homeXz;
  }

  public static IslandSpec NearestSpec(Vector2 worldXz, IReadOnlyList<IslandSpec> specs)
  {
    var best = specs[0];
    float bestD = (worldXz - best.Center).LengthSquared();
    for (int i = 1; i < specs.Count; i++)
    {
      float d = (worldXz - specs[i].Center).LengthSquared();
      if (d < bestD)
      {
        bestD = d;
        best = specs[i];
      }
    }

    return best;
  }

  private static float Hash01(Vector2 p)
  {
    float n = Mathf.Sin(p.X * 12.9898f + p.Y * 78.233f) * 43758.5453f;
    return n - Mathf.Floor(n);
  }
}
