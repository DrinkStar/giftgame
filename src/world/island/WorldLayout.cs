// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Deterministic archipelago layout (T8.5.1). Given one world seed it
///   produces the fixed island list: one Main island at the origin (product
///   spawn after 开始游戏), then Harvest / Ruin / Mutant story islands on a
///   near ring, one Storm island farther out (the shark-king arena), and
///   three optional exploration islands (Wild / Atoll / Wreck) between Storm
///   and the tutorial ring, plus 1–2 far-pole easter eggs (Volcano / Polar).
///   Exploration and easter-egg islands are not on the quest chain.
///   No world coordinate is hardcoded — every position, radius and
///   per-island seed is drawn from a <see cref="System.Random"/> seeded
///   with the world seed, so the same seed always reproduces the same
///   archipelago and different seeds produce different ones.
///
///   The chapter-1 tutorial island is NOT in <see cref="Generate"/>: it uses
///   a separate RNG stream (<see cref="GenerateTutorialIsland"/>) so product
///   islands stay byte-identical for a given seed. 开始游戏 spawns on Main
///   (origin); 新手教程 teleports to the tutorial island.
/// </summary>
public static class WorldLayout
{
    /// <summary>
    ///   Main / starter island radius in meters. Large enough for radio,
    ///   trees, beach and a 40 m spawn-safe ring, without reading as a
    ///   huge empty disc or a 36 m tutorial clone.
    /// </summary>
    public const float MainRadius = 120f;

    /// <summary>
    ///   Main island terrain amplitude in meters. Inland ridges reach
    ///   about +HeightScale/2; <see cref="IslandHeightmap"/> blends a
    ///   building plateau at the origin so Y stays on the Ground box top
    ///   (0.5 m) and does not bury the BuildingSystem grid.
    /// </summary>
    public const float MainHeightScale = 10f;

    /// <summary>Main island noise frequency (feature size ≈ 31 m).</summary>
    public const float MainNoiseScale = 0.032f;

    /// <summary>Minimum shore-to-shore water between any two product islands.</summary>
    public const float IslandShoreGap = 16f;

    /// <summary>
    ///   Closest story-island center distance. Must clear
    ///   MainRadius + HarvestMaxRadius so the farm islet never overlaps Main;
    ///   the leftover water is a short swim / paddle.
    /// </summary>
    public const float StoryMinDistance = 216f;

    /// <summary>Farthest story-island center (mutant woods), still raft-plausible.</summary>
    public const float StoryMaxDistance = 265f;

    /// <summary>Harvest / farmland island radius range.</summary>
    public const float HarvestMinRadius = 63f;

    /// <summary>Harvest / farmland island radius range.</summary>
    public const float HarvestMaxRadius = 75f;

    /// <summary>Harvest island terrain amplitude (rolling fields).</summary>
    public const float HarvestHeightScale = 7f;

    /// <summary>Harvest island noise frequency.</summary>
    public const float HarvestNoiseScale = 0.042f;

    /// <summary>Ruin / stone island radius range.</summary>
    public const float RuinMinRadius = 54f;

    /// <summary>Ruin / stone island radius range.</summary>
    public const float RuinMaxRadius = 66f;

    /// <summary>Ruin island terrain amplitude (rockier, taller).</summary>
    public const float RuinHeightScale = 16f;

    /// <summary>Ruin island noise frequency.</summary>
    public const float RuinNoiseScale = 0.055f;

    /// <summary>Mutant woods island radius range.</summary>
    public const float MutantMinRadius = 69f;

    /// <summary>Mutant woods island radius range.</summary>
    public const float MutantMaxRadius = 81f;

    /// <summary>Mutant woods terrain amplitude.</summary>
    public const float MutantHeightScale = 11f;

    /// <summary>Mutant woods noise frequency.</summary>
    public const float MutantNoiseScale = 0.038f;

    /// <summary>Minimum storm-island center distance from the origin.</summary>
    public const float StormMinDistance = 410f;

    /// <summary>Maximum storm-island center distance from the origin.</summary>
    public const float StormMaxDistance = 470f;

    /// <summary>Minimum storm-island radius in meters.</summary>
    public const float StormMinRadius = 36f;

