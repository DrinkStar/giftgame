// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Deterministic archipelago layout (T8.5.1). Given one world seed it
///   produces the fixed island list: one Main island at the origin, 2-3
///   Spawn islands 120-180 m out and one Storm island 350-450 m out (the
///   shark-king boss arena). No world coordinate is hardcoded — every
///   position, radius and per-island seed is drawn from a
///   <see cref="System.Random"/> seeded with the world seed, so the same
///   seed always reproduces the same archipelago and different seeds produce
///   different ones.
/// </summary>
public static class WorldLayout
{
    /// <summary>Main island radius in meters (covers the existing 40×40 Ground grid).</summary>
    public const float MainRadius = 32f;

    /// <summary>Main island terrain amplitude in meters.</summary>
    public const float MainHeightScale = 5f;

    /// <summary>Main island noise frequency (feature size ≈ 25 m).</summary>
    public const float MainNoiseScale = 0.04f;

    /// <summary>Minimum spawn-island center distance from the origin.</summary>
    public const float SpawnMinDistance = 120f;

    /// <summary>Maximum spawn-island center distance from the origin.</summary>
    public const float SpawnMaxDistance = 180f;

    /// <summary>Smallest allowed spawn-island count.</summary>
    public const int MinSpawnIslands = 2;

    /// <summary>Largest allowed spawn-island count.</summary>
    public const int MaxSpawnIslands = 3;

    /// <summary>Minimum spawn-island radius in meters.</summary>
    public const float SpawnMinRadius = 16f;

    /// <summary>Maximum spawn-island radius in meters.</summary>
    public const float SpawnMaxRadius = 22f;

    /// <summary>Spawn-island terrain amplitude in meters.</summary>
    public const float SpawnHeightScale = 8f;

    /// <summary>Spawn-island noise frequency (feature size ≈ 20 m).</summary>
    public const float SpawnNoiseScale = 0.05f;

    /// <summary>Minimum storm-island center distance from the origin.</summary>
    public const float StormMinDistance = 350f;

    /// <summary>Maximum storm-island center distance from the origin.</summary>
    public const float StormMaxDistance = 450f;

    /// <summary>Minimum storm-island radius in meters.</summary>
    public const float StormMinRadius = 26f;

    /// <summary>Maximum storm-island radius in meters.</summary>
    public const float StormMaxRadius = 34f;

    /// <summary>Storm-island terrain amplitude in meters (rockier boss arena).</summary>
    public const float StormHeightScale = 14f;

    /// <summary>Storm-island noise frequency (feature size ≈ 25 m).</summary>
    public const float StormNoiseScale = 0.04f;

    /// <summary>
    ///   Generates the deterministic island list for a world seed.
    /// </summary>
    /// <param name="worldSeed">The world's fixed generation seed.</param>
    /// <returns>
    ///   A fresh list ordered [Main, Spawn…, Storm]; never null. Specs are
    ///   immutable records, so callers cannot corrupt the layout.
    /// </returns>
    public static List<IslandSpec> Generate(long worldSeed)
    {
        // Mix both halves of the long seed so nearby world seeds cannot
        // collide on the same int (System.Random takes an int seed).
        var rng = new Random(unchecked((int)(worldSeed ^ (worldSeed >> 32))));
        var islands = new List<IslandSpec>(MaxSpawnIslands + 2);

        // 1) Main island at the origin (mid tier; existing Ground and
        //    BuildingSystem stay in place — this spec only wraps them).
        islands.Add(new IslandSpec(
            Seed: rng.Next(),
            Center: Vector2.Zero,
            Radius: MainRadius,
            HeightScale: MainHeightScale,
            NoiseScale: MainNoiseScale,
            Tier: IslandTier.Main));

        // 2) 2-3 low-risk spawn islands 120-180 m out, spread around the
        //    main island with angular jitter so no two ever overlap.
        int spawnCount = rng.Next(MinSpawnIslands, MaxSpawnIslands + 1);
        for (int i = 0; i < spawnCount; i++)
        {
            float angle = Mathf.Tau * i / spawnCount
                + (float)(rng.NextDouble() - 0.5) * 0.8f;
            float distance = SpawnMinDistance
                + (float)rng.NextDouble() * (SpawnMaxDistance - SpawnMinDistance);
            float radius = SpawnMinRadius
                + (float)rng.NextDouble() * (SpawnMaxRadius - SpawnMinRadius);

            islands.Add(new IslandSpec(
                Seed: rng.Next(),
                Center: new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance),
                Radius: radius,
                HeightScale: SpawnHeightScale,
                NoiseScale: SpawnNoiseScale,
                Tier: IslandTier.Spawn));
        }

        // 3) One high-risk storm island 350-450 m out — the shark-king boss
        //    arena. Storm weather is owned by WeatherService / the main
        //    orchestrator, not by the layout.
        float stormAngle = (float)(rng.NextDouble() * Mathf.Tau);
        float stormDistance = StormMinDistance
            + (float)rng.NextDouble() * (StormMaxDistance - StormMinDistance);
        float stormRadius = StormMinRadius
            + (float)rng.NextDouble() * (StormMaxRadius - StormMinRadius);

        islands.Add(new IslandSpec(
            Seed: rng.Next(),
            Center: new Vector2(Mathf.Cos(stormAngle) * stormDistance, Mathf.Sin(stormAngle) * stormDistance),
            Radius: stormRadius,
            HeightScale: StormHeightScale,
            NoiseScale: StormNoiseScale,
            Tier: IslandTier.Storm));

        return islands;
    }
}