    /// <summary>Maximum storm-island radius in meters.</summary>
    public const float StormMaxRadius = 46f;

    /// <summary>Storm-island terrain amplitude in meters (rockier boss arena).</summary>
    public const float StormHeightScale = 14f;

    /// <summary>Storm-island noise frequency (feature size ≈ 25 m).</summary>
    public const float StormNoiseScale = 0.04f;

    /// <summary>
    ///   Closest exploration-island center. Clears worst-case Storm
    ///   (StormMaxDistance + StormMaxRadius) plus ExploreMaxRadius and
    ///   <see cref="IslandShoreGap"/>.
    /// </summary>
    public const float ExploreMinDistance = 600f;

    /// <summary>
    ///   Farthest exploration-island center. Stays inside
    ///   TutorialMinDistance − TutorialMaxRadius − ExploreMaxRadius − gap
    ///   so the separate tutorial stream can never overlap these islets.
    /// </summary>
    public const float ExploreMaxDistance = 640f;

    /// <summary>Wild / hunting islet radius range.</summary>
    public const float WildMinRadius = 57f;

    /// <summary>Wild / hunting islet radius range.</summary>
    public const float WildMaxRadius = 66f;

    /// <summary>Wild islet terrain amplitude (wooded ridges).</summary>
    public const float WildHeightScale = 12f;

    /// <summary>Wild islet noise frequency.</summary>
    public const float WildNoiseScale = 0.036f;

    /// <summary>Atoll / extra-beach radius range.</summary>
    public const float AtollMinRadius = 51f;

    /// <summary>Atoll / extra-beach radius range.</summary>
    public const float AtollMaxRadius = 63f;

    /// <summary>Atoll terrain amplitude (low sand ring).</summary>
    public const float AtollHeightScale = 6f;

    /// <summary>Atoll noise frequency.</summary>
    public const float AtollNoiseScale = 0.048f;

    /// <summary>Wreck / salvage islet radius range.</summary>
    public const float WreckMinRadius = 48f;

    /// <summary>Wreck / salvage islet radius range.</summary>
    public const float WreckMaxRadius = 57f;

    /// <summary>Wreck islet terrain amplitude.</summary>
    public const float WreckHeightScale = 10f;

    /// <summary>Wreck islet noise frequency.</summary>
    public const float WreckNoiseScale = 0.052f;

    /// <summary>Optional post-story exploration islands in <see cref="Generate"/>.</summary>
    public const int ExplorationIslandCount = 3;

    /// <summary>
    ///   Closest easter-egg center |Z|. Clears TutorialMaxDistance + radii
    ///   plus <see cref="IslandShoreGap"/> so the tutorial stream cannot
    ///   overlap volcano / polar islands.
    /// </summary>
    public const float EasterMinDistance = 960f;

    /// <summary>Farthest easter-egg center |Z| (still a long raft voyage).</summary>
    public const float EasterMaxDistance = 1120f;

    /// <summary>Volcano islet radius range.</summary>
    public const float VolcanoMinRadius = 54f;

    /// <summary>Volcano islet radius range.</summary>
    public const float VolcanoMaxRadius = 66f;

    /// <summary>Volcano terrain amplitude (tall cone / ridges).</summary>
    public const float VolcanoHeightScale = 20f;

    /// <summary>Volcano noise frequency.</summary>
    public const float VolcanoNoiseScale = 0.046f;

    /// <summary>Polar / snow mountain radius range.</summary>
    public const float PolarMinRadius = 60f;

    /// <summary>Polar / snow mountain radius range.</summary>
    public const float PolarMaxRadius = 72f;

    /// <summary>Polar terrain amplitude (alpine peak).</summary>
    public const float PolarHeightScale = 18f;

    /// <summary>Polar noise frequency.</summary>
    public const float PolarNoiseScale = 0.034f;

    /// <summary>At least one easter-egg island is always present.</summary>
    public const int EasterEggMinCount = 1;

    /// <summary>At most volcano + polar together.</summary>
    public const int EasterEggMaxCount = 2;

    /// <summary>
    ///   Mix constant for the tutorial-island RNG. XOR'd with the world seed
    ///   so <see cref="GenerateTutorialIsland"/> never consumes the product
    ///   <see cref="Generate"/> stream.
    /// </summary>
    public const int TutorialStreamMix = unchecked((int)0x71A1B1E5);

    /// <summary>
    ///   Minimum tutorial-island center distance from the origin. Must clear
    ///   the exploration ring (ExploreMaxDistance + radii + gap).
    /// </summary>
    public const float TutorialMinDistance = 770f;

    /// <summary>Maximum tutorial-island center distance from the origin.</summary>
    public const float TutorialMaxDistance = 830f;

    /// <summary>Minimum tutorial-island radius in meters.</summary>
    public const float TutorialMinRadius = 36f;

    /// <summary>Maximum tutorial-island radius in meters.</summary>
    public const float TutorialMaxRadius = 42f;

    /// <summary>
    ///   Tutorial-island terrain amplitude. Matches the original Main so the
    ///   building plateau blends to the Ground box top (Y = 0.5).
    /// </summary>
    public const float TutorialHeightScale = 12f;

    /// <summary>Tutorial-island noise frequency (feature size ≈ 25 m).</summary>
    public const float TutorialNoiseScale = 0.04f;

    /// <summary>
    ///   Always-present product islands (Main + story + Storm + exploration).
    ///   Easter eggs add 1–2 more; see <see cref="EasterEggMinCount"/>.
    /// </summary>
    public const int ProductIslandCount = 8;

    /// <summary>True for optional free-exploration islets (not quest-gated).</summary>
    public static bool IsExploration(IslandTier tier) =>
        tier is IslandTier.Wild or IslandTier.Atoll or IslandTier.Wreck;

    /// <summary>True for far-pole volcano / snow easter eggs (not quest-gated).</summary>
    public static bool IsEasterEgg(IslandTier tier) =>
        tier is IslandTier.Volcano or IslandTier.Polar;

    /// <summary>
    ///   Generates the deterministic island list for a world seed.
    /// </summary>
    /// <param name="worldSeed">The world's fixed generation seed.</param>
    /// <returns>
    ///   A fresh list ordered [Main, Harvest, Ruin, Mutant, Storm, Wild,
    ///   Atoll, Wreck] plus 1–2 easter eggs (Volcano / Polar); never null.
    ///   Specs are immutable records. Exploration and easter eggs are
    ///   appended after Storm so the original five keep the same RNG draws.
    /// </returns>
    public static List<IslandSpec> Generate(long worldSeed)
    {
        // Mix both halves of the long seed so nearby world seeds cannot
        // collide on the same int (System.Random takes an int seed).
        var rng = new Random(unchecked((int)(worldSeed ^ (worldSeed >> 32))));
        var islands = new List<IslandSpec>(ProductIslandCount + EasterEggMaxCount);

        // 1) Main island at the origin (product spawn). Existing Ground and
        //    BuildingSystem stay in place — this spec only wraps them.
        islands.Add(new IslandSpec(
            Seed: rng.Next(),
            Center: Vector2.Zero,
            Radius: MainRadius,
            HeightScale: MainHeightScale,
            NoiseScale: MainNoiseScale,
            Tier: IslandTier.Main));

        // 2) Three story islands on a shared rotating ring so the farm,
        //    stones and woods are distinct places a short swim/paddle apart,
        //    never copies of Main.
        float ringRotation = (float)(rng.NextDouble() * Mathf.Tau);
        islands.Add(PlaceStoryIsland(
            rng, islands, IslandTier.Harvest, ringRotation,
            StoryMinDistance, StoryMinDistance + 20f,
            HarvestMinRadius, HarvestMaxRadius,
            HarvestHeightScale, HarvestNoiseScale));
        islands.Add(PlaceStoryIsland(
            rng, islands, IslandTier.Ruin, ringRotation + Mathf.Tau / 3f,
            StoryMinDistance + 10f, StoryMinDistance + 32f,
            RuinMinRadius, RuinMaxRadius,
            RuinHeightScale, RuinNoiseScale));
        islands.Add(PlaceStoryIsland(
            rng, islands, IslandTier.Mutant, ringRotation + Mathf.Tau * 2f / 3f,
            StoryMaxDistance - 26f, StoryMaxDistance,
            MutantMinRadius, MutantMaxRadius,
            MutantHeightScale, MutantNoiseScale));

        // 3) Storm / shark-king arena farther out — a raft voyage, not a
        //    swim. Weather is owned by WeatherService, not by the layout.
        islands.Add(PlaceStormIsland(rng, islands));

        // 4) Optional exploration ring: past Storm, inside the tutorial
        //    exclusion. Same world seed → same extra islands; nothing on
        //    the main quest requires visiting them.
        float exploreRotation = (float)(rng.NextDouble() * Mathf.Tau);
        islands.Add(PlaceExplorationIsland(
            rng, islands, IslandTier.Wild, exploreRotation,
            WildMinRadius, WildMaxRadius, WildHeightScale, WildNoiseScale));
        islands.Add(PlaceExplorationIsland(
            rng, islands, IslandTier.Atoll, exploreRotation + Mathf.Tau / 3f,
            AtollMinRadius, AtollMaxRadius, AtollHeightScale, AtollNoiseScale));
        islands.Add(PlaceExplorationIsland(
            rng, islands, IslandTier.Wreck, exploreRotation + Mathf.Tau * 2f / 3f,
            WreckMinRadius, WreckMaxRadius, WreckHeightScale, WreckNoiseScale));

        // 5) Easter eggs far on ±Z, after exploration so the first eight
        //    islands keep their draws. roll 0 = volcano, 1 = polar, 2 = both.
        AppendEasterEggs(rng, islands);

        return islands;
    }

    /// <summary>
    ///   Dedicated chapter-1 tutorial island for a world seed. Separate RNG
    ///   stream: product <see cref="Generate"/> layouts are unchanged.
    ///   Same seed → same tutorial spec; the island sits beyond the storm
    ///   ring so it never overlaps Main / story / Storm.
    /// </summary>
    public static IslandSpec GenerateTutorialIsland(long worldSeed)
    {
        var rng = new Random(unchecked((int)(worldSeed ^ (worldSeed >> 32) ^ TutorialStreamMix)));
        float angle = (float)(rng.NextDouble() * Mathf.Tau);
        float distance = TutorialMinDistance
            + (float)rng.NextDouble() * (TutorialMaxDistance - TutorialMinDistance);
        float radius = TutorialMinRadius
            + (float)rng.NextDouble() * (TutorialMaxRadius - TutorialMinRadius);

        return new IslandSpec(
            Seed: rng.Next(),
            Center: new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance),
            Radius: radius,
            HeightScale: TutorialHeightScale,
            NoiseScale: TutorialNoiseScale,
            Tier: IslandTier.Tutorial);
    }

    private static IslandSpec PlaceStoryIsland(
        Random rng,
        List<IslandSpec> existing,
        IslandTier tier,
        float angleCenter,
        float minDistance,
        float maxDistance,
        float minRadius,
        float maxRadius,
        float heightScale,
        float noiseScale)
    {
        float radius = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
        var center = Vector2.Zero;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = angleCenter + (float)(rng.NextDouble() - 0.5) * 0.45f;
            float distance = minDistance
                + (float)rng.NextDouble() * (maxDistance - minDistance);
            if (attempt > 8)
                distance += 8f * (attempt - 8);
            center = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            if (Fits(center, radius, existing, IslandShoreGap))
                break;
        }

        return new IslandSpec(
            Seed: rng.Next(),
            Center: center,
            Radius: radius,
            HeightScale: heightScale,
            NoiseScale: noiseScale,
            Tier: tier);
    }

    private static IslandSpec PlaceStormIsland(Random rng, List<IslandSpec> existing)
    {
        float radius = StormMinRadius
            + (float)rng.NextDouble() * (StormMaxRadius - StormMinRadius);
        var center = Vector2.Zero;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float distance = StormMinDistance
                + (float)rng.NextDouble() * (StormMaxDistance - StormMinDistance);
            center = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            if (Fits(center, radius, existing, IslandShoreGap))
                break;
        }

        if (!Fits(center, radius, existing, IslandShoreGap))
        {
            // Fail-closed: park due opposite the harvest island at max range.
            var away = existing.Count > 1 ? existing[1].Center : Vector2.Right;
            if (away.LengthSquared() < 0.01f)
                away = Vector2.Right;
            center = away.Normalized() * -StormMaxDistance;
        }

        return new IslandSpec(
            Seed: rng.Next(),
            Center: center,
            Radius: radius,
            HeightScale: StormHeightScale,
            NoiseScale: StormNoiseScale,
            Tier: IslandTier.Storm);
    }

    private static IslandSpec PlaceExplorationIsland(
        Random rng,
        List<IslandSpec> existing,
        IslandTier tier,
        float angleCenter,
        float minRadius,
        float maxRadius,
        float heightScale,
        float noiseScale)
    {
        float radius = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
        float minDist = Mathf.Max(
            ExploreMinDistance,
            StormMaxDistance + StormMaxRadius + radius + IslandShoreGap);
        float maxDist = Mathf.Min(
            ExploreMaxDistance,
            TutorialMinDistance - TutorialMaxRadius - radius - IslandShoreGap);
        if (maxDist < minDist)
            maxDist = minDist;

        var center = Vector2.Zero;
        bool placed = false;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = angleCenter + (float)(rng.NextDouble() - 0.5) * 0.50f;
            float distance = minDist + (float)rng.NextDouble() * (maxDist - minDist);
            center = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            if (Fits(center, radius, existing, IslandShoreGap))
            {
                placed = true;
                break;
            }
        }

        if (!placed)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = angleCenter + i * (Mathf.Tau / 12f);
                center = new Vector2(Mathf.Cos(angle) * maxDist, Mathf.Sin(angle) * maxDist);
                if (Fits(center, radius, existing, IslandShoreGap))
                    break;
            }
        }

        return new IslandSpec(
            Seed: rng.Next(),
            Center: center,
            Radius: radius,
            HeightScale: heightScale,
            NoiseScale: noiseScale,
            Tier: tier);
    }

    private static void AppendEasterEggs(Random rng, List<IslandSpec> existing)
    {
        int roll = rng.Next(3);
        int pole = rng.Next(2) == 0 ? 1 : -1;
        bool spawnVolcano = roll != 1;
        bool spawnPolar = roll != 0;

        if (spawnPolar)
        {
            existing.Add(PlaceEasterEggIsland(
                rng, existing, IslandTier.Polar, pole,
                PolarMinRadius, PolarMaxRadius, PolarHeightScale, PolarNoiseScale));
        }

        if (spawnVolcano)
        {
            int volcanoPole = spawnPolar ? -pole : pole;
            existing.Add(PlaceEasterEggIsland(
                rng, existing, IslandTier.Volcano, volcanoPole,
                VolcanoMinRadius, VolcanoMaxRadius, VolcanoHeightScale, VolcanoNoiseScale));
        }
    }

    private static IslandSpec PlaceEasterEggIsland(
        Random rng,
        List<IslandSpec> existing,
        IslandTier tier,
        int pole,
        float minRadius,
        float maxRadius,
        float heightScale,
        float noiseScale)
    {
        float radius = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
        float sign = pole >= 0 ? 1f : -1f;
        var center = Vector2.Zero;
        bool placed = false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float distance = EasterMinDistance
                + (float)rng.NextDouble() * (EasterMaxDistance - EasterMinDistance);
            if (attempt > 12)
                distance = Mathf.Min(EasterMaxDistance, distance + 12f * (attempt - 12));
            float xJitter = ((float)rng.NextDouble() - 0.5f) * 48f;
            center = new Vector2(xJitter, sign * distance);
            if (Fits(center, radius, existing, IslandShoreGap))
            {
                placed = true;
                break;
            }
        }

        if (!placed)
            center = new Vector2(0f, sign * EasterMaxDistance);

        return new IslandSpec(
            Seed: rng.Next(),
            Center: center,
            Radius: radius,
            HeightScale: heightScale,
            NoiseScale: noiseScale,
            Tier: tier);
    }

    private static bool Fits(
        Vector2 center, float radius, List<IslandSpec> existing, float gap)
    {
        foreach (var other in existing)
        {
            if ((center - other.Center).Length() < radius + other.Radius + gap)
                return false;
        }

        return true;
    }
}
